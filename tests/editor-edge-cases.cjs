const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');

const root = path.resolve(__dirname, '../src/SnapCraft/Assets');
const evidence = path.resolve(process.env.EDITOR_TEST_OUTPUT || path.join(__dirname, '../dist/editor-edge-cases'));
const group = process.argv[2];
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.ttf': 'font/ttf', '.png': 'image/png', '.svg': 'image/svg+xml' };

(async () => {
  fs.mkdirSync(evidence, { recursive: true });
  const results = [];
  const server = http.createServer({ maxHeaderSize: 1024 * 1024 }, (req, res) => {
    const file = path.resolve(root, '.' + new URL(req.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) { res.writeHead(404).end(); return; }
    res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ headless: true });
    const fixture = await browser.newPage();
    const image = await fixture.evaluate(() => {
      const canvas = document.createElement('canvas'); canvas.width = 800; canvas.height = 2400;
      const ctx = canvas.getContext('2d'); ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, canvas.width, canvas.height);
      return canvas.toDataURL();
    });
    await fixture.close();
    const check = async (category, id, exercise) => {
      if (group && category !== group) return;
      const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
      const errors = []; page.on('pageerror', (error) => errors.push(error.message));
      try {
        const response = await page.goto(`http://127.0.0.1:${server.address().port}/editor.html?image=${encodeURIComponent(image)}`);
        assert(response.ok());
        await page.locator('#loading').waitFor({ state: 'hidden' });
        await page.waitForFunction(() => [...document.images].every((image) => image.complete && image.naturalWidth > 0));
        await exercise(page);
        assert.deepEqual(errors, [], 'No uncaught editor errors');
        results.push({ id, status: 'PASS' });
      } catch (error) {
        await page.screenshot({ path: path.join(evidence, `${id}.png`) });
        results.push({ id, status: 'FAIL', error: error.message, pageErrors: errors });
      } finally { await page.close(); }
    };
    for (const mode of ['draw', 'move', 'resize', 'crop']) {
      for (const key of ['Escape', 'Control+z', 'Delete']) {
        if (mode === 'crop' && key === 'Delete') continue;
        await check('gesture', `${mode}-interrupted-${key.replace('+', '-').toLowerCase()}`, async (page) => {
          const box = await page.locator('#canvas').boundingBox();
          if (mode === 'move' || mode === 'resize') {
            await page.locator('.shape-primary[data-tool="box"]').click();
            await page.mouse.move(box.x + 60, box.y + 60); await page.mouse.down();
            await page.mouse.move(box.x + 180, box.y + 180); await page.mouse.up();
            await page.locator('[data-tool="select"]').click();
          } else await page.locator(`[data-tool="${mode === 'crop' ? 'crop' : 'pen'}"]`).click();
          const start = mode === 'resize' ? 180 : 100;
          await page.mouse.move(box.x + start, box.y + start); await page.mouse.down();
          await page.mouse.move(box.x + start + 25, box.y + start + 25, { steps: 3 });
          await page.keyboard.press(key);
          if (mode !== 'crop' || key === 'Escape') {
            assert.equal(await page.evaluate(() => gesture), null, 'Interrupted gesture must finish immediately');
          }
          await page.mouse.move(box.x + start + 45, box.y + start + 45, { steps: 3 }); await page.mouse.up();
          assert.equal(await page.evaluate(() => gesture), null);
          await page.locator('[data-tool="pen"]').click();
          const current = await page.locator('#canvas').boundingBox();
          const before = await page.evaluate(() => objects.length);
          await page.mouse.move(current.x + 240, current.y + 100); await page.mouse.down();
          await page.mouse.move(current.x + 280, current.y + 140, { steps: 3 }); await page.mouse.up();
          assert.equal(await page.evaluate(() => objects.length), before + 1, 'Next stroke must work');
        });
      }
    }
    await check('gesture', 'crop-undo-stops-auto-scroll', async (page) => {
      await page.locator('.shape-primary[data-tool="box"]').click();
      const box = await page.locator('#canvas').boundingBox();
      await page.mouse.move(box.x + 50, box.y + 50); await page.mouse.down();
      await page.mouse.move(box.x + 120, box.y + 120); await page.mouse.up();
      await page.locator('[data-tool="crop"]').click();
      const stageBox = await page.locator('#stage').boundingBox();
      await page.mouse.move(box.x + 50, box.y + 60); await page.mouse.down();
      await page.mouse.move(box.x + 450, stageBox.y + stageBox.height - 3, { steps: 3 });
      await page.waitForFunction(() => stage.scrollTop > 100);
      await page.keyboard.press('Control+z');
      const stoppedAt = await page.evaluate(() => stage.scrollTop);
      await page.waitForTimeout(200);
      assert.equal(await page.evaluate(() => gesture), null);
      assert.equal(await page.evaluate(() => cropScrollFrame), 0);
      assert.equal(await page.evaluate(() => stage.scrollTop), stoppedAt);
      await page.mouse.up();
    });
    for (const tool of ['line', 'arrow', 'pen', 'highlight']) {
      for (const axis of ['horizontal', 'vertical']) {
        for (const reverse of [false, true]) {
          await check('resize', `${tool}-${axis}-${reverse ? 'reversed' : 'forward'}`, async (page) => {
            const result = await page.evaluate(({ tool, axis, reverse }) => {
              const a = { x: 100, y: 100 }, b = axis === 'horizontal' ? { x: 300, y: 100 } : { x: 100, y: 300 };
              const points = reverse ? [b, a] : [a, b];
              const original = { tool, size: 6, color: '#000000' };
              if (tool === 'pen' || tool === 'highlight') original.points = points;
              else Object.assign(original, { x1: points[0].x, y1: points[0].y, x2: points[1].x, y2: points[1].y });
              const item = structuredClone(original);
              resizeItem(item, original, axis === 'horizontal' ? { x: 320, y: 102 } : { x: 102, y: 320 });
              return { bounds: bounds(item), length: item.points ? Math.hypot(item.points[1].x - item.points[0].x, item.points[1].y - item.points[0].y) : Math.hypot(item.x2 - item.x1, item.y2 - item.y1) };
            }, { tool, axis, reverse });
            assert(Math.abs(result.length - 220) < 1, '2px perpendicular movement must not amplify length: ' + JSON.stringify(result));
            assert.equal(result.bounds.x, 100); assert.equal(result.bounds.y, 100);
          });
        }
      }
    }
    await check('resize', 'thin-diagonal-and-shape-scaling', async (page) => {
      await page.evaluate(() => {
        const original = { tool: 'arrow', x1: 100, y1: 100, x2: 300, y2: 102, size: 6 };
        const item = structuredClone(original); resizeItem(item, original, { x: 320, y: 104 });
        if (Math.abs(item.x2 - item.x1 - 220) > 1) throw new Error('Near-horizontal line resize amplified');
        const square = { tool: 'box', x1: 100, y1: 100, x2: 200, y2: 200, size: 6 };
        const resized = structuredClone(square); resizeItem(resized, square, { x: 300, y: 300 });
        if (bounds(resized).width !== 200 || bounds(resized).height !== 200) throw new Error('Shape scaling regressed');
      });
    });
    await check('crop', 'fractional-edge-repeated-crop-and-history', async (page) => {
      const apply = async (draft) => {
        await page.locator('[data-tool="crop"]').click();
        await page.evaluate((draft) => { cropDraft = draft; }, draft);
        await page.locator('#applyCrop').click();
      };
      await apply({ x: 0.5, y: 0.5, width: 799.5, height: 2399.5 });
      const first = await page.evaluate(() => ({ ...cropRect }));
      assert(first.x + first.width <= 800 && first.y + first.height <= 2400, JSON.stringify(first));
      await apply({ x: first.x + 0.5, y: first.y + 0.5, width: first.width - 0.5, height: first.height - 0.5 });
      const second = await page.evaluate(() => ({ ...cropRect }));
      assert(second.x + second.width <= first.x + first.width && second.y + second.height <= first.y + first.height);
      const pixel = await page.evaluate(async () => {
        const blob = await exportBlob(); const image = await createImageBitmap(blob);
        const output = document.createElement('canvas'); output.width = image.width; output.height = image.height;
        const ctx = output.getContext('2d'); ctx.drawImage(image, 0, 0);
        return [...ctx.getImageData(output.width - 1, output.height - 1, 1, 1).data];
      });
      assert.deepEqual(pixel, [255, 255, 255, 255]);
      await page.locator('#undoButton').click();
      assert.deepEqual(await page.evaluate(() => cropRect), first);
      await page.locator('#redoButton').click();
      assert.deepEqual(await page.evaluate(() => cropRect), second);
    });
    await check('crop', 'long-image-auto-scroll', async (page) => {
      await page.locator('[data-tool="crop"]').click();
      const stageBox = await page.locator('#stage').boundingBox(), box = await page.locator('#canvas').boundingBox();
      await page.mouse.move(box.x + 50, box.y + 80); await page.mouse.down();
      await page.mouse.move(box.x + 500, stageBox.y + stageBox.height - 3, { steps: 6 });
      await page.waitForFunction(() => stage.scrollTop > 500); await page.mouse.up();
      assert(await page.evaluate(() => cropDraft.height > 800));
      await page.locator('#applyCrop').click();
      assert.equal(await page.evaluate(() => gesture), null);
      await page.screenshot({ path: path.join(evidence, 'long-image-cropped.png') });
    });
  } finally { await browser?.close(); await new Promise((resolve) => server.close(resolve)); }
  fs.writeFileSync(path.join(evidence, `results-${group || 'all'}.json`), JSON.stringify(results, null, 2));
  for (const result of results) console.log(`${result.status} ${result.id}${result.error ? ': ' + result.error : ''}`);
  if (!results.length || results.some((result) => result.status === 'FAIL')) process.exitCode = 1;
})().catch((error) => { console.error(error); process.exitCode = 1; });
