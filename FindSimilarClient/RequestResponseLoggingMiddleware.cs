using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;

namespace FindSimilarClient
{
    public class RequestResponseLoggingMiddleware
    {
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

            _logger.LogDebug(GetRequestInformation(context.Request));

            // Call the _next delegate/middleware in the pipeline
            await _next(context);

            _logger.LogDebug(GetResponseInformation(context.Response));


            /* 
                _logger.LogInformation(await FormatRequest(context.Request));

                var originalBodyStream = context.Response.Body;

                using (var responseBody = new MemoryStream())
                {
                    context.Response.Body = responseBody;

                    await _next(context);

                    _logger.LogInformation(await FormatResponse(context.Response));

                    //Because you change the response body which is not allowed on a 204.
                    if (context.Response.StatusCode != 204)
                        await responseBody.CopyToAsync(originalBodyStream);
                }
             */
        }

        private string GetRequestInformation(HttpRequest request)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-------HTTP REQUEST INFORMATION-------");
            sb.AppendLine($"{request.Scheme} {request.Host}{request.Path} {request.QueryString}");

            sb.AppendLine("Headers:");
            foreach (var key in request.Headers.Keys)
            {
                sb.AppendLine($"{key}={request.Headers[key]}");
            }

            return sb.ToString();
        }

        private string GetResponseInformation(HttpResponse response)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("-------HTTP RESPONSE INFORMATION-------");
            sb.AppendLine($"StatusCode: {response.StatusCode}");

            sb.AppendLine("Headers:");
            foreach (var key in response.Headers.Keys)
            {
                sb.AppendLine($"{key}={response.Headers[key]}");
            }

            return sb.ToString();
        }

        private async Task<string> FormatRequest(HttpRequest request)
        {
            var body = request.Body;
            request.EnableRewind();

            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            await request.Body.ReadAsync(buffer, 0, buffer.Length);
            var bodyAsText = Encoding.UTF8.GetString(buffer);
            request.Body.Position = 0;
            return $"{request.Scheme} {request.Host}{request.Path} {request.QueryString} {bodyAsText}";
        }

        private async Task<string> FormatResponse(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);

            return $"Response {text}";
        }
    }
}