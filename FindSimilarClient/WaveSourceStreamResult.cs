using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CSCore;
using CSCore.Codecs.WAV;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FindSimilarClient
{
    public class WaveSourceStreamResult : FileStreamResult
    {
        // default buffer size as defined in BufferedStream type
        private const int BufferSize = 0x1000;
        private string MultipartBoundary = "THIS_STRING_SEPARATES";
        private const string CrLf = "\r\n";
        private IWaveSource WaveSource { get; set; }

        public WaveSourceStreamResult(IWaveSource waveSource, string contentType)
            : base(new MemoryStream(), contentType)
        {
            if (waveSource == null)
                throw new ArgumentNullException("waveSource");

            WaveSource = waveSource;
        }

        public WaveSourceStreamResult(IWaveSource waveSource, MediaTypeHeaderValue contentType)
            : base(new MemoryStream(), contentType)
        {
            if (waveSource == null)
                throw new ArgumentNullException("waveSource");

            WaveSource = waveSource;
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

            // this is a bit challenging, how do we get this to correspond with the read method?
            // var lengthInBytes = WaveSource.Length / WaveSource.WaveFormat.BytesPerSample;
            var lengthInBytes = WaveSource.Length;
            double duration = (double)WaveSource.Length / (double)WaveSource.WaveFormat.SampleRate / (double)WaveSource.WaveFormat.Channels / (double)WaveSource.WaveFormat.BytesPerSample;

            var range = response.HttpContext.GetRanges(lengthInBytes);

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
                    response.Headers.Add("Content-Range", $"bytes {range.Ranges.First().From}-{range.Ranges.First().To}/{lengthInBytes}");
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
                        await response.WriteAsync($"Content-Range: bytes {rangeValue.From}-{rangeValue.To}/{lengthInBytes}");
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

        // Read from the IWaveSource the correct number of bytes specified by the range value
        // and send them to the HttpResponse object

        private async Task WriteRangeDataToResponseBody(RangeItemHeaderValue rangeValue, HttpResponse response)
        {
            var startIndex = rangeValue.From ?? 0;
            var endIndex = rangeValue.To ?? 0;

            byte[] buffer = new byte[BufferSize];
            long totalToSend = endIndex - startIndex;
            int count = 0;

            long bytesRemaining = totalToSend + 1;

            // handle special case if the request is for only two bytes
            // and at the begginning (Range Header: bytes=0-1)
            if (startIndex == 0 && bytesRemaining == 2)
            {
                // IWaveSource uses 4 bytes internally and cannot send only two bytes
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
                    var headerBytes = GetWaveHeaderBytes(WaveSource.Length);
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

            WaveSource.Position = startIndex;

            while (bytesRemaining > 0)
            {
                try
                {
                    if (bytesRemaining <= buffer.Length)
                        count = WaveSource.Read(buffer, 0, (int)bytesRemaining);
                    else
                        count = WaveSource.Read(buffer, 0, buffer.Length);

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
        }

        private byte[] GetWaveHeaderBytes(long totalSampleCount)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                WriteWavHeader(ms,
                            WaveSource.WaveFormat.BitsPerSample == 32 ? true : false,
                            (ushort)WaveSource.WaveFormat.Channels,
                            (ushort)WaveSource.WaveFormat.BitsPerSample,
                            WaveSource.WaveFormat.SampleRate,
                            (int)WaveSource.Length);
                ms.Seek(0, SeekOrigin.Begin);
                return ms.ToArray();
            }
        }

        // totalSampleCount needs to be the combined count of samples of all channels. 
        // So if the left and right channels contain 1000 samples each, then totalSampleCount should be 2000.
        // isFloatingPoint should only be true if the audio data is in 32-bit floating-point format.
        private void WriteWavHeader(MemoryStream stream, bool isFloatingPoint, ushort channelCount, ushort bitDepth, int sampleRate, int totalSampleCount)
        {
            stream.Position = 0;

            // RIFF header.
            // Chunk ID.
            stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);

            // Chunk size.
            stream.Write(BitConverter.GetBytes(((bitDepth / 8) * totalSampleCount) + 36), 0, 4);

            // Format.
            stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);


            // Sub-chunk 1.
            // Sub-chunk 1 ID.
            stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);

            // Sub-chunk 1 size.
            stream.Write(BitConverter.GetBytes(16), 0, 4);

            // Audio format (floating point (3) or PCM (1)). Any other format indicates compression.
            stream.Write(BitConverter.GetBytes((ushort)(isFloatingPoint ? 3 : 1)), 0, 2);

            // Channels.
            stream.Write(BitConverter.GetBytes(channelCount), 0, 2);

            // Sample rate.
            stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);

            // Average bytes per second
            stream.Write(BitConverter.GetBytes(sampleRate * channelCount * (bitDepth / 8)), 0, 4);

            // Block align.
            stream.Write(BitConverter.GetBytes((ushort)channelCount * (bitDepth / 8)), 0, 2);

            // Bits per sample.
            stream.Write(BitConverter.GetBytes(bitDepth), 0, 2);


            // Sub-chunk 2.
            // Sub-chunk 2 ID.
            stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);

            // Sub-chunk 2 size.
            stream.Write(BitConverter.GetBytes((bitDepth / 8) * totalSampleCount), 0, 4);
        }

        // Read from the remaining bytes from IWaveSource and send them to the HttpResponse object
        private async Task WriteRemainingDataToResponseBody(HttpResponse response)
        {
            byte[] buffer = new byte[BufferSize];
            long totalToSend = WaveSource.Length - WaveSource.Position;
            int count = 0;

            long bytesRemaining = totalToSend + 1;

            while (bytesRemaining > 0)
            {
                try
                {
                    if (bytesRemaining <= buffer.Length)
                        count = WaveSource.Read(buffer, 0, (int)bytesRemaining);
                    else
                        count = WaveSource.Read(buffer, 0, buffer.Length);

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
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            await WriteStreamAsync(context.HttpContext.Response);
        }
    }
}