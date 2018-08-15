
// Testing d3Line.js
const browserPagePool = require('./browserPagePool.js');
const chart = require('./d3Line.js');
var data = [3, 6, 2, 7, 5, 2, 0, 3, 8, 9, 2, 5, 9, 3, 6, 3, 6, 2, 7, 5, 2, 1, 3, 8, 9, 2, 5, 9, 2, 7];
var options = { width: 1000, height: 400 };

const start = Date.now();
var result = chart(
    function (err, result) {
        console.log(result)
    },
    options,
    data);


// Only call this once in your application -- at the point you want
// to shutdown and stop using this pool.
browserPagePool.drain().then(function () {
    browserPagePool.clear();
    process.exit();
});

process.on('exit', function (code) {
    return console.log((Date.now() - start) + 'ms');
});
