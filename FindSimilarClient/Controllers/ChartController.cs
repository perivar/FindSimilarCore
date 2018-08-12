using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.NodeServices;

namespace FindSimilarClient.Controllers
{
    public class ChartController : Controller
    {
        public async Task<IActionResult> Chart([FromServices] INodeServices nodeServices)
        {
            var data = new int[] { 3, 6, 2, 7, 5, 2, 0, 3, 8, 9, 2, 5, 9, 3, 6, 3, 6, 2, 7, 5, 2, 1, 3, 8, 9, 2, 5, 9, 2, 7 };

            var options = new { width = 1000, height = 400 };

            string markup = await nodeServices.InvokeAsync<string>("Node/d3Line.js", options, data);

            ViewData["ChartImage"] = markup;

            return View();
        }
    }
}