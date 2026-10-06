const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');

const roots = {
  windows: path.resolve(__dirname, '../src/SnapCraft/Assets'),
  extension: path.resolve(__dirname, '../../../outputs/chrome-capture-extension')
};
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.ttf': 'font/ttf', '.svg': 'image/svg+xml', '.png': 'image/png' };

(async () => {
  const server = http.createServer({ maxHeaderSize: 1024 * 1024 }, (req, res) => {
    const [variant, ...parts] = new URL(req.url, 'http://localhost').pathname.slice(1).split('/');
    const root = roots[variant];
    const file = root && path.resolve(root, ...parts);
    if (!file || !file.startsWith(root + path.sep) || !fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
    res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ headless: true });
    for (const variant of Object.keys(roots)) {
      const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
      const errors = [];
      page.on('pageerror', (error) => errors.push(error.message));
      const dataUrl = await page.evaluate(() => {
        const image = document.createElement('canvas'); image.width = 800; image.height = 2400;
        const brush = image.getContext('2d'); brush.fillStyle = '#ffffff'; brush.fillRect(0, 0, 800, 2400);
        return image.toDataURL();
      });
      await page.addInitScript((url) => {
        window.chrome = window.chrome || {};
        window.chrome.storage = { local: { get: async (key) => ({ [key]: { type: 'image', dataUrl: url } }) } };
        if (location.pathname.includes('/extension/')) window.chrome.runtime = { getManifest: () => ({ version: '1.5.7' }) };
      }, dataUrl);
      const url = `http://127.0.0.1:${server.address().port}/${variant}/editor.html?image=${encodeURIComponent(dataUrl)}&id=test&version=9.8.7`;
      const response = await page.goto(url);
      assert(response.ok(), `Editor response: ${response.status()} ${await response.text()}`);
      assert.equal(await page.locator('#canvas').count(), 1, await page.content());
      await page.locator('#loading').waitFor({ state: 'hidden' });
      assert.equal(await page.locator('#appVersion').textContent(), variant === 'windows' ? 'v9.8.7' : 'v1.5.7');
      await page.locator('#editorAbout').click();
      assert.match(await page.locator('#editorAboutDialog').textContent(), /jairlinethai/);
      await page.locator('#closeEditorAbout').click();
      const box = await page.locator('#canvas').boundingBox();
      const scale = box.width / 800;
      const at = (x, y) => ({ x: box.x + x * scale, y: box.y + y * scale });
      const click = async (x, y) => { const p = at(x, y); await page.mouse.click(p.x, p.y); };
      const drag = async (x, y, dx, dy) => {
        const p = at(x, y), q = at(dx, dy);
        await page.mouse.move(p.x, p.y); await page.mouse.down(); await page.mouse.move(q.x, q.y, { steps: 5 }); await page.mouse.up();
      };
      const pixel = (x, y) => page.evaluate(([x, y]) => { render(false); const p = [...ctx.getImageData(x, y, 1, 1).data]; render(); return p; }, [x, y]);
      assert.equal(await page.locator('[data-tool="select"]').getAttribute('aria-pressed'), 'true');
      assert.equal(await page.locator('.filled-shape svg').first().evaluate((el) => getComputedStyle(el).fill), 'rgb(23, 32, 29)');
      assert.equal(await page.locator('[data-shape-group="outline"] .shape-primary svg').evaluate((el) => getComputedStyle(el).fill), 'none');
      assert.equal(await page.locator('[data-tool="blur"] svg').getAttribute('data-lucide'), variant === 'windows' ? 'droplet' : 'grid-3x3');
      if (variant === 'windows') {
        assert.equal(await page.locator('[data-tool="blur"]').getAttribute('title'), 'เบลอเฉพาะพื้นที่');
        assert.equal(await page.locator('[data-tool="blur"]').getAttribute('aria-label'), 'เบลอเฉพาะพื้นที่');
        assert.equal(await page.locator('[data-tool="blur"] svg').evaluate(el => getComputedStyle(el.querySelector('path')).filter), 'none', 'Droplet must remain crisp');
      }
      await page.locator('.shape-primary[data-tool="box"]').click();
      await drag(50, 50, 130, 130);
      const historyBefore = await page.evaluate(() => undoStack.length);
      await page.locator('#sizeInput').evaluate((input) => {
        for (const value of ['12', '18', '24']) { input.value = value; input.dispatchEvent(new Event('input', { bubbles: true })); }
      });
      assert.equal(await page.evaluate(() => objects[selected].size), 24, 'Width updates before change/release');
      assert.equal(await page.locator('#sizeValue').textContent(), '24 px');
      assert.equal(await page.evaluate(() => undoStack.length), historyBefore + 1, 'One undo per drag');
      await page.locator('#sizeInput').dispatchEvent('change');
      await page.locator('#undoButton').click();
      assert.equal(await page.evaluate(() => objects[0].size), variant === 'windows' ? 5 : 6, 'Undo restores pre-drag width');
      await page.evaluate(() => { selected = 0; syncControls(); });
      await page.locator('#opacityInput').evaluate((input) => { input.value = '32'; input.dispatchEvent(new Event('input', { bubbles: true })); });
      assert.equal(await page.evaluate(() => objects[0].opacity), 0.32);
      await page.locator('#opacityInput').dispatchEvent('change');
      await page.locator('#undoButton').click();
      await page.evaluate(() => { selected = 0; syncControls(); });
      assert.deepEqual(await pixel(90, 90), [255, 255, 255, 255]);
      assert.equal(await page.evaluate(() => activeTool), 'box');
      await page.locator('.shape-primary[data-tool="block"]').click();
      await drag(180, 50, 260, 130);
      assert.deepEqual(await pixel(220, 90), [239, 51, 64, 255]);
      for (const [group, tool, x] of [['outline', 'ellipse', 310], ['filled', 'ellipseFill', 440]]) {
        await page.locator(`[data-shape-group="${group}"] .shape-toggle`).click();
        await page.locator(`.shape-menu [data-tool="${tool}"]`).click();
        await drag(x, 50, x + 80, 130);
        assert.equal(await page.locator(`.shape-primary[data-tool="${tool}"]`).getAttribute('aria-pressed'), 'true');
        assert.equal(await page.locator(`[data-shape-group="${group}"]`).evaluate((el) => el.classList.contains('active')), true);
        assert.deepEqual(await pixel(x + 40, 90), tool === 'ellipse' ? [255, 255, 255, 255] : [239, 51, 64, 255]);
      }
      await page.locator('[data-tool="number"]').click();
      for (const x of [100, 200, 300]) await click(x, 220);
      assert.deepEqual(await page.evaluate(() => objects.filter((item) => item.tool === 'number').map((item) => item.number)), [1, 2, 3]);
      assert.equal(await page.locator('[data-tool="number"]').getAttribute('aria-pressed'), 'true');
      await page.locator('#undoButton').click(); await click(400, 220);
      assert.deepEqual(await page.evaluate(() => objects.filter((item) => item.tool === 'number').map((item) => item.number)), variant === 'windows' ? [1, 2, 3] : [1, 2, 4]);
      await page.locator('#deleteButton').click(); await click(500, 220);
      assert.equal(await page.evaluate(() => objects.at(-1).number), variant === 'windows' ? 3 : 5);
      await page.locator('[data-tool="select"]').click();
      await drag(100, 220, 140, 260);
      assert.deepEqual(await page.evaluate(() => { const item = objects.find((item) => item.number === 1); return [Math.round(item.x1), Math.round(item.y1)]; }), [140, 260]);
      const marker = await page.evaluate(() => bounds(objects[selected]));
      await drag(marker.x + marker.width, marker.y + marker.height, marker.x + marker.width * 2, marker.y + marker.height * 2);
      assert(await page.evaluate(() => objects[selected].size > 6));
      await page.locator('#undoButton').click(); await page.locator('#redoButton').click();
      assert(await page.evaluate(() => objects.find((item) => item.number === 1).size > 6));
      // Crop offsets must preserve annotation coordinates during PNG export.
      await page.locator('[data-tool="crop"]').click();
      await page.evaluate(() => { cropDraft = { x: 30, y: 30, width: 700, height: 400 }; });
      await page.locator('#applyCrop').click();
      const exported = await page.evaluate(async () => {
        const blob = await exportBlob(); const bitmap = await createImageBitmap(blob);
        const output = document.createElement('canvas'); output.width = bitmap.width; output.height = bitmap.height;
        const brush = output.getContext('2d'); brush.drawImage(bitmap, 0, 0);
        return { width: bitmap.width, height: bitmap.height, pixel: [...brush.getImageData(470, 190, 1, 1).data] };
      });
      assert.equal(exported.width, 700); assert.equal(exported.height, 400);
      assert.notDeepEqual(exported.pixel, [255, 255, 255, 255]);
      await page.screenshot({ path: path.resolve(__dirname, `../dist/editor-toolbar-${variant}.png`) });
      await page.setViewportSize({ width: 480, height: 700 });
      await page.locator('[data-shape-group="filled"] .shape-toggle').click();
      const menu = await page.locator('#filledShapes').boundingBox();
      assert(menu.x >= 0 && menu.x + menu.width <= 480);
      await page.keyboard.press('ArrowDown'); await page.keyboard.press('Enter');
      await page.locator('[data-shape-group="outline"] .shape-toggle').click();
      await page.keyboard.press('Escape');
      assert.equal(await page.locator('#outlineShapes').isVisible(), false);
      await page.screenshot({ path: path.resolve(__dirname, `../dist/editor-toolbar-${variant}-compact.png`) });
      await page.reload(); await page.locator('#loading').waitFor({ state: 'hidden' });
      await page.locator('[data-tool="number"]').click();
      const fresh = await page.locator('#canvas').boundingBox();
      await page.mouse.click(fresh.x + 60, fresh.y + 60);
      assert.equal(await page.evaluate(() => objects.at(-1).number), 1);
      await page.evaluate(() => {
        objects = [
          { tool: 'block', x1: 100, y1: 100, x2: 200, y2: 200, color: '#000000', size: 6 },
          { tool: 'blur', x1: 70, y1: 70, x2: 230, y2: 230, color: '#000000', size: 6, blur: 4 }
        ]; selected = 1; syncControls(); render();
      });
      const beforeBlur = await pixel(108, 150);
      await page.locator('#opacityInput').evaluate((input) => { input.value = '20'; input.dispatchEvent(new Event('input', { bubbles: true })); });
      assert.notDeepEqual(await pixel(108, 150), beforeBlur, 'Object opacity must update before slider release');
      await page.locator('#opacityInput').dispatchEvent('change');
      await page.locator('#undoButton').click();
      assert.deepEqual(await pixel(108, 150), beforeBlur);
      await page.locator('#paletteButton').click();
      assert.equal(await page.locator('#paletteGrid [data-color]').count(), 66);
      const paletteBounds = await page.locator('#colorPalette').boundingBox();
      assert(paletteBounds.x >= 0 && paletteBounds.x + paletteBounds.width <= 480);
      await page.locator('#paletteGrid [data-color="#fff258"]').click();
      assert.equal(await page.locator('#colorInput').inputValue(), '#fff258');
      assert.equal(await page.locator('#colorPalette').isVisible(), false);
      assert.equal(await page.locator('#recentColors [data-color="#fff258"]').count(), 1);
      await page.locator('[data-tool="highlight"]').click();
      await page.evaluate(() => {
        objects = []; selected = -1; cropRect = { x: 0, y: 0, width: 800, height: 2400 }; canvas.width = 800; canvas.height = 2400; fitCanvas();
      });
      const high = await page.locator('#canvas').boundingBox();
      await page.mouse.move(high.x + 50 * high.width / 800, high.y + 90 * high.width / 800);
      await page.mouse.down();
      await page.mouse.move(high.x + 240 * high.width / 800, high.y + 90 * high.width / 800, { steps: 6 });
      await page.mouse.up();
      assert.equal(await page.evaluate(() => objects[0].tool), 'highlight');
      assert.equal(await page.evaluate(() => objects[0].opacity), 0.4);
      const highPixel = await pixel(150, 90);
      assert(highPixel[2] > 100 && highPixel[2] < 230, 'Highlighter leaves original pixels visible');
      await page.evaluate(() => {
        window.clipboardDone = null;
        Object.defineProperty(navigator.clipboard, 'write', { configurable: true, value: () => new Promise((resolve) => { clipboardDone = resolve; }) });
      });
      await page.locator('#copyButton').click();
      assert.equal(await page.locator('#copyButton').getAttribute('aria-busy'), 'true');
      assert.equal(await page.locator('#copyButton').evaluate((button) => button.classList.contains('active')), true);
      await page.evaluate(() => clipboardDone());
      await page.waitForFunction(() => document.querySelector('#copyButton').classList.contains('copied'));
      assert.equal(await page.locator('#copyButton').getAttribute('aria-pressed'), 'true');
      assert.equal(await page.locator('#copyButton svg').getAttribute('data-lucide'), 'check');
      await page.screenshot({ path: path.resolve(__dirname, `../dist/editor-new-${variant}.png`) });
      assert.deepEqual(errors, []);
      console.log(`PASS ${variant}: shape icons/menus, active state, sequential numbers, move/resize, undo/redo, crop/PNG, compact viewport, new-capture reset`);
      await page.close();
    }
  } finally {
    await browser?.close(); await new Promise((resolve) => server.close(resolve));
  }
})().catch((error) => { console.error(error); process.exitCode = 1; });
