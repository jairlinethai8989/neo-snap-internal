const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../src/SnapCraft/Assets');
const output = path.resolve(__dirname, '../dist/team-readiness');
const group = process.argv[2];
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.ttf': 'font/ttf', '.svg': 'image/svg+xml', '.png': 'image/png' };
(async () => {
  fs.mkdirSync(output, { recursive: true });
  const server = http.createServer({ maxHeaderSize: 1024 * 1024 }, (req, res) => {
    const file = path.resolve(root, '.' + new URL(req.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) { res.writeHead(404).end(); return; }
    res.setHeader('Content-Type', mime[path.extname(file)] || 'application/octet-stream'); fs.createReadStream(file).pipe(res);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ headless: true });
    const origin = `http://127.0.0.1:${server.address().port}`;
    if (!group || group === 'launcher') {
      const page = await browser.newPage({ viewport: { width: 380, height: 156 } });
      await page.addInitScript(() => {
        window.sent = [];
        window.chrome = { webview: { postMessage: value => sent.push(value), addEventListener: (_, callback) => window.receive = callback } };
      });
      await page.goto(origin + '/launcher.html');
      for (const selector of ['header', '.actions', '.options']) {
        const box = await page.locator(selector).boundingBox();
        assert(box.y >= 0 && box.y + box.height <= 156 && box.x + box.width <= 380, `${selector} must fit the compact launcher`);
      }
      assert.equal(await page.locator('#state').textContent(), 'พร้อมใช้งาน');
      await page.locator('[data-action="scroll"]').click();
      assert.equal(await page.locator('[data-action="scroll"]').getAttribute('aria-busy'), 'false', 'Selection alone is not a running capture');
      await page.evaluate(() => receive({ data: { type: 'captureActivity', mode: 'scroll', value: true } }));
      assert.equal(await page.locator('[data-action="scroll"]').getAttribute('aria-busy'), 'true');
      assert.equal(await page.locator('[data-action="scroll"]').isDisabled(), true);
      assert.match(await page.locator('#state').textContent(), /กำลัง/);
      await page.evaluate(() => receive({ data: { type: 'captureActivity', mode: 'scroll', value: false } }));
      assert.equal(await page.locator('#state').textContent(), 'พร้อมใช้งาน');
      assert.equal(await page.locator('[data-action="scroll"]').getAttribute('aria-busy'), 'false');
      await page.evaluate(() => receive({ data: { type: 'idle' } }));
      assert.equal(await page.locator('.actions .active').count(), 0);
      await page.mouse.move(0, 0);
      await page.screenshot({ path: path.join(output, 'launcher-compact.png') });
      await page.locator('#shortcutButton').click();
      await page.waitForFunction(() => sent.some(message => message.action === 'layout' && message.expanded));
      await page.setViewportSize({ width: 380, height: 320 });
      await page.locator('#hotkeyMode').selectOption('area');
      assert.equal(await page.locator('#hotkeyKey').inputValue(), '44');
      assert.equal(await page.locator('#hotkeyCtrl').isChecked(), true);
      assert.equal(await page.locator('#hotkeyAlt').isChecked(), false);
      assert.equal(await page.locator('#hotkeyShift').isChecked(), false);
      await page.locator('#saveShortcut').click();
      assert.deepEqual(await page.evaluate(() => sent.findLast(item => item.action === 'hotkey')), { action: 'hotkey', mode: 'area', modifiers: 2, key: 44, enabled: true });
      await page.screenshot({ path: path.join(output, 'launcher-shortcuts.png') });
      await page.locator('#closeShortcut').click();
      await page.waitForFunction(() => sent.at(-1).action === 'layout' && sent.at(-1).expanded === false);
      await page.locator('#aboutButton').click();
      await page.locator('#aboutDialog summary').click();
      assert.match(await page.locator('#aboutDialog').textContent(), /0\.1\.20/);
      assert.match(await page.locator('#aboutDialog').textContent(), /จัดชั้นวัตถุและภาพ/);
      await page.screenshot({ path: path.join(output, 'launcher-about-0.1.19.png') });
      await page.locator('#closeAbout').click();
      console.log('PASS compact launcher: layout, truthful activity, idle reset, dialog expansion');
      await page.close();
    }
    if (!group || group === 'editor') {
      const page = await browser.newPage({ viewport: { width: 1000, height: 700 } });
      const errors = []; page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(() => {
        window.sent = []; window.chrome = { webview: { postMessage: value => sent.push(value) } };
        window.ClipboardItem = class { constructor(value) { this.value = value; } };
        Object.defineProperty(navigator, 'clipboard', { value: { write: async () => { if (window.failCopy) throw new Error('copy failed'); } } });
      });
      const fixture = await page.evaluate(() => {
        const c = document.createElement('canvas'); c.width = 800; c.height = 1600;
        const x = c.getContext('2d'); x.fillStyle = '#ffffff'; x.fillRect(0, 0, c.width, c.height); return c.toDataURL();
      });
      await page.goto(origin + '/editor.html?image=' + encodeURIComponent(fixture));
      await page.locator('#loading').waitFor({ state: 'hidden' });
      assert.equal(await page.evaluate(() => window.neoSnapEditor?.hasUnkeptChanges()), true, 'A new capture has not been kept yet');
      await page.locator('#copyButton').click();
      await page.waitForFunction(() => !document.querySelector('#copyButton').disabled);
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), false, 'Successful clipboard copy keeps this exact state');
      await page.locator('[data-tool="pen"]').click();
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), false, 'Choosing a tool is not an edit');
      const box = await page.locator('#canvas').boundingBox();
      await page.mouse.move(box.x + 80, box.y + 80); await page.mouse.down();
      await page.mouse.move(box.x + 160, box.y + 120); await page.mouse.up();
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), true, 'Drawing after copying requires close confirmation');
      await page.locator('#undoButton').click();
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), false, 'Undo back to copied image does not warn');
      await page.locator('#redoButton').click();
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), true);
      await page.evaluate(() => window.failCopy = true);
      await page.locator('#copyButton').click();
      await page.waitForFunction(() => !document.querySelector('#copyButton').disabled);
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), true, 'Failed copy must not keep an image');
      const exported = await page.evaluate(() => neoSnapEditor.exportImage());
      assert(exported.base64.startsWith('iVBOR'), 'Native save receives a flattened PNG');
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), true, 'Exporting alone is not a completed save');
      await page.evaluate(snapshot => neoSnapEditor.markKept(snapshot), exported.snapshot);
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), false);
      await page.locator('#downloadButton').click();
      assert.equal(await page.evaluate(() => sent.at(-1).action), 'saveImage', 'Native save must own completion rather than blind download');
      await page.evaluate(() => { checkpoint(); objects[0].color = '#112233'; render(); });
      await page.evaluate(snapshot => neoSnapEditor.markKept(snapshot), exported.snapshot);
      assert.equal(await page.evaluate(() => neoSnapEditor.hasUnkeptChanges()), true, 'Late save cannot mark newer edits saved');
      await page.locator('[data-tool="text"]').click();
      await page.mouse.click(box.x + 100, box.y + 180);
      await page.locator('.canvas-text-editor').fill('Pending text must survive');
      const textExport = await page.evaluate(() => neoSnapEditor.exportImage());
      assert(textExport.snapshot.includes('Pending text must survive'), 'Uncommitted text is included in save');
      assert.deepEqual(errors, []);
      console.log('PASS editor: new image, successful/failed copy, undo/redo, verified save snapshot, pending text');
      await page.close();
    }
  } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
