const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../src/SnapCraft/Assets');
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
    const page = await browser.newPage({ viewport: { width: 380, height: 320 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => {
      window.sent = [];
      window.chrome = { webview: { postMessage: value => sent.push(value), addEventListener: (_, callback) => { window.receive = callback; } } };
    });
    const origin = `http://127.0.0.1:${server.address().port}`;
    await page.goto(origin + '/launcher.html?product=SnapZy&language=en');
    assert.equal(await page.locator('.identity strong').evaluate(node => node.firstChild.textContent.trim()), 'SnapZy');
    for (const product of ['SnapZy', 'Neo Snap']) {
      for (const language of ['en', 'th']) {
        for (const delayMs of [0, 3000, 5000, 10000]) {
          await page.evaluate(data => receive({ data }), { type: 'settings', productName: product, language, version: '1.0.1', delayMs, openMode: 'window' });
          assert.equal(await page.locator('#delay').getAttribute('data-delayed'), String(delayMs > 0), 'Nonzero capture delay must be visibly active');
          assert.equal(await page.locator('#delay').inputValue(), String(delayMs));
        }
      }
    }
    await page.locator('#delay').selectOption('0');
    assert.equal(await page.locator('#delay').getAttribute('data-delayed'), 'false');
    assert.equal((await page.evaluate(() => sent.at(-1))).delayMs, 0);
    await page.evaluate(() => receive({ data: { type: 'settings', productName: 'SnapZy', language: 'en', version: '1.0.1', delayMs: 0, openMode: 'window' } }));
    const mode = process.argv[2];
    if (!mode || mode === 'language') {
      assert.equal(await page.locator('[data-language-toggle]').count(), 1, 'Use a compact language button, not a dropdown');
      assert.equal(await page.locator('[data-language-select]').count(), 0);
      assert.equal(await page.locator('[data-language-toggle]').textContent(), 'EN');
      await page.locator('[data-language-toggle]').click();
      assert.equal(await page.locator('html').getAttribute('lang'), 'th');
      assert.equal(await page.locator('[data-language-toggle]').textContent(), 'TH');
      assert.deepEqual(await page.evaluate(() => sent.at(-1)), { action: 'language', language: 'th' });
      await page.locator('[data-language-toggle]').click();
      assert.equal(await page.locator('html').getAttribute('lang'), 'en');
      assert.equal(await page.locator('[data-action="scroll"] span').textContent(), 'Scrolling');
      console.log('PASS compact EN/TH toggle, immediate translation and persistence message');
    }
    if (!mode || mode === 'hotkey') {
      await page.locator('#shortcutButton').click();
      await page.locator('#saveShortcut').click();
      assert.equal(await page.locator('#shortcutSavedDialog').count(), 1, 'Hotkey save needs a visible confirmation popup');
      assert.equal(await page.locator('#shortcutSavedDialog').isVisible(), false, 'Do not announce success before native registration/persistence');
      await page.evaluate(() => receive({ data: { type: 'status', text: 'Shortcut unavailable', error: true } }));
      assert.equal(await page.locator('#shortcutSavedDialog').isVisible(), false);
      assert.equal(await page.locator('#shortcutDialog').isVisible(), true);
      await page.evaluate(() => receive({ data: { type: 'settings', productName: 'SnapZy', language: 'en', version: '1.0.1', delayMs: 0, openMode: 'window' } }));
      assert.equal(await page.locator('#shortcutDialog').isVisible(), true, 'Background settings must not dismiss the editor');
      await page.evaluate(() => receive({ data: { type: 'hotkeySaved', mode: 'launcher' } }));
      assert.equal(await page.locator('#shortcutDialog').isVisible(), false);
      assert.equal(await page.locator('#shortcutSavedDialog').isVisible(), true);
      assert.match(await page.locator('#shortcutSavedDialog').textContent(), /Shortcut saved successfully/);
      await page.screenshot({ path: path.resolve(__dirname, '../dist/snapzy-hotkey-saved.png') });
      await page.locator('#closeShortcutSaved').click();
      console.log('PASS hotkey confirmation only after successful native acknowledgement');
    }
    if (!mode || mode === 'help' || mode === 'icons') {
      const image = await page.evaluate(() => { const canvas = document.createElement('canvas'); canvas.width = 900; canvas.height = 500; const ctx = canvas.getContext('2d'); ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, 900, 500); return canvas.toDataURL(); });
      await page.setViewportSize({ width: 1200, height: 800 });
      await page.goto(origin + '/editor.html?product=SnapZy&language=en&version=1.0.1&image=' + encodeURIComponent(image));
      await page.locator('#loading').waitFor({ state: 'hidden' });
      if (!mode || mode === 'help') {
        assert.equal(await page.locator('#editorHelpButton').count(), 1, 'Editor needs a Help button');
        await page.locator('[data-tool="arrow"]').click();
        await page.locator('#editorHelpButton').click();
        assert.equal(await page.locator('#editorHelpDialog').isVisible(), true);
        const described = await page.locator('[data-help-for]').evaluateAll(nodes => nodes.flatMap(node => node.dataset.helpFor.split(' ')));
        const commands = await page.locator('.topbar button, .tool-row button, .style-row button, .style-row input, #bubbleTailHandle, #curveHandle, #applyLayerSize').evaluateAll(nodes => nodes.map(node => node.id || node.dataset.tool || node.dataset.stroke || node.dataset.view || node.getAttribute('aria-controls')));
        for (const id of commands) assert(described.includes(id), `Help must explain ${id}`);
        assert.match(await page.locator('#editorHelpDialog').textContent(), /Speech bubble/);
        assert.match(await page.locator('#editorHelpDialog').textContent(), /original images/);
        await page.screenshot({ path: path.resolve(__dirname, '../dist/snapzy-editor-help.png') });
        await page.keyboard.press('Escape');
        assert.equal(await page.locator('#editorHelpDialog').isVisible(), false);
        assert.equal(await page.evaluate(() => activeTool), 'arrow', 'Closing Help must preserve current drawing tool');
        await page.locator('[data-language-toggle]').click();
        await page.locator('#editorHelpButton').click();
        assert.match(await page.locator('#editorHelpDialog').textContent(), /ช่องคำพูด/);
        assert.doesNotMatch(await page.locator('#editorHelpDialog').textContent(), /Speech bubble/);
        await page.setViewportSize({ width: 760, height: 560 });
        const box = await page.locator('#editorHelpDialog').boundingBox();
        assert(box.x >= 0 && box.y >= 0 && box.x + box.width <= 760 && box.y + box.height <= 560);
        await page.screenshot({ path: path.resolve(__dirname, '../dist/snapzy-editor-help-th.png') });
        await page.locator('#closeEditorHelp').click();
        console.log('PASS bilingual Help covers every toolbar command and preserves editing state');
      }
      if (!mode || mode === 'icons') {
        assert.equal(await page.locator('.brand-link img').getAttribute('src'), 'icons/snapzy.svg', 'Snapzy must display approved capture icon A');
        assert.equal(await page.locator('.brand-link img').evaluate(img => img.complete && img.naturalWidth > 0), true, 'The actual logo asset must render');
        assert.equal(fs.existsSync(path.join(root, 'icons/snapzy.ico')), true);
        const csproj = fs.readFileSync(path.resolve(root, '../SnapCraft.csproj'), 'utf8');
        assert.match(csproj, /<ApplicationIcon>Assets\\icons\\snapzy.ico<\/ApplicationIcon>/);
        console.log('PASS Snapzy web and executable icon assets are isolated');
      }
    }
    if (!mode || mode === 'branding') {
      await page.goto(origin + '/video-preview.html?product=SnapZy');
      assert.equal(await page.locator('.brand-link b').textContent(), 'SnapZy');
      assert.equal(await page.locator('.brand-link img').getAttribute('src'), 'icons/snapzy.svg');
      assert.match(await page.title(), /^SnapZy/);
      console.log('PASS SnapZy preview display name and approved icon');
    }
    assert.deepEqual(errors, [], 'No browser runtime errors');
  } finally { await browser?.close(); await new Promise(resolve => server.close(resolve)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
