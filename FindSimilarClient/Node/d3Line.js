// Include all modules we need
const svg2png = require("svg2png");
const { JSDOM } = require("jsdom");
const d3 = require("d3");

module.exports = function (callback, options, data) {

    var dom = new JSDOM(
        `<html>
        <head>
            &nbsp;
            <style>
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
            <div id="chart"></div>
        </body>
        </html>`
    );

    // Create disconnected HTML DOM and attach it to D3
    // var dom = new JSDOM('<html><body><div id="chart"></div></body></html>');
    dom.window.d3 = d3.select(dom.window.document);

    // define dimensions of graph
    var width = options.width || 1000;
    var height = options.height || 400;
    var m = [80, 80, 80, 80]; // margins
    var w = width - m[1] - m[3]; // width
    var h = height - m[0] - m[2]; // height

    // X scale will fit all values from data[] within pixels 0-w
    var x = d3.scaleLinear().domain([0, data.length]).range([0, w]);
    // Y scale will fit values from 0-10 within pixels h-0 (Note the inverted domain for the y-scale: bigger is up!)
    // var y = d3.scaleLinear().domain([0, 10]).range([h, 0]);
    // automatically determining max range can work something like this
    var y = d3.scaleLinear().domain([0, d3.max(data)]).range([h, 0]);

    // create a line function that can convert data[] into x and y points
    var line = d3.line()
        // assign the X function to plot our line as we wish
        .x(function (d, i) {
            // verbose logging to show what's actually being done
            // console.log('Plotting X value for data point: ' + d + ' using index: ' + i + ' to be at: ' + x(i) + ' using our xScale.');
            // return the X coordinate where we want to plot this datapoint
            return x(i);
        })
        .y(function (d) {
            // verbose logging to show what's actually being done
            // console.log('Plotting Y value for data point: ' + d + ' to be at: ' + y(d) + " using our yScale.");
            // return the Y coordinate where we want to plot this datapoint
            return y(d);
        })

    // Add an SVG element with the desired dimensions and margin.
    var graph = dom.window.d3.select("#chart")
        .append("svg:svg")
        .attr("width", width)
        .attr("height", height)
        .append("svg:g")
        .attr("transform", "translate(" + m[3] + "," + m[0] + ")");

    // create bottom xAxis
    var xAxis = d3.axisBottom(x).tickSize(-h);
    // Add the x-axis.
    graph.append("svg:g")
        .attr("class", "x axis")
        .attr("transform", "translate(0," + h + ")")
        .call(xAxis);

    // create left yAxis
    var yAxisLeft = d3.axisLeft(y).ticks(4);
    // Add the y-axis to the left
    graph.append("svg:g")
        .attr("class", "y axis")
        // .attr("transform", "translate(-25,0)")
        .call(yAxisLeft);

    // Add the line by appending an svg:path element with the data line we created above
    // do this AFTER the axes above so that the line is above the tick-lines
    graph.append("svg:path").attr("d", line(data));

    // Convert SVG to PNG and return it to controller
    // var svgText = dom.window.document.body.outerHTML;
    var svgText = dom.window.document.body.innerHTML;
    // var svgText = dom.window.d3.select("#chart").html();
    // callback(null, svgText);

    svg2png(Buffer.from(svgText), { width: width, height: height })
        .then(buffer => "data:image/png;base64," + buffer.toString("base64"))
        .then(buffer => callback(null, buffer));
}