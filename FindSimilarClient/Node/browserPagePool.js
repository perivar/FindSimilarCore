const genericPool = require('generic-pool');
const puppeteer = require('puppeteer');

// puppeteer.launch will return a promise to resolve with a browser instance
const browserPromise = async () => await puppeteer.launch(
    {
        // headless: false, // The browser is visible
        // slowMo: 250, // slow down by 250ms        
        // ignoreHTTPSErrors: true
    }
);

const factory = {
    create: async function () {
        // reuse the same browser instance to create new pages
        const browser = await browserPromise();
        const page = await browser.newPage();
        // await page.setViewport({ width: 800, height: 420 });
        return page;
    },
    destroy: function (puppeteer) {
        puppeteer.close();
    },
};

const browserPagePool = genericPool.createPool(factory,
    {
        max: 10,
        min: 2,
        maxWaitingClients: 50,
    }
);

module.exports = browserPagePool;
