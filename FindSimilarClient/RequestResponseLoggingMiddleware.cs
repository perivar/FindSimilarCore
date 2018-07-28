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
        private readonly RequestDelegate next;
        private readonly ILogger logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next,
                                                ILoggerFactory loggerFactory)
        {
            this.next = next;
            this.logger = loggerFactory
                      .CreateLogger<RequestResponseLoggingMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {

            LogRequestHeaders(context.Request);

            await this.next.Invoke(context);

            LogResponseHeaders(context.Response);

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

        private void LogRequestHeaders(HttpRequest request)
        {
            if (Debugger.IsAttached)
            {
                string headers = String.Empty;
                foreach (var key in request.Headers.Keys)
                {
                    headers += key + "=" + request.Headers[key] + Environment.NewLine;
                }
                logger.LogInformation("----Request Headers----\n" + headers);
            }
        }

        private void LogResponseHeaders(HttpResponse response)
        {
            if (Debugger.IsAttached)
            {
                string headers = "StatusCode: " + response.StatusCode.ToString() + Environment.NewLine;
                foreach (var key in response.Headers.Keys)
                {
                    headers += key + "=" + response.Headers[key] + Environment.NewLine;
                }
                logger.LogInformation("----Response Headers----\n" + headers);
            }
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