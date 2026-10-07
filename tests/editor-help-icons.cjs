const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(process.env.EDITOR_ASSETS_ROOT || path.join(__dirname, '../src/SnapCraft/Assets'));
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.ttf': 'font/ttf' };

(async () => {
  const server = http.createServer((req, res) => {
    const file = path.resolve(root, '.' + new URL(req.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
    res.setHeader('Content-Type', types[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    const errors = [], missing = [], malformed = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (message.type() === 'warn') errors.push(message.text()); });
    const image = await page.evaluate(() => {
      const canvas = document.createElement('canvas'); canvas.width = 900; canvas.height = 500;
      const ctx = canvas.getContext('2d'); ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, 900, 500);
      return canvas.toDataURL();
    });
    const origin = `http://127.0.0.1:${server.address().port}`;
    const screenshots = path.resolve(__dirname, '../dist/editor-help-check');
    fs.mkdirSync(screenshots, { recursive: true });
    for (const product of ['SnapZy', 'Neo Snap']) {
      for (const language of ['en', 'th']) {
        await page.setViewportSize({ width: 1200, height: 800 });
        await page.goto(`${origin}/editor.html?product=${encodeURIComponent(product)}&language=${language}&image=${encodeURIComponent(image)}`);
        await page.locator('#loading').waitFor({ state: 'hidden' });
        await page.locator('[data-tool="arrow"]').click();
        await page.locator('#editorHelpButton').click();
        assert.equal(await page.locator('#editorHelpDialog').isVisible(), true);
        const rows = await page.locator('#editorHelpContent li').evaluateAll(nodes => nodes.map(node => {
          const icon = node.querySelector('svg');
          const bounds = icon?.getBBox();
          const size = icon?.getBoundingClientRect();
          return { id: node.dataset.helpFor.split(' ')[0], drawn: !!bounds && bounds.width > 0 && bounds.height > 0 && size.width >= 16 && size.height >= 16 };
        }));
        assert(rows.length > 0, 'Help must contain the guide entries');
        missing.push(...rows.filter(row => !row.drawn).map(row => `${product}/${language}: ${row.id}`));
        const marker = page.locator('[data-help-for="number"] .number-icon');
        if (await marker.count() !== 1 || await marker.textContent() !== '1') malformed.push(`${product}/${language}: numbered marker lost its numeral`);
        if (await page.locator('[data-help-for^="paletteButton"] svg[data-lucide="palette"]').count() !== 1) malformed.push(`${product}/${language}: palette shown as a dropdown arrow`);
        if (await marker.count() === 1) {
          await marker.scrollIntoViewIfNeeded();
          const label = await marker.boundingBox();
          const ring = await page.locator('[data-help-for="number"] svg').boundingBox();
          assert(Math.abs(label.x + label.width / 2 - ring.x - ring.width / 2) <= 1 && Math.abs(label.y + label.height / 2 - ring.y - ring.height / 2) <= 1, 'Numeral must remain centered inside its circle');
        }
        assert.equal(await page.locator('[data-help-for^="box "] svg').evaluate(icon => getComputedStyle(icon).fill), 'none', 'Outline shape stays transparent');
        assert.notEqual(await page.locator('[data-help-for^="block "] svg').evaluate(icon => getComputedStyle(icon).fill), 'none', 'Filled shape stays visibly solid');
        const count = rows.length;
        const nextLanguage = language === 'en' ? 'th' : 'en';
        await page.evaluate(value => editorI18n.setLanguage(value), nextLanguage);
        assert.equal(await page.locator('html').getAttribute('lang'), nextLanguage);
        assert.equal(await page.locator('#editorHelpContent li svg').count(), count, 'Language changes must retain every icon while Help is open');
        await page.evaluate(value => editorI18n.setLanguage(value), language);
        for (const width of [760, 380]) {
          await page.setViewportSize({ width, height: 560 });
          const dialog = await page.locator('#editorHelpDialog').boundingBox();
          assert(dialog.x >= 0 && dialog.y >= 0 && dialog.x + dialog.width <= width && dialog.y + dialog.height <= 560, 'Help must fit a compact viewport');
        }
        await page.setViewportSize({ width: 760, height: 560 });
        const name = `${product.replace(/\s/g, '-').toLowerCase()}-${language}`;
        await page.locator('#editorHelpContent').evaluate(node => { node.scrollTop = 0; });
        await page.locator('#editorHelpDialog').screenshot({ path: path.join(screenshots, `${name}-top.png`) });
        await page.locator('#editorHelpContent').evaluate(node => { node.scrollTop = node.scrollHeight; });
        await page.locator('#editorHelpDialog').screenshot({ path: path.join(screenshots, `${name}-styles.png`) });
        await page.keyboard.press('Escape');
        assert.equal(await page.evaluate(() => activeTool), 'arrow', 'Closing Help must preserve the selected tool');
      }
    }
    if (!process.argv.includes('--markers-only')) assert.deepEqual(missing, [], 'Every Help entry must have a rendered icon, including sliders, checkboxes and text-only controls');
    assert.deepEqual(malformed, [], 'Help icons must retain meaningful tool symbols');
    assert.deepEqual(errors, [], 'Help must not request missing icons or cause browser errors');
    console.log('PASS every editor Help icon: SnapZy / Neo Snap, EN / TH, live language, numbered/filled symbols, compact viewports');
  } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
