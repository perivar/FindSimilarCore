
// Testing d3Line.js
const chart = require('./d3Line.js');

// var data = [3, 6, 2, 7, 5, 2, 0, 3, 8, 9, 2, 5, 9, 3, 6, 3, 6, 2, 7, 5, 2, 1, 3, 8, 9, 2, 5, 9, 2, 7];
var data =
    [
        [12, 95, 74, 48, 1, 92, 9, 8, 100, 27, 35, 32, 24, 70, 64, 30, 99, 53, 62, 81],
        [97, 82, 49, 76, 14, 3, 71, 21, 80, 79, 7, 11, 75, 19, 50, 45, 83, 54, 87, 20],
        [5, 43, 52, 63, 4, 46, 56, 2, 72, 37, 65, 84, 86, 57, 68, 10, 88, 77, 89, 96],
        [25, 67, 41, 23, 28, 18, 13, 51, 17, 31, 44, 61, 55, 39, 66, 40, 94, 93, 16, 15],
        [90, 47, 22, 33, 98, 34, 42, 36, 6, 29, 69, 59, 85, 91, 60, 38, 78, 26, 58, 73],
    ];

var options = { width: 1000, height: 400 };

const start = Date.now();
var result = chart.startPool(
    function (err, result) {
        console.log(result)
    },
    null,
    null);

var result = chart.generateChart(
    function (err, result) {
        console.log(result)
    },
    options,
    data);

var result = chart.stopPool(
    function (err, result) {
        console.log(result)
        process.exit();
    },
    null,
    null);


process.on('exit', function (code) {
    return console.log((Date.now() - start) + 'ms');
});
