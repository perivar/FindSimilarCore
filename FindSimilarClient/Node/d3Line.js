// Include all modules we need
const { JSDOM } = require("jsdom");
const d3 = require("d3");
const browserPagePool = require('./browserPagePool.js');

module.exports = async function (callback, options, data) {

    // Create disconnected HTML DOM and attach it to D3
    // note: multi-line html can be enclosed in `
    var dom = new JSDOM(`<html><body><div id="chart"></div></body></html>`);
    dom.window.d3 = d3.select(dom.window.document);

    // define dimensions of graph
    var width = options.width || 1000;
    var height = options.height || 400;

    var margin = {
        top: 80,
        right: 80,
        bottom: 80,
        left: 80
    },
        w = width - margin.left - margin.right,
        h = height - margin.top - margin.bottom;

    // X scale will fit all values from data[] within pixels 0-w
    var x = d3.scaleLinear().domain([0, data.length]).range([0, w]);

    // Y scale will fit values within pixels h-0 (Note the inverted domain for the y-scale: bigger is up!)
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
        .attr("transform", "translate(" + margin.right + "," + margin.top + ")");

    // create bottom xAxis
    var xAxis = d3.axisBottom(x).tickSize(-h);
    // Add the x-axis.
    var xg = graph.append("svg:g")
        .attr("class", "x axis")
        .style("shape-rendering", "crispEdges")
        .attr("transform", "translate(0," + h + ")")
        .call(xAxis);

    xg.selectAll("line")
        .style("stroke", "lightgrey");

    xg.selectAll("path")
        .style("display", "none");

    // create left yAxis
    var yAxisLeft = d3.axisLeft(y).ticks(4);
    // Add the y-axis to the left
    var yg = graph.append("svg:g")
        .attr("class", "y axis")
        .style("shape-rendering", "crispEdges")
        // .attr("transform", "translate(-25,0)")
        .call(yAxisLeft);

    yg.selectAll("path")
        .style("fill", "none")
        .style("stroke", "#000")
        .style("stroke-width", "1.5");

    yg.selectAll("path")
        .style("fill", "none")
        .style("stroke", "#000");

    // Add the line by appending an svg:path element with the data line we created above
    // do this AFTER the axes above so that the line is above the tick-lines
    graph.append("svg:path")
        .attr("d", line(data))
        .style("fill", "none")
        .style("stroke", "steelblue")
        .style("stroke-width", "1.5");

    // converting SVG to PNG using Chromium (using puppeteer)
    // return base64 encoded PNG
    // the page.goto method requires a propertly formatted svg with version and xml namespace 
    var html = dom.window.d3.select("#chart svg")
        .attr("version", 1.1)
        .attr("xmlns", "http://www.w3.org/2000/svg")
        .node().parentNode.innerHTML;

    // // return as base64 encoded SVG data-uri
    // var imgSrc = "data:image/svg+xml;base64," + Buffer.from(html).toString('base64');
    // callback(null, imgSrc);
    // return;        
    

    // either use module.exports = async function
    // or wrap in anonymous async method 
    // (async () => {

    // Use pool in your code to acquire/release resources
    const page = await browserPagePool.acquire();

    await page.goto(`data:image/svg+xml;base64,${new Buffer(html).toString("base64")}`,
        {
            // waitUntil: 'networkidle0',
            // timeout: 15000
        }
    );

    // to ensure crisp screenshots we need to set the device factor to 2
    await page.setViewport({ width: width, height: height, deviceScaleFactor: 2 });

    // const start = Date.now();

    // jpeg is somewhat faster than png
    const screenshot = await page.screenshot({ type: 'jpeg' });
    var buffer = "data:image/jpeg;base64," + screenshot.toString("base64");
    await browserPagePool.release(page);

    // console.log((Date.now() - start) + 'ms')

    return callback(null, buffer);

    // wrap in anonymous async method 
    // })();

}