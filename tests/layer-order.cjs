const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require('C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../src/SnapCraft/Assets');
(async () => {
  const server = http.createServer((req, res) => {
    const file = path.resolve(root, '.' + new URL(req.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) return res.writeHead(404).end();
    res.setHeader('Content-Type', { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.ttf': 'font/ttf' }[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1100, height: 750 } });
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    const image = await page.evaluate(() => {
      const c = document.createElement('canvas'); c.width = 300; c.height = 200;
      const x = c.getContext('2d'); x.fillStyle = '#ffffff'; x.fillRect(0, 0, 300, 200); return c.toDataURL();
    });
    await page.goto(`http://127.0.0.1:${server.address().port}/editor.html?image=${encodeURIComponent(image)}`);
    await page.locator('#loading').waitFor({ state: 'hidden' });
    assert.equal(await page.locator('#layerUpButton').count(), 1, 'Editor needs upper/lower controls for every annotation');
    assert.equal(await page.locator('#layerUpButton').isDisabled(), true);
    await page.evaluate(() => {
      objects = ['#ff0000', '#0000ff', '#00ff00'].map((color, index) => ({ tool: 'block', color, size: 5, opacity: 1, x1: 20, y1: 20, x2: 120, y2: 120, label: String(index) }));
      selected = 1; undoStack.length = 0; redoStack.length = 0; syncControls(); render();
    });
    const order = () => page.evaluate(() => objects.map(o => o.label));
    const pixel = () => page.evaluate(() => { render(false); return [...ctx.getImageData(60, 60, 1, 1).data]; });
    await page.locator('#layerUpButton').click();
    assert.deepEqual(await order(), ['0', '2', '1']);
    assert.deepEqual(await pixel(), [0, 0, 255, 255]);
    assert.equal(await page.evaluate(() => objects[selected].label), '1');
    assert.equal(await page.locator('#layerUpButton').isDisabled(), true);
    const historySize = await page.evaluate(() => undoStack.length);
    await page.evaluate(() => reorderSelectedLayer('up'));
    assert.equal(await page.evaluate(() => undoStack.length), historySize, 'Boundary clicks must not add undo entries');
    await page.locator('#undoButton').click(); assert.deepEqual(await order(), ['0', '1', '2']);
    await page.locator('#redoButton').click(); assert.deepEqual(await order(), ['0', '2', '1']);
    await page.evaluate(() => { selected = 2; syncControls(); });
    await page.locator('#layerBackButton').click(); assert.deepEqual(await order(), ['1', '0', '2']);
    await page.locator('#layerUpButton').click(); assert.deepEqual(await order(), ['0', '1', '2']);
    await page.locator('#layerDownButton').click(); assert.deepEqual(await order(), ['1', '0', '2']);
    await page.locator('#layerFrontButton').click(); assert.deepEqual(await order(), ['0', '2', '1']);
    const exported = await page.evaluate(() => neoSnapEditor.exportProject().project);
    await page.evaluate(p => neoSnapEditor.loadProject(p), exported);
    assert.deepEqual(await order(), ['0', '2', '1']);
    assert.deepEqual(await pixel(), [0, 0, 255, 255]);
    assert.equal(await page.evaluate(() => objects.findLastIndex(o => hitTest({ x: 60, y: 60 }, o))), 2, 'Picking must follow layer order');
    const pngPixel = await page.evaluate(async () => {
      const data = neoSnapEditor.exportImage(); const image = await loadImage('data:image/png;base64,' + data.base64);
      const c = document.createElement('canvas'); c.width = image.width; c.height = image.height; const x = c.getContext('2d'); x.drawImage(image, 0, 0); return [...x.getImageData(60, 60, 1, 1).data];
    });
    assert.deepEqual(pngPixel, [0, 0, 255, 255], 'Export must keep the displayed stacking');
    await page.evaluate(async image => {
      await neoSnapEditor.addImages([image]);
      const picture = objects.find(o => o.tool === 'image');
      objects = [picture, { tool: 'text', owner: picture.id, x1: 30, y1: 30, text: 'Child', color: '#ff0000', opacity: 1, size: 5 }, { tool: 'block', label: 'outside', x1: 50, y1: 50, x2: 100, y2: 100, color: '#0000ff', opacity: 1, size: 5 }];
      selected = 0; syncControls(); render();
    }, image);
    await page.locator('#layerUpButton').click();
    assert.deepEqual(await page.evaluate(() => objects.map(o => o.tool)), ['block', 'image', 'text'], 'Image ordering carries owned annotations');
    await page.locator('#layerDownButton').click();
    assert.deepEqual(await page.evaluate(() => objects.map(o => o.tool)), ['image', 'text', 'block']);
    for (const width of [1100, 380]) {
      await page.setViewportSize({ width, height: 750 });
      for (const id of ['layerFrontButton', 'layerUpButton', 'layerDownButton', 'layerBackButton']) {
        assert.equal(await page.locator(`#${id} svg`).count(), 1, 'Layer icon must render');
        const b = await page.locator('#' + id).boundingBox(); assert(b.x >= 0 && b.x + b.width <= width, 'Layer controls must fit');
      }
      await page.screenshot({ path: path.resolve(__dirname, `../dist/layer-order-${width}.png`) });
    }
    assert.deepEqual(errors, []);
    console.log('PASS layer ordering: steps/extremes, selection, no-op history, undo/redo, pixels/export/round-trip, image ownership, responsive icons');
  } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
