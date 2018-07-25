using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CSCore;
using CSCore.Codecs.WAV;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FindSimilarClient
{
    public class IWaveSourceStreamResult : FileStreamResult
    {
        // default buffer size as defined in BufferedStream type
        private const int BufferSize = 0x1000;
        private string MultipartBoundary = "<qwe123>";
        private const string CrLf = "\r\n";
        private bool doSendWaveHeaders = true;
        private IWaveSource WaveSource { get; set; }

        public IWaveSourceStreamResult(IWaveSource waveSource, string contentType)
            : base(new MemoryStream(), contentType)
        {
            WaveSource = waveSource;
        }

        public IWaveSourceStreamResult(IWaveSource waveSource, MediaTypeHeaderValue contentType)
            : base(new MemoryStream(), contentType)
        {
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

            var length = WaveSource.Length;

            var range = response.HttpContext.GetRanges(length);

            if (IsMultipartRequest(range))
            {
                response.ContentType = $"multipart/byteranges; boundary={MultipartBoundary}";
            }
            else
            {
                response.ContentType = ContentType.ToString();
            }

            response.Headers.Add("Accept-Ranges", "bytes");

            // check https://github.com/aspnet/Mvc/blob/a67d9363e22be8ef63a1a62539991e1da3a6e30e/src/Microsoft.AspNetCore.Mvc.Core/Infrastructure/FileResultExecutorBase.cs
            if (IsRangeRequest(range))
            {
                response.StatusCode = (int)HttpStatusCode.PartialContent;

                if (!IsMultipartRequest(range))
                {
                    // check https://github.com/dotnet/corefx/blob/master/src/System.Net.Http/src/System/Net/Http/Headers/ContentRangeHeaderValue.cs
                    // 14.16 Content-Range - A server sending a response with status code 416 (Requested range not satisfiable)
                    // SHOULD include a Content-Range field with a byte-range-resp-spec of "*". The instance-length specifies
                    // the current length of the selected resource.  e.g. */length
                    response.Headers.Add("Content-Range", $"bytes {range.Ranges.First().From}-{range.Ranges.First().To}/{length}");
                }

                foreach (var rangeValue in range.Ranges)
                {
                    if (IsMultipartRequest(range)) // dunno if multipart works
                    {
                        await response.WriteAsync($"--{MultipartBoundary}");
                        await response.WriteAsync(CrLf);
                        await response.WriteAsync($"Content-type: {ContentType}");
                        await response.WriteAsync(CrLf);
                        await response.WriteAsync($"Content-Range: bytes {range.Ranges.First().From}-{range.Ranges.First().To}/{length}");
                        await response.WriteAsync(CrLf);
                    }

                    if (doSendWaveHeaders)
                    {
                        await WriteWaveHeadersToResponseBody(response);
                        doSendWaveHeaders = false;
                    }

                    await WriteDataToResponseBody(rangeValue, response);

                    if (IsMultipartRequest(range))
                    {
                        await response.WriteAsync(CrLf);
                    }
                }

                if (IsMultipartRequest(range))
                {
                    await response.WriteAsync($"--{MultipartBoundary}--");
                    await response.WriteAsync(CrLf);
                }
            }
            else
            {
                // write until end
                await WriteDataToResponseBody(response.Body);
            }
        }

        private async Task WriteDataToResponseBody(Stream responseBody)
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

                    await responseBody.WriteAsync(buffer, 0, count);

                    bytesRemaining -= count;
                }
                catch (IndexOutOfRangeException)
                {
                    await responseBody.FlushAsync();
                    return;
                }
                finally
                {
                    await responseBody.FlushAsync();
                }
            }
        }

        private async Task WriteDataToResponseBody(RangeItemHeaderValue rangeValue, HttpResponse response)
        {
            var startIndex = rangeValue.From ?? 0;
            var endIndex = rangeValue.To ?? 0;

            byte[] buffer = new byte[BufferSize];
            long totalToSend = endIndex - startIndex;
            int count = 0;

            long bytesRemaining = totalToSend + 1;
            response.ContentLength = bytesRemaining;

            // WaveSource.Seek(startIndex, SeekOrigin.Begin);
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

        private async Task WriteWaveHeadersToResponseBody(HttpResponse response)
        {
            try
            {
                MemoryStream ms = new MemoryStream();
                using (WaveWriter waveWriter = new WaveWriter(ms, WaveSource.WaveFormat))
                {
                }
                ms.Seek(0, SeekOrigin.Begin);

                var buffer = ms.ToArray();

                await response.Body.WriteAsync(buffer, 0, buffer.Length);

            }
            catch (Exception e)
            {
                await response.Body.FlushAsync();
                return;
            }
            finally
            {
                await response.Body.FlushAsync();
            }
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            await WriteStreamAsync(context.HttpContext.Response);
        }
    }
}