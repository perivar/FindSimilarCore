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

            return this.Ok();
        }
    }
}