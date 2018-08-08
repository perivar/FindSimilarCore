using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FindSimilarClient.Controllers
{
    [Produces("application/json")]
    [Route("api/files")]
    public class FilesController : Controller
    {
        private readonly IHostingEnvironment _hostingEnvironment;

        public FilesController(IHostingEnvironment hostingEnvironment)
        {
            this._hostingEnvironment = hostingEnvironment;
        }

        [HttpPost]
        [Route("upload")]
        public async Task<IActionResult> UploadFilesAsyncActionResult(List<IFormFile> files)
        {
            var filesPath = $"{this._hostingEnvironment.WebRootPath}/files";

            foreach (var file in files)
            {
                var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName;

                // Ensure the file name is correct
                fileName = fileName.Contains("\\")
                    ? fileName.Trim('"').Substring(fileName.LastIndexOf("\\", StringComparison.Ordinal) + 1)
                    : fileName.Trim('"');

                var fullFilePath = Path.Combine(filesPath, fileName);

                if (file.Length <= 0)
                {
                    continue;
                }

                using (var stream = new FileStream(fullFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
            }

            // if we are calling this methods from a jquery $.ajax call, specifying dataType: "json" 
            // and using the deferred execution method .done() and.fail(),  
            // we need to ensure this methods returns what the ajax call expect of dataType,
            // otherwise it will fail, even if this method returns OK().
            // So if the ajax code contains:
            // dataType: "json"
            // In this case jQuery:
            // Evaluates the response as JSON and returns a JavaScript object. […] 
            // The JSON data is parsed in a strict manner; any malformed JSON is rejected 
            // and a parse error is thrown. 
            // […] an empty response is also rejected; 
            // the server should return a response of null or {} instead.

            // therefore force the whole controller to always use json 
            // [Produces("application/json")]
            // and return some text
            return Ok("Success");
        }
    }
}