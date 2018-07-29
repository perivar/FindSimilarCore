using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Serilog;
using System.Threading.Tasks;
using CSCore;
using CSCore.Codecs.WAV;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using CommonUtils.Audio;

namespace FindSimilarClient
{
    public class SampleSourceStreamResult : FileStreamResult
    {
        // default buffer size as defined in BufferedStream type
        private const int BufferSize = 0x1000;
        private string MultipartBoundary = "THIS_STRING_SEPARATES";
        private const string CrLf = "\r\n";
        private ISampleSource SampleSource { get; set; }
        private long _lengthInBytes;
        private double _durationInSeconds;

        public SampleSourceStreamResult(ISampleSource sampleSource, string contentType)
            : base(new MemoryStream(), contentType)
        {
            if (sampleSource == null)
                throw new ArgumentNullException("sampleSource");

            SampleSource = sampleSource;
            Init();
        }

        public SampleSourceStreamResult(ISampleSource sampleSource, MediaTypeHeaderValue contentType)
            : base(new MemoryStream(), contentType)
        {
            if (sampleSource == null)
                throw new ArgumentNullException("sampleSource");

            SampleSource = sampleSource;
            Init();
        }

        private void Init()
        {
            _lengthInBytes = SampleSource.Length * SampleSource.WaveFormat.BytesPerSample;
            _durationInSeconds = (double)SampleSource.Length / (double)SampleSource.WaveFormat.SampleRate / (double)SampleSource.WaveFormat.Channels;
            Log.Verbose("SampleSource: byte length: {0}, duration: {1}", _lengthInBytes, _durationInSeconds);
        }

        private bool IsMultipartRequest(RangeHeaderValue range)
        {
            return range != null && range.Ranges != null && range.Ranges.Count > 1;
        }

        private bool IsRangeRequest(RangeHeaderValue range)
        {
            return range != null && range.Ranges != null && range.Ranges.Count > 0;
        }

        protected async Task WriteStreamAsync(HttpResponse response)
        {
            var bufferingFeature = response.HttpContext.Features.Get<IHttpBufferingFeature>();
            bufferingFeature?.DisableResponseBuffering();

            var range = response.HttpContext.GetRanges(_lengthInBytes);

            if (IsMultipartRequest(range))
            {
                // check https://github.com/aspnet/Mvc/blob/a67d9363e22be8ef63a1a62539991e1da3a6e30e/src/Microsoft.AspNetCore.Mvc.Core/Infrastructure/FileResultExecutorBase.cs
                response.ContentType = $"multipart/byteranges; boundary={MultipartBoundary}";
            }
            else
            {
                response.ContentType = ContentType.ToString();
            }

            response.Headers.Add("Accept-Ranges", "bytes");

            if (IsRangeRequest(range))
            {
                response.StatusCode = (int)HttpStatusCode.PartialContent;

                if (!IsMultipartRequest(range))
                {
                    // check https://github.com/dotnet/corefx/blob/master/src/System.Net.Http/src/System/Net/Http/Headers/ContentRangeHeaderValue.cs
                    // 14.16 Content-Range - A server sending a response with status code 416 (Requested range not satisfiable)
                    // SHOULD include a Content-Range field with a byte-range-resp-spec of "*". The instance-length specifies
                    // the current length of the selected resource.  e.g. */length
                    response.Headers.Add("Content-Range", $"bytes {range.Ranges.First().From}-{range.Ranges.First().To}/{_lengthInBytes}");
                }

                foreach (var rangeValue in range.Ranges)
                {
                    // check https://stackoverflow.com/questions/38069730/how-to-create-a-multipart-http-response-with-asp-net-core
                    // and https://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html
                    if (IsMultipartRequest(range))
                    {
                        await response.WriteAsync($"--{MultipartBoundary}");
                        await response.WriteAsync(CrLf);
                        await response.WriteAsync($"Content-type: {ContentType}");
                        await response.WriteAsync(CrLf);
                        await response.WriteAsync($"Content-Range: bytes {rangeValue.From}-{rangeValue.To}/{_lengthInBytes}");
                        await response.WriteAsync(CrLf);
                    }

                    await WriteRangeDataToResponseBody(rangeValue, response);

                    if (IsMultipartRequest(range))
                    {
                        await response.WriteAsync(CrLf);
                    }
                }

                if (IsMultipartRequest(range))
                {
                    await response.WriteAsync($"--{MultipartBoundary}");
                    await response.WriteAsync(CrLf);
                }
            }
            else
            {
                // write until end
                await WriteRemainingDataToResponseBody(response);
            }
        }

        // Read from the ISampleSource the correct number of bytes specified by the range value
        // and send them to the HttpResponse object

        private async Task WriteRangeDataToResponseBody(RangeItemHeaderValue rangeValue, HttpResponse response)
        {
            var startIndex = rangeValue.From ?? 0;
            var endIndex = rangeValue.To ?? 0;

            long totalToSend = endIndex - startIndex;
            long bytesRemaining = totalToSend + 1;

            // handle special case if the request is for only two bytes
            // and at the begginning (Range Header: bytes=0-1)
            if (startIndex == 0 && bytesRemaining == 2)
            {
                // ISampleSource uses 4 bytes internally and cannot send only two bytes
                // therefore send two dummy bytes instead
                response.ContentLength = bytesRemaining;
                await response.Body.WriteAsync(new byte[] { 0x00, 0x00 }, 0, (int)bytesRemaining);
                await response.Body.FlushAsync();
                return;
            }

            if (startIndex == 0)
            {
                // the beginning of a file requires a header
                try
                {
                    // send header unless it's a two byte request IWaveSource cannot handle
                    var headerBytes = SoundIOUtils.GetWaveHeaderBytes(
                            SampleSource.WaveFormat.BitsPerSample == 32 ? true : false,
                            (ushort)SampleSource.WaveFormat.Channels,
                            (ushort)SampleSource.WaveFormat.BitsPerSample,
                            SampleSource.WaveFormat.SampleRate,
                            (int)SampleSource.Length);

                    response.ContentLength = bytesRemaining + headerBytes.Length;
                    await response.Body.WriteAsync(headerBytes, 0, headerBytes.Length);
                }
                finally
                {
                    await response.Body.FlushAsync();
                }
            }
            else
            {
                response.ContentLength = bytesRemaining;
            }

            SampleSource.Position = startIndex;


            int read = 0;
            byte[] buffer = new byte[BufferSize];
            float[] floatBuffer = new float[BufferSize / 4];

            while (bytesRemaining > 0)
            {
                try
                {
                    if (bytesRemaining <= buffer.Length / 4)
                    {
                        read = SampleSource.Read(floatBuffer, 0, (int)bytesRemaining / 4);
                    }
                    else
                    {
                        read = SampleSource.Read(floatBuffer, 0, buffer.Length / 4);
                    }

                    if (read == 0)
                    {
                        return;
                    }

                    System.Buffer.BlockCopy(floatBuffer, 0, buffer, 0, read * 4);

                    await response.Body.WriteAsync(buffer, 0, read * 4);

                    bytesRemaining -= (read * 4);
                }
                catch (IndexOutOfRangeException)
                {
                    await response.Body.FlushAsync();
                    return;
                }
                finally
                {
                    await response.Body.FlushAsync();
                }
            }

        }

        // Read from the remaining bytes from ISampleSource and send them to the HttpResponse object
        private async Task WriteRemainingDataToResponseBody(HttpResponse response)
        {
            byte[] buffer = new byte[BufferSize];
            long totalToSend = SampleSource.Length - SampleSource.Position;
            int count = 0;

            long bytesRemaining = totalToSend + 1;

            /* 
                        while (bytesRemaining > 0)
                        {
                            try
                            {
                                if (bytesRemaining <= buffer.Length)
                                    count = SampleSource.Read(buffer, 0, (int)bytesRemaining);
                                else
                                    count = SampleSource.Read(buffer, 0, buffer.Length);

                                if (count == 0)
                                    return;

                                await response.Body.WriteAsync(buffer, 0, count);

                                bytesRemaining -= count;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                await response.Body.FlushAsync();
                                return;
                            }
                            finally
                            {
                                await response.Body.FlushAsync();
                            }
                        }
             */
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            await WriteStreamAsync(context.HttpContext.Response);
        }
    }
}