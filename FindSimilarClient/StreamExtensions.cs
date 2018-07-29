using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace FindSimilarClient
{
    public static class StreamExtensions
    {
        private static ILogger _logger = ApplicationLogging.CreateLogger("StreamExtensions");

        /// <summary>
        /// Parse the Range header: bytes=x,y
        /// Usage: var range = response.HttpContext.GetRanges(lengthInBytes);
        /// </summary>
        /// <param name="context">HttpContext</param>
        /// <param name="contentSize">length in bytes</param>
        /// <returns>A rangeheadervalue containing the list of ranges found</returns>
        public static RangeHeaderValue GetRanges(this HttpContext context, long contentSize)
        {
            // see http://www.mintydog.com/2014/01/serving-video-from-sitecore-for-iphones/

            RangeHeaderValue rangesResult = null;

            string rangeHeader = context.Request.Headers["Range"];

            if (!string.IsNullOrEmpty(rangeHeader))
            {
                _logger.LogTrace("Parsing Range Header: {0}", rangeHeader);

                // rangeHeader contains the value of the Range HTTP Header and can have values like:
                //      Range: bytes=0-1            * Get bytes 0 and 1, inclusive
                //      Range: bytes=0-500          * Get bytes 0 to 500 (the first 501 bytes), inclusive
                //      Range: bytes=400-1000       * Get bytes 500 to 1000 (501 bytes in total), inclusive
                //      Range: bytes=-200           * Get the last 200 bytes
                //      Range: bytes=500-           * Get all bytes from byte 500 to the end
                //
                // Can also have multiple ranges delimited by commas, as in:
                //      Range: bytes=0-500,600-1000 * Get bytes 0-500 (the first 501 bytes), inclusive plus bytes 600-1000 (401 bytes) inclusive

                // Remove "Ranges" and break up the ranges
                string[] ranges = rangeHeader.Replace("bytes=", string.Empty)
                                             .Split(",".ToCharArray());

                rangesResult = new RangeHeaderValue();

                for (int i = 0; i < ranges.Length; i++)
                {
                    const int START = 0, END = 1;

                    long endByte, startByte;

                    long parsedValue;

                    // Get the START and END values for the current range
                    string[] currentRange = ranges[i].Split("-".ToCharArray());
                    if (long.TryParse(currentRange[END], out parsedValue))
                    {
                        // An end was specified
                        endByte = parsedValue;
                    }
                    else
                    {
                        // No end specified
                        endByte = contentSize - 1;
                    }


                    if (long.TryParse(currentRange[START], out parsedValue))
                    {
                        // A normal begin value
                        startByte = parsedValue;
                    }
                    else
                    {
                        // No beginning specified, get last n bytes of file
                        // We already parsed end, so subtract from total and
                        // make end the actual size of the file
                        startByte = contentSize - endByte;
                        endByte = contentSize - 1;
                    }

                    _logger.LogTrace("Found Byte Range: {0}-{1} / {2}", startByte, endByte, contentSize);

                    rangesResult.Ranges.Add(new RangeItemHeaderValue(startByte, endByte));
                }
            }

            return rangesResult;
        }
    }
}