using System.Threading.Tasks;
using CommonUtils;
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
            mfcc = new MFCC(2048, 44100, 40, 20);
        }

        [HttpGet]
        [Route("/chart")]
        public async Task<IActionResult> Chart([FromServices] INodeServices nodeServices)
        {
            // var data = new double[][] {
            //     new double[] { 63.4, 58.0, 53.3, 55.7, 64.2, 58.8, 57.9, 61.8, 69.3, 71.2, 68.7, 61.8, 63.0, 66.9, 61.7, 61.8, 62.8, 60.8, 62.1, 65.1, 55.6, 54.4, 54.4, 54.8, 57.9, 54.6, 54.4, 42.5, 40.9, 38.6, 44.2, 49.6, 47.2, 50.1, 50.1, 43.5, 43.8, 48.9, 55.5, 53.7, 57.7, 48.5, 46.8, 51.1, 56.8, 59.7, 56.5, 49.6, 41.5, 44.3, 54.0, 54.1, 49.4, 50.0, 44.0, 50.3, 52.1, 49.6, 57.2, 59.1, 50.6, 44.3, 43.9, 42.1, 43.9, 50.2, 54.2, 54.6, 43.4, 42.2, 45.0, 33.8, 36.8, 38.6, 41.9, 49.6, 50.2, 40.6, 29.1, 33.7, 45.8, 47.4, 54.4, 47.8, 34.9, 35.9, 43.6, 42.9, 46.2, 30.8, 40.8, 49.8, 46.3, 43.2, 30.3, 19.2, 32.1, 41.2, 47.0, 46.0, 34.7, 39.4, 40.4, 45.4, 40.7, 30.4, 23.9, 22.6, 39.8, 43.2, 26.3, 32.8, 27.4, 25.0, 39.4, 48.7, 43.0, 37.1, 48.2, 43.7, 40.1, 38.0, 43.5, 50.4, 45.8, 37.5, 40.8, 36.5, 39.1, 43.2, 36.5, 36.5, 38.3, 36.9, 29.7, 33.1, 39.6, 42.3, 39.7, 46.0, 41.2, 39.8, 38.1, 37.1, 45.5, 50.6, 42.7, 42.6, 36.9, 40.9, 45.9, 40.7, 41.3, 36.8, 47.6, 44.2, 38.5, 32.9, 43.3, 51.2, 47.8, 37.2, 42.9, 48.8, 52.6, 60.5, 47.2, 44.7, 48.2, 48.2, 53.1, 57.8, 57.5, 57.3, 61.7, 55.8, 48.4, 49.8, 39.6, 49.7, 56.8, 46.5, 42.2, 45.3, 48.1, 51.2, 61.0, 50.7, 48.0, 51.1, 55.7, 58.3, 55.0, 49.0, 51.7, 53.1, 55.2, 62.3, 62.9, 69.3, 59.0, 54.1, 56.5, 58.2, 52.4, 51.6, 49.3, 52.5, 50.5, 51.9, 47.4, 54.1, 51.9, 57.4, 53.7, 53.1, 57.2, 57.0, 56.6, 54.6, 57.9, 59.2, 61.1, 59.7, 64.1, 65.3, 64.2, 62.0, 63.8, 64.5, 61.0, 62.6, 66.2, 62.7, 63.7, 66.4, 64.5, 65.4, 69.4, 71.9, 74.4, 75.9, 72.9, 72.5, 67.2, 68.3, 67.7, 61.9, 58.3, 61.7, 66.7, 68.7, 72.2, 72.6, 69.2, 66.9, 66.7, 67.7, 68.5, 67.5, 64.2, 61.7, 66.4, 77.9, 88.3, 82.2, 77.0, 75.4, 70.9, 65.9, 73.5, 77.4, 79.6, 84.2, 81.8, 82.5, 80.2, 77.8, 86.1, 79.9, 83.5, 81.5, 77.8, 76.1, 76.3, 75.8, 77.2, 79.3, 78.9, 79.6, 83.3, 84.3, 75.1, 68.4, 68.4, 72.2, 75.6, 82.6, 78.4, 77.0, 79.4, 77.4, 72.5, 72.9, 73.6, 75.0, 77.7, 79.7, 79.6, 81.5, 80.0, 75.7, 77.8, 78.6, 77.8, 78.5, 78.8, 78.6, 76.8, 76.7, 75.9, 77.6, 72.6, 70.4, 71.8, 73.6, 74.7, 74.6, 76.0, 76.2, 73.4, 74.6, 79.4, 74.7, 73.5, 77.9, 80.7, 75.1, 73.5, 73.5, 77.7, 74.2, 76.0, 77.1, 69.7, 67.8, 64.0, 68.1, 69.3, 70.0, 69.3, 66.3, 67.0, 72.8, 67.2, 62.1, 64.0, 65.5, 65.7, 60.4, 63.2, 68.5, 69.2, 68.7, 62.5, 62.3},
            //     new double[] { 62.7, 59.9, 59.1, 58.8, 58.7, 57.0, 56.7, 56.8, 56.7, 60.1, 61.1, 61.5, 64.3, 67.1, 64.6, 61.6, 61.1, 59.2, 58.9, 57.2, 56.4, 60.7, 65.1, 60.9, 56.1, 54.6, 56.1, 58.1, 57.5, 57.7, 55.1, 57.9, 64.6, 56.2, 50.5, 51.3, 52.6, 51.4, 50.6, 54.6, 55.6, 53.9, 54.0, 53.8, 53.5, 53.4, 52.2, 52.7, 53.1, 49.0, 50.4, 51.1, 52.3, 54.6, 55.1, 51.5, 53.6, 52.3, 51.0, 49.5, 49.8, 60.4, 62.2, 58.3, 52.7, 51.5, 49.9, 48.6, 46.4, 49.8, 52.1, 48.8, 47.4, 47.2, 46.1, 48.8, 47.9, 49.8, 49.1, 48.3, 49.3, 48.4, 53.3, 47.5, 47.9, 48.9, 45.9, 47.2, 48.9, 50.9, 52.9, 50.1, 53.9, 53.1, 49.7, 52.7, 52.6, 49.0, 51.0, 56.8, 52.3, 51.6, 49.8, 51.9, 53.7, 52.9, 49.7, 45.3, 43.6, 45.0, 47.3, 51.4, 53.7, 48.3, 52.9, 49.1, 52.1, 53.6, 50.4, 50.3, 53.8, 51.9, 50.0, 50.0, 51.3, 51.5, 52.0, 53.8, 54.6, 54.3, 51.9, 53.8, 53.9, 52.3, 50.1, 49.5, 48.6, 49.9, 52.4, 49.9, 51.6, 47.8, 48.7, 49.7, 53.4, 54.1, 55.9, 51.7, 47.7, 45.4, 47.0, 49.8, 48.9, 48.1, 50.7, 55.0, 48.8, 48.4, 49.9, 49.2, 51.7, 49.3, 50.0, 48.6, 53.9, 55.2, 55.9, 54.6, 48.2, 47.1, 45.8, 49.7, 51.4, 51.4, 48.4, 49.0, 46.4, 49.7, 54.1, 54.6, 52.3, 54.5, 56.2, 51.1, 50.5, 52.2, 50.6, 47.9, 47.4, 49.4, 50.0, 51.3, 53.8, 52.9, 53.9, 50.2, 50.9, 51.5, 51.9, 53.2, 53.0, 55.1, 55.8, 58.0, 52.8, 55.1, 57.9, 57.5, 55.3, 53.5, 54.7, 54.0, 53.4, 52.7, 50.7, 52.6, 53.4, 53.1, 56.5, 55.3, 52.0, 52.4, 53.4, 53.1, 49.9, 52.0, 56.0, 53.0, 51.0, 51.4, 52.2, 52.4, 54.5, 52.8, 53.9, 56.5, 54.7, 52.5, 52.1, 52.2, 52.9, 52.1, 52.1, 53.3, 54.8, 54.0, 52.3, 55.3, 53.5, 54.1, 53.9, 54.4, 55.0, 60.0, 57.2, 55.1, 53.3, 53.4, 54.6, 57.0, 55.6, 52.5, 53.9, 55.3, 53.3, 54.1, 55.2, 55.8, 56.8, 57.5, 57.7, 56.6, 56.4, 58.4, 58.8, 56.4, 56.5, 55.8, 54.8, 54.9, 54.7, 52.8, 53.7, 53.1, 52.7, 52.0, 53.4, 54.0, 54.0, 54.5, 56.7, 57.5, 57.1, 58.1, 57.6, 56.0, 56.6, 57.8, 57.5, 56.4, 55.3, 55.0, 55.6, 55.6, 55.9, 55.4, 54.4, 53.7, 54.1, 57.8, 58.2, 58.0, 57.0, 55.0, 54.8, 53.0, 52.5, 53.3, 53.9, 56.2, 57.1, 55.3, 56.2, 54.3, 53.1, 53.4, 54.5, 55.7, 54.8, 53.8, 56.5, 58.3, 58.7, 57.5, 55.9, 55.4, 55.7, 53.1, 53.5, 52.5, 54.5, 56.3, 56.4, 56.5, 56.4, 55.4, 56.2, 55.7, 54.3, 55.2, 54.3, 52.9, 54.8, 54.8, 56.8, 55.4, 55.8, 55.9, 52.8, 54.5, 53.3, 53.6, 52.1, 52.6, 53.9, 55.1},
            //     new double[] { 72.2, 67.7, 69.4, 68.0, 72.4, 77.0, 82.3, 78.9, 68.8, 68.7, 70.3, 75.3, 76.6, 66.6, 68.0, 70.6, 71.1, 70.0, 61.6, 57.4, 64.3, 72.4, 72.4, 72.5, 72.7, 73.4, 70.7, 56.8, 51.0, 54.9, 58.8, 62.6, 71.0, 58.4, 45.1, 52.2, 73.0, 75.4, 72.1, 56.6, 55.4, 46.7, 62.0, 71.6, 75.5, 72.1, 65.7, 56.8, 49.9, 71.7, 77.7, 76.4, 68.8, 57.0, 55.5, 61.6, 64.1, 51.1, 43.0, 46.4, 48.0, 48.1, 60.6, 62.6, 57.1, 44.2, 37.4, 35.0, 37.0, 45.4, 50.7, 48.6, 52.2, 60.8, 70.0, 64.2, 50.9, 51.6, 55.2, 62.1, 56.3, 47.2, 52.3, 45.2, 43.6, 42.9, 48.2, 45.4, 44.2, 50.4, 52.4, 53.5, 55.9, 48.2, 41.0, 48.9, 54.8, 61.2, 59.7, 52.5, 54.0, 47.7, 49.2, 48.4, 40.2, 43.9, 45.2, 65.0, 68.2, 47.5, 57.1, 61.9, 54.6, 56.7, 54.4, 52.7, 61.8, 55.0, 50.7, 52.9, 44.4, 49.1, 62.8, 64.6, 61.1, 70.0, 61.3, 48.2, 44.2, 51.3, 49.2, 45.7, 54.1, 44.9, 36.5, 44.8, 52.3, 68.0, 54.6, 53.8, 56.2, 50.8, 53.0, 61.0, 68.8, 69.4, 59.3, 47.2, 47.7, 61.9, 67.2, 70.1, 62.1, 72.7, 59.0, 51.8, 55.0, 61.8, 67.1, 72.0, 46.4, 46.7, 56.9, 61.9, 68.8, 71.9, 72.0, 72.5, 71.7, 71.1, 73.0, 63.8, 60.0, 62.3, 61.1, 62.0, 64.6, 66.0, 65.8, 69.2, 69.5, 73.5, 73.9, 75.3, 75.4, 77.3, 67.0, 71.1, 70.4, 73.6, 71.1, 70.0, 69.0, 69.2, 74.5, 73.4, 76.0, 74.5, 63.6, 67.3, 65.1, 67.9, 68.9, 65.1, 65.4, 70.1, 67.0, 75.4, 77.5, 77.0, 77.7, 77.7, 77.7, 77.0, 77.9, 79.1, 80.1, 82.1, 79.0, 79.8, 70.0, 69.8, 71.3, 69.4, 72.0, 72.4, 72.5, 67.6, 69.0, 72.7, 73.7, 77.5, 75.8, 76.9, 78.8, 77.7, 80.6, 81.4, 82.3, 80.3, 80.3, 82.2, 81.9, 82.4, 77.9, 81.1, 82.2, 81.2, 83.0, 83.2, 82.1, 77.5, 77.9, 82.9, 86.8, 85.3, 76.9, 84.5, 84.4, 83.8, 82.5, 82.9, 82.5, 81.3, 80.8, 81.7, 83.9, 85.5, 87.2, 88.0, 89.6, 86.7, 85.3, 81.7, 78.5, 83.1, 83.1, 84.5, 84.6, 84.2, 86.7, 84.3, 83.7, 77.1, 77.4, 80.6, 81.4, 80.2, 81.8, 77.3, 80.8, 81.6, 80.9, 83.9, 85.6, 83.6, 84.0, 83.0, 84.8, 84.4, 84.3, 83.9, 85.0, 84.9, 86.3, 86.5, 85.8, 85.3, 86.0, 84.2, 81.9, 86.5, 86.1, 86.8, 88.0, 85.1, 87.4, 88.0, 88.0, 87.2, 86.1, 86.8, 84.9, 76.8, 80.6, 80.0, 78.2, 79.1, 81.9, 84.7, 83.5, 82.1, 84.0, 85.7, 87.2, 82.9, 84.8, 83.9, 85.5, 86.4, 85.8, 85.4, 85.3, 81.9, 74.8, 71.6, 75.9, 82.1, 80.5, 70.0, 71.2, 70.3, 72.1, 73.7, 72.7, 71.7, 72.9, 73.1, 75.6, 78.3, 78.3, 79.6, 76.4, 77.2, 75.2, 71.9}
            // };
            var data = mfcc.filterWeights.MatrixData;

            var options = new { width = 1600, height = 900 };

            string markup = await nodeServices.InvokeExportAsync<string>("Node/d3Line.js", "generateChart", options, data);

            ViewData["ChartImage"] = markup;

            // return View(); // if we want the chart within the website

            string html = $@"<!DOCTYPE html>
<html>
<body>
    <img src='{markup}' height='{options.height}' width='{options.width}' />
</body>
</html>";

            return Content(html, "text/html"); // if we want "plain" html 
            // return Content(markup, "image/svg+xml"); // if we want "plain" svg
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