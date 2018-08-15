

// Testing d3Line.js
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

process.on('exit', function (code) {
    return console.log((Date.now() - start) + 'ms');
});

/*
const { Cluster } = require('puppeteer-cluster');

(async () => {
    // Create a cluster with 2 workers
    const cluster = await Cluster.launch({
        concurrency: Cluster.CONCURRENCY_CONTEXT,
        maxConcurrency: 2,
    });

    // Define a task (extracting document.title in this case)
    await cluster.task(async ({ page, data: url }) => {
        await page.goto(url);

        const path = url.replace(/[^a-zA-Z]/g, '_') + '.png';
        const start = Date.now();
        await page.screenshot({ path });
        console.log((Date.now() - start) + 'ms')
        console.log(`Screenshot of ${url} saved: ${path}`);
    });

    // Add some pages to queue
    await cluster.queue('https://www.google.com');
    await cluster.queue('https://www.wikipedia.org');
    await cluster.queue('https://github.com/');

    // Shutdown after everything is done
    await cluster.idle();
    await cluster.close();
})();
*/