const fs = require('node:fs'), path = require('node:path'), http = require('node:http');
const assert = require('node:assert/strict');
const {chromium} = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../src/SnapCraft/Assets');
(async () => {
  const server = http.createServer((req, res) => {
    const file = path.resolve(root, '.' + new URL(req.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) return res.writeHead(404).end();
    res.setHeader('Content-Type', {'.html':'text/html', '.js':'text/javascript', '.css':'text/css'}[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({headless: true});
    const page = await browser.newPage();
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => { window.sent = []; window.chrome = {webview: {postMessage: value => sent.push(value), addEventListener: () => {}}}; });
    for (const product of ['SnapZy', 'Neo Snap']) {
      await page.goto(`http://127.0.0.1:${server.address().port}/launcher.html?product=${encodeURIComponent(product)}`);
      await page.locator('#openProject').click();
      assert.equal(await page.evaluate(() => sent.at(-1).action), 'browseImages', `${product} must browse images rather than projects`);
    }
    assert.deepEqual(errors, []);
    console.log('PASS launcher browse images: complete browser page, both products');
  } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
