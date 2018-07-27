using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Serilog;
using MimeMapping;
using CommonUtils;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;
using System.Threading.Tasks;

namespace FindSimilarClient
{
    public class MultipartFileSender
    {
        private static int DEFAULT_BUFFER_SIZE = 20480; // ..bytes = 20KB.
        private static long DEFAULT_EXPIRE_TIME = 604800000L; // ..ms = 1 week.
        private static string MULTIPART_BOUNDARY = "MULTIPART_BYTERANGES";
        private const string CrLf = "\r\n";

        string filePath;
        HttpRequest request;
        HttpResponse response;

        public MultipartFileSender()
        {
        }
        public static MultipartFileSender FromFile(FileInfo file)
        {
            return new MultipartFileSender().SetFilePath(file.FullName);
        }

        public static MultipartFileSender FromFile(string filePath)
        {
            return new MultipartFileSender().SetFilePath(filePath);
        }

        private MultipartFileSender SetFilePath(string filepath)
        {
            this.filePath = filepath;
            return this;
        }

        public MultipartFileSender With(HttpRequest httpRequest)
        {
            request = httpRequest;
            return this;
        }

        public MultipartFileSender With(HttpResponse httpResponse)
        {
            response = httpResponse;
            return this;
        }

        public async Task ServeResource()
        {
            if (response == null || request == null)
            {
                return;
            }

            if (!File.Exists(filePath))
            {
                Log.Error("FileInfo doesn't exist at URI : {}", filePath);
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            long length = new FileInfo(filePath).Length;
            string fileName = Path.GetFileName(filePath);
            DateTime lastModifiedObj = File.GetLastWriteTime(filePath);

            if (string.IsNullOrEmpty(fileName) || lastModifiedObj == null)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
            }

            long lastModified = (long)DateUtils.ConvertToUnixTimestamp(lastModifiedObj);
            string contentType = MimeMapping.MimeUtility.GetMimeMapping(filePath);

            // Validate request headers for caching ---------------------------------------------------

            // If-None-Match header should contain "*" or ETag. If so, then return 304.
            string ifNoneMatch = response.HttpContext.Request.Headers["If-None-Match"];
            if (ifNoneMatch != null && HttpUtils.Matches(ifNoneMatch, fileName))
            {
                response.Headers.Add("ETag", fileName); // Required in 304.
                response.StatusCode = (int)HttpStatusCode.NotModified;
                return;
            }

            // If-Modified-Since header should be greater than LastModified. If so, then return 304.
            // This header is ignored if any If-None-Match header is specified.
            long ifModifiedSince = 0;
            long.TryParse(response.HttpContext.Request.Headers["If-Modified-Since"].ToString(), out ifModifiedSince);
            if (ifNoneMatch == null && ifModifiedSince != -1 && ifModifiedSince + 1000 > lastModified)
            {
                response.Headers.Add("ETag", fileName); // Required in 304.
                response.StatusCode = (int)HttpStatusCode.NotModified;
                return;
            }

            // Validate request headers for resume ----------------------------------------------------

            // If-Match header should contain "*" or ETag. If not, then return 412.
            string ifMatch = response.HttpContext.Request.Headers["If-Match"];
            if (ifMatch != null && !HttpUtils.Matches(ifMatch, fileName))
            {
                response.StatusCode = (int)HttpStatusCode.PreconditionFailed;
                return;
            }

            // If-Unmodified-Since header should be greater than LastModified. If not, then return 412.
            long ifUnmodifiedSince = 0;
            long.TryParse(response.HttpContext.Request.Headers["If-Unmodified-Since"].ToString(), out ifUnmodifiedSince);
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
            Regex rangeRegex = new Regex(@"^bytes=\\d*-\\d*(,\\d*-\\d*)*$");
            string range = response.HttpContext.Request.Headers["Range"];
            if (range != null)
            {
                // Range header should match format "bytes=n-n,n-n,n-n...". If not, then return 416.
                if (!rangeRegex.IsMatch(range))
                {
                    response.Headers.Add("Content-Range", "bytes */" + length); // Required in 416.
                    response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                    return;
                }

                string ifRange = response.HttpContext.Request.Headers["If-Range"];
                if (ifRange != null && !ifRange.Equals(fileName))
                {
                    try
                    {
                        long ifRangeTime = 0;
                        long.TryParse(response.HttpContext.Request.Headers["If-Range"].ToString(), out ifRangeTime);

                        // Throws IAE if invalid.
                        if (ifRangeTime != -1)
                        {
                            ranges.Add(full);
                        }
                    }
                    catch (ArgumentException)
                    {
                        ranges.Add(full);
                    }
                }

                // If any valid If-Range header, then process each part of byte range.
                if (ranges.Count == 0)
                {
                    foreach (string part in range.Substring(6).Split(","))
                    {
                        // Assuming a file with length of 100, the following examples returns bytes at:
                        // 50-80 (50 to 80), 40- (40 to length=100), -20 (length-20=80 to length=100).
                        long start = Range.SubLong(part, 0, part.IndexOf("-"));
                        long end = Range.SubLong(part, part.IndexOf("-") + 1, part.Length);

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
                            response.Headers.Add("Content-Range", "bytes */" + length); // Required in 416.
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
                string accept = response.HttpContext.Request.Headers["Accept"];
                disposition = accept != null && HttpUtils.Accepts(accept, contentType) ? "inline" : "attachment";
            }

            Log.Debug("Content-Type : {}", contentType);

            // Initialize response.
            // response.Reset();
            // response.SetBufferSize(DEFAULT_BUFFER_SIZE);
            response.Headers.Add("Content-Type", contentType);
            response.Headers.Add("Content-Disposition", disposition + ";filename=\"" + fileName + "\"");

            Log.Debug("Content-Disposition : {}", disposition);
            response.Headers.Add("Accept-Ranges", "bytes");
            response.Headers.Add("ETag", fileName);

            response.Headers.Add("Last-Modified", lastModified.ToString());
            response.Headers.Add("Expires", (DateTime.UtcNow.Millisecond + DEFAULT_EXPIRE_TIME).ToString());

            // Send requested file (part(s)) to client ------------------------------------------------

            // Prepare streams.

            Stream input = new BufferedStream(File.OpenRead(filePath));
            Stream output = response.Body;

            if (ranges.Count == 0 || ranges[0] == full)
            {
                // Return full file.
                Log.Information("Return full file");
                response.ContentType = contentType;
                response.Headers.Add("Content-Range", "bytes " + full.Start + "-" + full.End + "/" + full.Total);
                response.Headers.Add("Content-Length", full.Length.ToString());

                Range.Copy(input, output, length, full.Start, full.Length);
            }
            else if (ranges.Count == 1)
            {
                // Return single part of file.
                Range r = ranges[0];
                Log.Information("Return 1 part of file : from ({}) to ({})", r.Start, r.End);
                response.ContentType = contentType;
                response.Headers.Add("Content-Range", "bytes " + r.Start + "-" + r.End + "/" + r.Total);
                response.Headers.Add("Content-Length", r.Length.ToString());
                response.StatusCode = (int)HttpStatusCode.PartialContent; // 206

                // Copy single part range.
                Range.Copy(input, output, length, r.Start, r.Length);
            }
            else
            {
                // Return multiple parts of file.
                response.ContentType = "multipart/byteranges; boundary=" + MULTIPART_BOUNDARY;
                response.StatusCode = (int)HttpStatusCode.PartialContent; // 206

                // Copy multi part range.
                foreach (Range r in ranges)
                {
                    Log.Information("Return multi part of file : from ({}) to ({})", r.Start, r.End);

                    // Add multipart boundary and header fields for every range.
                    await response.WriteAsync(CrLf);
                    await response.WriteAsync("--" + MULTIPART_BOUNDARY);
                    await response.WriteAsync(CrLf);
                    await response.WriteAsync("Content-Type: " + contentType);
                    await response.WriteAsync(CrLf);
                    await response.WriteAsync("Content-Range: bytes " + r.Start + "-" + r.End + "/" + r.Total);
                    await response.WriteAsync(CrLf);

                    // Copy single part range of multi part range.
                    Range.Copy(input, output, length, r.Start, r.Length);
                }

                // End with multipart boundary.
                await response.WriteAsync(CrLf);
                await response.WriteAsync("--" + MULTIPART_BOUNDARY + "--");
                await response.WriteAsync(CrLf);
            }
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

            public static long SubLong(string value, int beginIndex, int endIndex)
            {
                string substring = value.Substring(beginIndex, endIndex);
                return (substring.Length > 0) ? long.Parse(substring) : -1;
            }

            public static async Task Copy(Stream input, Stream output, long inputSize, long start, long length)
            {
                byte[] buffer = new byte[DEFAULT_BUFFER_SIZE];
                int read;

                if (inputSize == length)
                {
                    // Write full range.
                    while ((read = input.Read(buffer)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read);
                        await output.FlushAsync();
                    }
                }
                else
                {
                    input.Seek(start, SeekOrigin.Begin);
                    long toRead = length;

                    while ((read = input.Read(buffer)) > 0)
                    {
                        if ((toRead -= read) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read);
                            await output.FlushAsync();
                        }
                        else
                        {
                            await output.WriteAsync(buffer, 0, (int)toRead + read);
                            await output.FlushAsync();
                            break;
                        }
                    }
                }
            }
        }
        private static class HttpUtils
        {
            /// <summary>
            /// Returns true if the given accept header accepts the given value.
            /// </summary>
            /// <param name="acceptHeader">The accept header.</param>
            /// <param name="toAccept">The value to be accepted.</param>
            /// <returns>True if the given accept header accepts the given value.</returns>
            public static bool Accepts(string acceptHeader, string toAccept)
            {
                string[] acceptValues = Regex.Split(acceptHeader, "\\s*(,|;)\\s*");
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
                string[] matchValues = Regex.Split(matchHeader, "\\s*,\\s*");
                Array.Sort(matchValues);

                return Array.BinarySearch(matchValues, toMatch) > -1
                        || Array.BinarySearch(matchValues, "*") > -1;
            }
        }
    }
}
