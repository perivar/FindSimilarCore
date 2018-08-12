using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.NodeServices;

namespace FindSimilarClient.Controllers
{
    public class ChartController : Controller
    {
        public async Task<IActionResult> Chart([FromServices] INodeServices nodeServices)
        {
            var data = new int [3, 6, 2, 7, 5, 2, 0, 3, 8, 9, 2, 5, 9, 3, 6, 3, 6, 2, 7, 5, 2, 1, 3, 8, 9, 2, 5, 9, 2, 7];

            var options = new { width = 400, height = 200 };

            string markup = await nodeServices.InvokeAsync<string>("Node/d3Line.js", options, data);

            string html = @"<!DOCTYPE html>
<html>
<head><meta charset=""utf-8"" />
    <style type=""text/css"">
        /* tell the SVG path to be a thin blue line without any area fill */
        path {
            stroke: steelblue;
            stroke-width: 1.5;
            fill: none;
        }

        .axis {
            shape-rendering: crispEdges;
        }

        .x.axis line {
            stroke: lightgrey;
        }

        .x.axis path {
            display: none;
        }

        .y.axis line,
        .y.axis path {
            fill: none;
            stroke: #000;
        }
    </style>
</head>
<body>
    <img src=""" + markup + @""" />
</body>
</html>";

            return Content(html, "text/html");
        }
    }
}