using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using MimeMapping;
using CommonUtils;
using Serilog;

namespace FindSimilarClient
{
    public class MultipartFileSender : FileStreamResult
    {
        private const int DEFAULT_BUFFER_SIZE = 64 * 1024; // copied buffer size from FileResultExecutorBase.cs
        private const long DEFAULT_EXPIRE_TIME_SECONDS = 604800L; // ..seconds = 1 week.
        private const string MULTIPART_BOUNDARY = "MULTIPART_BYTERANGES";
        private const string CrLf = "\r\n";

        private static Regex RangeRegex = new Regex(@"^bytes=\d*-\d*(,\d*-\d*)*$", RegexOptions.Compiled);
        private string filePath;

        private MultipartFileSender(Stream fileStream, string contentType)
            : base(fileStream, contentType)
        {
        }

        private MultipartFileSender(Stream fileStream, MediaTypeHeaderValue contentType)
            : base(fileStream, contentType)
        {
        }

        public static MultipartFileSender FromFile(FileInfo file)
        {
            if (file == null)
            {
                throw new ArgumentNullException("File cannot be null.");
            }

            string filePath = file.FullName;
            return MultipartFileSender.FromFile(filePath);
        }

        public static MultipartFileSender FromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(filePath);
            }

            string contentType = MimeMapping.MimeUtility.GetMimeMapping(filePath);

            // File.OpenRead(filePath) is the same as new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
            return new MultipartFileSender(File.OpenRead(filePath), contentType).SetFilePath(filePath);
        }

        public static MultipartFileSender FromStream(Stream stream, MediaTypeHeaderValue contentType)
        {
            return new MultipartFileSender(stream, contentType);
        }

        public static MultipartFileSender FromStream(Stream stream, string contentType)
        {
            return new MultipartFileSender(stream, contentType);
        }

        private MultipartFileSender SetFilePath(string filepath)
        {
            this.filePath = filepath;
            return this;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            await ServeResource(context.HttpContext.Response);
        }

        public async Task ServeResource(HttpResponse response)
        {
            if (response == null)
            {
                return;
            }

            // Read all the file properties needed ---------------------------------------------------
            // the file-name and last modified date
            // and the content-type (mime mapping)

            if (!File.Exists(filePath))
            {
                Log.Error("FileInfo doesn't exist at URI : {0}", filePath);
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            long length = new FileInfo(filePath).Length;
            string fileName = StringUtils.RemoveNonAsciiCharactersFast(Path.GetFileName(filePath));
            DateTime lastModifiedDateTime = File.GetLastWriteTimeUtc(filePath);

            if (string.IsNullOrEmpty(fileName) || lastModifiedDateTime == null)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
            }

            // convert the datetime to date time offset 
            DateTimeOffset lastModifiedDateTimeOffset = DateTime.SpecifyKind(lastModifiedDateTime, DateTimeKind.Utc);

            // Since the 'Last-Modified' and other similar http date headers are rounded down to whole seconds, 
            // round down current file's last modified to whole seconds for correct comparison. 
            lastModifiedDateTimeOffset = RoundDownToWholeSeconds(lastModifiedDateTimeOffset);

            // compare date times using milliseconds since UNIX epoch (January 1, 1970 00:00:00 UTC)
            long lastModified = lastModifiedDateTimeOffset.ToUnixTimeMilliseconds();

            // read in stored Content-Type
            string contentType = ContentType;

            // Validate request headers for caching ---------------------------------------------------

            // If-None-Match header should contain "*" or ETag. If so, then return 304.
            string ifNoneMatch = response.HttpContext.Request.Headers[HeaderNames.IfNoneMatch];
            if (ifNoneMatch != null && HttpUtils.Matches(ifNoneMatch, fileName))
            {
                response.Headers.Add(HeaderNames.ETag, fileName); // Required in 304.
                response.StatusCode = (int)HttpStatusCode.NotModified;
                return;
            }

            // If-Modified-Since header should be greater than LastModified. If so, then return 304.
            // This header is ignored if any If-None-Match header is specified.
            long ifModifiedSince = GetDateHeader(response, HeaderNames.IfModifiedSince);
            if (ifNoneMatch == null && ifModifiedSince != -1 && ifModifiedSince + 1000 > lastModified)
            {
                response.Headers.Add(HeaderNames.ETag, fileName); // Required in 304.
                response.StatusCode = (int)HttpStatusCode.NotModified;
                return;
            }

            // Validate request headers for resume ----------------------------------------------------

            // If-Match header should contain "*" or ETag. If not, then return 412.
            string ifMatch = response.HttpContext.Request.Headers[HeaderNames.IfMatch];
            if (ifMatch != null && !HttpUtils.Matches(ifMatch, fileName))
            {
                response.StatusCode = (int)HttpStatusCode.PreconditionFailed;
                return;
            }

            // If-Unmodified-Since header should be greater than LastModified. If not, then return 412.
            long ifUnmodifiedSince = GetDateHeader(response, HeaderNames.IfUnmodifiedSince);
            if (ifUnmodifiedSince != -1 && ifUnmodifiedSince + 1000 <= lastModified)
            {
                response.StatusCode = (int)HttpStatusCode.PreconditionFailed;
                return;
            }

            // Validate and process range -------------------------------------------------------------

            // Prepare some variables. The full Range represents the complete file.
            Range full = new Range(0, length - 1, length);
            List<Range> ranges = new List<Range>();

            // Validate and process Range and If-Range headers.
            string range = response.HttpContext.Request.Headers["Range"];
            if (range != null)
            {
                // Range header should match format "bytes=n-n,n-n,n-n...". If not, then return 416.
                if (!RangeRegex.IsMatch(range))
                {
                    response.Headers.Add(HeaderNames.ContentRange, $"bytes */{length}"); // Required in 416.
                    response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    return;
                }

                string ifRange = response.HttpContext.Request.Headers[HeaderNames.IfRange];
                if (ifRange != null && !ifRange.Equals(fileName))
                {
                    long ifRangeTime = GetDateHeader(response, HeaderNames.IfRange);
                    if (ifRangeTime != -1)
                    {
                        ranges.Add(full);
                    }
                }

                // If any valid If-Range header, then process each part of byte range.
                if (ranges.Count == 0)
                {
                    // Remove "Ranges" and break up the ranges
                    string[] rangeArray = range.Replace("bytes=", string.Empty)
                                                 .Split(",".ToCharArray());

                    foreach (string part in rangeArray)
                    {
                        // Assuming a file with length of 100, the following examples returns bytes at:
                        // 50-80 (50 to 80), 40- (40 to length=100), -20 (length-20=80 to length=100).
                        long start = Range.SubstringLong(part, 0, part.IndexOf("-"));
                        long end = Range.SubstringLong(part, part.IndexOf("-") + 1, part.Length);

                        if (start == -1)
                        {
                            start = length - end;
                            end = length - 1;
                        }
                        else if (end == -1 || end > length - 1)
                        {
                            end = length - 1;
                        }

                        // Check if Range is syntactically valid. If not, then return 416.
                        if (start > end)
                        {
                            // check https://github.com/dotnet/corefx/blob/master/src/System.Net.Http/src/System/Net/Http/Headers/ContentRangeHeaderValue.cs
                            // 14.16 Content-Range - A server sending a response with status code 416 (Requested range not satisfiable)
                            // SHOULD include a Content-Range field with a byte-range-resp-spec of "*". The instance-length specifies
                            // the current length of the selected resource.  e.g. */length
                            response.Headers.Add(HeaderNames.ContentRange, $"bytes */{length}"); // Required in 416.
                            response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                            return;
                        }

                        // Add range.                    
                        ranges.Add(new Range(start, end, length));
                    }
                }
            }

            // Prepare and Initialize response --------------------------------------------------------

            // disable response buffering
            var bufferingFeature = response.HttpContext.Features.Get<IHttpBufferingFeature>();
            bufferingFeature?.DisableResponseBuffering();

            // Get content type by file name and Set content disposition.
            string disposition = "inline";

            // If content type is unknown, then Set the default value.
            // For all content types, see: http://www.w3schools.com/media/media_mimeref.asp
            // To add new content types, add new mime-mapping entry in web.xml.
            if (contentType == null)
            {
                contentType = "application/octet-stream";
            }
            else if (!contentType.StartsWith("image"))
            {
                // Else, expect for images, determine content disposition. If content type is supported by
                // the browser, then Set to inline, else attachment which will pop a 'save as' dialogue.
                string accept = response.HttpContext.Request.Headers[HeaderNames.Accept];
                disposition = accept != null && HttpUtils.Accepts(accept, contentType) ? "inline" : "attachment";
            }

            // Initialize response.
            try
            {
                response.Headers.Add(HeaderNames.ContentType, contentType);
                response.Headers.Add(HeaderNames.ContentDisposition, disposition + $";filename=\"{fileName}\"");

                Log.Debug($"{HeaderNames.ContentType} : {contentType}");
                Log.Debug($"{HeaderNames.ContentDisposition} : {disposition}");

                response.Headers.Add(HeaderNames.AcceptRanges, "bytes");

                // Check SetLastModifiedAndEtagHeaders() in FileResultExecutorBase.cs for info about adding headers
                response.Headers.Add(HeaderNames.ETag, fileName);
                SetDateHeader(response, HeaderNames.LastModified, lastModifiedDateTimeOffset);

                // set expiration header (remove milliseconds)
                var expires = DateTimeOffset
                                .UtcNow
                                .AddSeconds(DEFAULT_EXPIRE_TIME_SECONDS);

                SetDateHeader(response, HeaderNames.Expires, expires);
            }
            catch (System.Exception e)
            {
                Log.Error("Failed adding response headers: {0}", e.Message);
            }

            // Send requested file (part(s)) to client ------------------------------------------------

            // Prepare streams.
            Stream input = FileStream;
            Stream output = response.Body;

            if (ranges.Count == 0 || ranges[0] == full)
            {
                // Return full file.
                Log.Information("Return full file : from ({0}) to ({1}) of ({2})", full.Start, full.End, full.Total);
                response.ContentType = contentType;

                response.Headers.Add(HeaderNames.ContentRange, $"bytes {full.Start}-{full.End}/{full.Total}");
                response.Headers.Add(HeaderNames.ContentLength, full.Length.ToString());

                await Range.CopyStream(input, output, length, full.Start, full.Length);
            }
            else if (ranges.Count == 1)
            {
                // Return single part of file.
                Range r = ranges[0];

                Log.Information("Return 1 part of file : from ({0}) to ({1}) of ({2})", r.Start, r.End, r.Total);
                response.ContentType = contentType;

                response.Headers.Add(HeaderNames.ContentRange, $"bytes {r.Start}-{r.End}/{r.Total}");
                response.Headers.Add(HeaderNames.ContentLength, r.Length.ToString());
                response.StatusCode = (int)HttpStatusCode.PartialContent; // 206

                // Copy single part range.
                await Range.CopyStream(input, output, length, r.Start, r.Length);
            }
            else
            {
                // Return multiple parts of file.
                // check https://stackoverflow.com/questions/38069730/how-to-create-a-multipart-http-response-with-asp-net-core
                // and https://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html
                response.ContentType = $"multipart/byteranges; boundary={MULTIPART_BOUNDARY}";
                response.StatusCode = (int)HttpStatusCode.PartialContent; // 206

                // Copy multi part range.
                foreach (Range r in ranges)
                {
                    Log.Information("Return multi part of file : from ({0}) to ({1}) of ({2})", r.Start, r.End, r.Total);

                    // Add multipart boundary and header fields for every range.
                    await response.WriteAsync(CrLf);
                    await response.WriteAsync($"--{MULTIPART_BOUNDARY}");
                    await response.WriteAsync(CrLf);
                    await response.WriteAsync($"{HeaderNames.ContentType}: {contentType}");
                    await response.WriteAsync(CrLf);
                    await response.WriteAsync($"{HeaderNames.ContentRange}: bytes {r.Start}-{r.End}/{r.Total}");
                    await response.WriteAsync(CrLf);

                    // Copy single part range of multi part range.
                    await Range.CopyStream(input, output, length, r.Start, r.Length);
                }

                // End with multipart boundary.
                await response.WriteAsync(CrLf);
                await response.WriteAsync($"--{MULTIPART_BOUNDARY}");
                await response.WriteAsync(CrLf);
            }
        }

        private static DateTimeOffset RoundDownToWholeSeconds(DateTimeOffset dateTimeOffset)
        {
            var ticksToRemove = dateTimeOffset.Ticks % TimeSpan.TicksPerSecond;
            return dateTimeOffset.Subtract(TimeSpan.FromTicks(ticksToRemove));
        }

        private static long GetDateHeader(HttpResponse response, string header)
        {
            var headerValue = response.HttpContext.Request.Headers[header].ToString();

            if (string.IsNullOrEmpty(headerValue)) return -1;

            DateTimeOffset parsedDateOffset;
            DateTimeOffset.TryParseExact(
                                headerValue,
                                "r",
                                CultureInfo.InvariantCulture.DateTimeFormat,
                                DateTimeStyles.AdjustToUniversal,
                                out parsedDateOffset);

            return parsedDateOffset.ToUnixTimeMilliseconds();
        }

        private static void SetDateHeader(HttpResponse response, string header, DateTimeOffset date)
        {
            // The RFC1123 ("R", "r") Format Specifier
            // The "R" or "r" standard format specifier represents a custom date and time format string
            //  that is defined by the DateTimeFormatInfo.RFC1123Pattern property. 
            // The pattern reflects a defined standard, and the property is read-only. 
            // Therefore, it is always the same, regardless of the culture used or the format provider supplied. 
            // The custom format string is "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'". 
            // When this standard format specifier is used, the formatting or parsing operation 
            // always uses the invariant culture.
            response.Headers.Add(header, date.ToString("r"));
        }

        private class Range
        {
            public long Start;
            public long End;
            public long Length;
            public long Total;

            /// <summary>
            /// Construct a byte range.
            /// </summary>
            /// <param name="start">Start of the byte range.</param>
            /// <param name="end">End of the byte range.</param>
            /// <param name="total">Total length of the byte source.</param>
            public Range(long start, long end, long total)
            {
                this.Start = start;
                this.End = end;
                this.Length = end - start + 1;
                this.Total = total;
            }

            public static long SubstringLong(string value, int beginIndex, int endIndex)
            {
                // simulates Java substring function
                int len = endIndex - beginIndex;
                string substring = value.Substring(beginIndex, len);
                return (substring.Length > 0) ? long.Parse(substring) : -1;
            }

            public static async Task CopyStream(Stream input, Stream output, long inputSize, long start, long length)
            {
                byte[] buffer = new byte[DEFAULT_BUFFER_SIZE];
                int bytesRead;

                if (inputSize == length)
                {
                    try
                    {
                        // Write full range.
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, bytesRead);
                            await output.FlushAsync();
                        }

                        // this doesn't seem to work?
                        // await input.CopyToAsync(output, DEFAULT_BUFFER_SIZE);
                    }
                    catch (System.Exception e)
                    {
                        Log.Error(e.Message);
                    }
                }
                else
                {
                    input.Seek(start, SeekOrigin.Begin);
                    long toRead = length;
                    try
                    {
                        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if ((toRead -= bytesRead) > 0)
                            {
                                await output.WriteAsync(buffer, 0, bytesRead);
                                await output.FlushAsync();
                            }
                            else
                            {
                                await output.WriteAsync(buffer, 0, (int)toRead + bytesRead);
                                await output.FlushAsync();
                                break;
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Log.Error(e.Message);
                    }
                }
            }
        }
        private static class HttpUtils
        {
            private static Regex AcceptHeaderRegex = new Regex(@"\s*(,|;)\s*", RegexOptions.Compiled);
            private static Regex MatchHeaderRegex = new Regex(@"\s*,\s*", RegexOptions.Compiled);


            /// <summary>
            /// Returns true if the given accept header accepts the given value.
            /// </summary>
            /// <param name="acceptHeader">The accept header.</param>
            /// <param name="toAccept">The value to be accepted.</param>
            /// <returns>True if the given accept header accepts the given value.</returns>
            public static bool Accepts(string acceptHeader, string toAccept)
            {
                string[] acceptValues = AcceptHeaderRegex.Split(acceptHeader);

                Array.Sort(acceptValues);

                Regex rgx = new Regex(@"/.*$");
                return Array.BinarySearch(acceptValues, toAccept) > -1
                        || Array.BinarySearch(acceptValues, rgx.Replace(toAccept, "/*")) > -1
                        || Array.BinarySearch(acceptValues, "*/*") > -1;
            }

            /// <summary>
            /// Returns true if the given match header matches the given value.
            /// </summary>
            /// <param name="matchHeader">The match header.</param>
            /// <param name="toMatch">The value to be matched.</param>
            /// <returns>True if the given match header matches the given value.</returns>
            public static bool Matches(string matchHeader, string toMatch)
            {
                string[] matchValues = MatchHeaderRegex.Split(matchHeader);
                Array.Sort(matchValues);

                return Array.BinarySearch(matchValues, toMatch) > -1
                        || Array.BinarySearch(matchValues, "*") > -1;
            }
        }
    }
}
