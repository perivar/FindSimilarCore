// Include all modules we need
const svg2png = require("svg2png");
const { JSDOM } = require("jsdom");
const d3 = require("d3");
const { convert } = require("convert-svg-to-png");
const puppeteer = require('puppeteer');

module.exports = function (callback, options, data) {

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

    // get the svg text
    // var svgText = dom.window.document.body.outerHTML; // the html, including styles, including the body tags
    var svgText = dom.window.document.body.innerHTML; // the html, including styles, excluding the body tags
    // var svgText = dom.window.d3.select("#chart").html();
    // callback(null, svgText);


    // converting SVG to PNG using the headless browser PhantomJS (using svg2png)
    // return base64 encoded PNG data-uri
    // svg2png(Buffer.from(svgText), { width: width, height: height })
    //     .then(buffer => "data:image/png;base64," + buffer.toString("base64"))
    //     .then(buffer => callback(null, buffer));


    // converting SVG to PNG using Chromium (using puppeteer)
    // return base64 encoded PNG
    // the page.goto method requires a propertly formatted svg with version and xml namespace 
    var html = dom.window.d3.select("#chart svg")
        .attr("version", 1.1)
        .attr("xmlns", "http://www.w3.org/2000/svg")
        .node().parentNode.innerHTML;

    puppeteer.launch({
        // headless: false, // The browser is visible
        // slowMo: 250, // slow down by 250ms        
        // ignoreHTTPSErrors: true
    }).then(browser => {
        browser.newPage()
            .then(page => {

                // to ensure crisp screenshots we need to set the device factor to 2
                page.setViewport({ width: width, height: height, deviceScaleFactor: 2 });

                // set debug 
                // page.on('console', msg => console.log('PAGE LOG:', msg.text()));

                // build svg content
                page.goto(`data:image/svg+xml;base64,${new Buffer(html).toString("base64")}`,
                    {
                        waitUntil: 'networkidle0',
                        timeout: 15000
                    }
                )
                    .then(resp => page.screenshot({ type: 'png' }))
                    .then(buffer => "data:image/png;base64," + buffer.toString("base64"))
                    .then(buffer => callback(null, buffer))
                    .then(buffer => browser.close());
            });
    });


    // converting SVG to PNG using headless Chromium (using convert-svg-to-png)
    // return base64 encoded PNG
    // Note! Cannot get this to work properly with double scale (for crisp fonts)
    // convert(Buffer.from(svgText),
    //     {
    //         width: width,
    //         height: height,
    //         puppeteer:
    //         {
    //             headless: false,
    //             slowMo: 250, // slow down by 250ms 
    //             ignoreHTTPSErrors: true,
    //             args: ['--force-device-scale-factor=2', `--window-size=${width},${height}`],
    //             // defaultViewport: {
    //             //     width: width,
    //             //     height: height,
    //             //     deviceScaleFactor: 2
    //             // }
    //         }
    //     })
    //     .then(buffer => "data:image/png;base64," + buffer.toString("base64"))
    //     .then(buffer => callback(null, buffer));


    // return as SVG
    // var html = dom.window.d3.select("#chart svg")
    //     .attr("version", 1.1)
    //     .attr("xmlns", "http://www.w3.org/2000/svg")
    //     .node().parentNode.innerHTML;

    // // return as base64 encoded SVG data-uri
    // var imgSrc = "data:image/svg+xml;base64," + Buffer.from(html).toString('base64');
    // callback(null, imgSrc);
}