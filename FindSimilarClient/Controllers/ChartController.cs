using System.Threading.Tasks;
using CommonUtils.MathLib.FeatureExtraction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.NodeServices;

namespace FindSimilarClient.Controllers
{
    public class ChartController : Controller
    {
        private readonly MFCC mfcc;
        public ChartController([FromServices] INodeServices nodeServices)
        {
            StartPool(nodeServices).GetAwaiter().GetResult();
            mfcc = new MFCC(2048, 44100, 120, 20);
        }

        public async Task<IActionResult> Chart([FromServices] INodeServices nodeServices)
        {
            // var data = new int[] { 3, 6, 2, 7, 5, 2, 0, 3, 8, 9, 2, 5, 9, 3, 6, 3, 6, 2, 7, 5, 2, 1, 3, 8, 9, 2, 5, 9, 2, 7 };
            var data = mfcc.filterWeights.MatrixData;

            var options = new { width = 1000, height = 400 };

            string markup = await nodeServices.InvokeExportAsync<string>("Node/d3Line.js", "generateChart", options, data);

            ViewData["ChartImage"] = markup;

            // return View(); // if we want the chart within the website

            string html = @"<!DOCTYPE html>
<html>
<body>
    <img src='" + markup + $"' height='{options.height}' width='{options.width}'" + @" />
</body>
</html>";

            return Content(html, "text/html"); // if we want "plain" html 
        }

        public async Task<IActionResult> StartPool([FromServices] INodeServices nodeServices)
        {
            string result = await nodeServices.InvokeExportAsync<string>("Node/d3Line.js", "startPool");
            return Content(result, "text/html"); // if we want "plain" html 
        }

        public async Task<IActionResult> StopPool([FromServices] INodeServices nodeServices)
        {
            string result = await nodeServices.InvokeExportAsync<string>("Node/d3Line.js", "stopPool");
            return Content(result, "text/html"); // if we want "plain" html 
        }
    }
}