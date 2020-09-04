using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FindSimilarClient
{
    public class RequestResponseLoggingMiddleware
    {
        const int MAX_BYTES_TO_READ = 50;

        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate _next,
                                                ILoggerFactory loggerFactory)
        {
            this._next = _next;
            this._logger = loggerFactory
                      .CreateLogger<RequestResponseLoggingMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            _logger.LogDebug(await FormatRequest(context.Request));

            // Copy a pointer to the original response body stream
            var originalBodyStream = context.Response.Body;

            // Create a new memory stream...
            using (var responseBody = new MemoryStream())
            {
                // ...and use that for the temporary response body
                context.Response.Body = responseBody;

                // Continue down the Middleware pipeline, eventually returning to this class
                await _next(context);

                // Format the response from the server
                _logger.LogDebug(await FormatResponse(context.Response));

                // Changing the response body is not allowed on a 204 ?!
                // if (context.Response.StatusCode != 204)
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private async Task<string> FormatRequest(HttpRequest request)
        {
            // This line allows us to set the reader for the request back at the beginning of its stream.
            request.EnableBuffering();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-------HTTP REQUEST INFORMATION-------");
            sb.AppendLine($"{request.Scheme} {request.Host}{request.Path} {request.QueryString}");

            sb.AppendLine("Headers:");
            foreach (var key in request.Headers.Keys)
            {
                sb.AppendLine($"{key}={request.Headers[key]}");
            }

            // We now need to read the request stream. 
            // First, we create a new byte[] with the same length as the request stream...
            // interestingly enough I first didnt get the tutorials solution working. 
            // I had the POST problems like a lot of others. Then stumbled upon your comment. 
            // I already had your proposal
            //    var buffer = new byte[Convert.ToInt32(request.Body.Length)];
            // implemented, but it didnt work for me. Instead the “old” way worked for POST requests
            //    var buffer = new byte[Convert.ToInt32(request.ContentLength)];

            // var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            int length = Math.Min(Convert.ToInt32(request.ContentLength), MAX_BYTES_TO_READ);
            var buffer = new byte[length];

            // ... copy the request stream into the new buffer.
            await request.Body.ReadAsync(buffer, 0, buffer.Length);

            // we convert the byte[] into a string using UTF8 encoding...
            var bodyAsText = Encoding.UTF8.GetString(buffer);

            // we need to reset the reader for the request so that we can read it later.
            // i.e. request.Body.Position = 0;
            // request.Body.Seek(0, SeekOrigin.Begin);
            request.Body.Position = 0;

            if (!string.IsNullOrEmpty(bodyAsText))
            {
                sb.AppendLine("-------HTTP REQUEST BODY -------");
                sb.AppendLine($"{bodyAsText}");
            }

            return sb.ToString();
        }

        private async Task<string> FormatResponse(HttpResponse response)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-------HTTP RESPONSE INFORMATION-------");
            sb.AppendLine($"StatusCode: {response.StatusCode}");

            sb.AppendLine("Headers:");
            foreach (var key in response.Headers.Keys)
            {
                sb.AppendLine($"{key}={response.Headers[key]}");
            }

            // we need to read the response stream from the beginning...
            response.Body.Seek(0, SeekOrigin.Begin);

            // ...and copy it
            byte[] buffer = new byte[MAX_BYTES_TO_READ];
            int bytesRead;
            using (var memStream = new MemoryStream())
            {
                // await response.Body.CopyToAsync(memStream);

                // only read the first bytes
                if ((bytesRead = await response.Body.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await memStream.WriteAsync(buffer, 0, bytesRead);
                    await memStream.FlushAsync();
                }

                buffer = memStream.ToArray();
            }

            // var bodyAsText = await new StreamReader(response.Body).ReadToEndAsync();
            // dump the first bytes as a hex editor output
            // see http://illegalargumentexception.blogspot.com/2008/04/c-file-hex-dump-application.html

            var bodyAsText = (buffer.Length > 0) ? StringUtils.ToHexAndAsciiString(buffer, false) : null;

            // get the body byte length
            long byteLength = response.Body.Length;

            // we need to reset the reader for the response so that the client can read it.
            response.Body.Seek(0, SeekOrigin.Begin);

            if (!string.IsNullOrEmpty(bodyAsText))
            {
                sb.AppendLine("-------HTTP RESPONSE BODY -------");
                sb.AppendLine($"Showing first {MAX_BYTES_TO_READ} of total {byteLength} bytes.");
                sb.AppendLine($"{bodyAsText}");
            }

            return sb.ToString();
        }
    }
}