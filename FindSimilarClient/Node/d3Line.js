// Include all modules we need
const svg2png = require("svg2png");
const { JSDOM } = require("jsdom");
const d3 = require("d3");

module.exports = function (callback, options, data) {

    var dom = new JSDOM('<!DOCTYPE html><html><head><meta charset="utf-8" /></head><body><svg width="960" height="500"></svg></body></html>');
    var document = dom.window.document;
    dom.window.d3 = d3.select(dom.window.document);

    callback(null, dom.window.document.body.innerHTML);
    // callback(null, dom.window.document.body.outerHTML);
    // callback(null, dom.window.document.body.querySelector("svg").outerHTML);

    // define dimensions of graph
    var m = [80, 80, 80, 80]; // margins
    var w = 1000 - m[1] - m[3]; // width
    var h = 400 - m[0] - m[2]; // height

    // create a simple data array that we'll plot with a line (this array represents only the Y values, X will just be the index location)
    // var data = [3, 6, 2, 7, 5, 2, 0, 3, 8, 9, 2, 5, 9, 3, 6, 3, 6, 2, 7, 5, 2, 1, 3, 8, 9, 2, 5, 9, 2, 7];

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
    var svg = dom.window.d3.select("svg"),
        width = +svg.attr("width"),
        height = +svg.attr("height"),
        graph = svg.append("g").attr("transform", "translate(" + width / 2 + "," + height / 2 + ")");

    // create bottom xAxis
    var xAxis = d3.axisBottom(x).tickSize(-h);
    // Add the x-axis.
    graph.append("g")
        .attr("class", "x axis")
        .attr("transform", "translate(0," + h + ")")
        .call(xAxis);

    // create left yAxis
    var yAxisLeft = d3.axisLeft(y).ticks(4);
    // Add the y-axis to the left
    graph.append("g")
        .attr("class", "y axis")
        // .attr("transform", "translate(-25,0)")
        .call(yAxisLeft);

    // Add the line by appending an svg:path element with the data line we created above
    // do this AFTER the axes above so that the line is above the tick-lines
    graph.append("path").attr("d", line(data));

    svg2png(Buffer.from(svgText), { width: width, height: height })
        .then(buffer => 'data:image/png;base64,' + buffer.toString('base64'))
        .then(buffer => callback(null, buffer));
}