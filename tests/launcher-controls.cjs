const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const roots = { windows: path.resolve(__dirname, '../src/SnapCraft/Assets'), extension: path.resolve(__dirname, '../../../outputs/chrome-capture-extension') };
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.ttf': 'font/ttf' };
(async () => {
  const server = http.createServer((req, res) => {
    const [variant, ...parts] = new URL(req.url, 'http://localhost').pathname.slice(1).split('/');
    const root = roots[variant], file = root && path.resolve(root, ...parts);
    if (!file || !file.startsWith(root + path.sep) || !fs.existsSync(file)) { res.writeHead(404); res.end(); return; }
    res.setHeader('Content-Type', types[path.extname(file)] || 'application/octet-stream'); fs.createReadStream(file).pipe(res);
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ headless: true });
    const origin = `http://127.0.0.1:${server.address().port}`;
    const page = await browser.newPage({ viewport: { width: 466, height: 200 } });
    const errors = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await page.addInitScript(() => {
      window.sent = []; window.chrome = { webview: { postMessage: (value) => sent.push(value), addEventListener: (_, callback) => { window.receive = callback; } } };
    });
    await page.goto(origin + '/windows/launcher.html');
    await page.evaluate(() => receive({ data: { type: 'settings', delayMs: 0, openMode: 'window', version: '0.1.10', developer: 'jairlinethai', cpuAvailable: false } }));
    assert.equal(await page.locator('#version').textContent(), 'v0.1.10');
    await page.locator('#aboutButton').click();
    assert.equal(await page.locator('#aboutDialog').isVisible(), true);
    assert.equal(await page.locator('#developer').textContent(), 'jairlinethai');
    assert.equal(await page.locator('#installCpu').isVisible(), true);
    await page.screenshot({ path: path.resolve(__dirname, '../dist/launcher-about.png') });
    await page.locator('#closeAbout').click();
    await page.locator('#shortcutButton').click();
    await page.evaluate(() => receive({ data: { type: 'cpuAvailability', value: true } }));
    assert.equal(await page.locator('#shortcutDialog').isVisible(), true, 'Background recorder discovery must not close shortcut editing');
    assert.equal(await page.locator('#installCpu').evaluate((button) => button.hidden), true);
    await page.locator('#hotkeyAlt').check();
    await page.locator('#hotkeyKey').selectOption('78');
    await page.locator('#saveShortcut').click();
    assert.deepEqual(await page.evaluate(() => sent.findLast(item => item.action === 'hotkey')), { action: 'hotkey', mode: 'launcher', modifiers: 7, key: 78, enabled: true });
    await page.evaluate(() => receive({ data: { type: 'status', text: 'คีย์ลัดถูกใช้แล้ว', error: true } }));
    assert.equal(await page.locator('#shortcutDialog').isVisible(), true);
    assert.match(await page.locator('#hotkeyStatus').textContent(), /คีย์ลัด/);
    await page.evaluate(() => receive({ data: { type: 'settings', delayMs: 0, openMode: 'window', version: '0.1.10', developer: 'jairlinethai', hotkeyModifiers: 7, hotkeyKey: 78 } }));
    assert.equal(await page.locator('#shortcutDialog').isVisible(), true);
    await page.evaluate(() => receive({ data: { type: 'hotkeySaved', mode: 'launcher' } }));
    assert.equal(await page.locator('#shortcutSavedDialog').isVisible(), true);
    await page.locator('#closeShortcutSaved').click();
    for (const mode of ['area', 'window', 'scroll', 'video']) {
      await page.locator('#shortcutButton').click();
      await page.locator('#hotkeyMode').selectOption(mode);
      await page.locator('#saveShortcut').click();
      assert.equal((await page.evaluate(() => sent.findLast(item => item.action === 'hotkey'))).mode, mode);
      await page.locator('#closeShortcut').click();
    }
    await page.evaluate(() => receive({ data: { type: 'videoPrompt' } }));
    assert.equal(await page.locator('#audioDialog').isVisible(), true);
    assert.equal(await page.locator('.actions .active').getAttribute('data-action'), 'video');
    await page.locator('#cancelAudio').click();
    for (const mode of ['area', 'window', 'scroll']) {
      await page.locator(`[data-action="${mode}"]`).click();
      assert.equal(await page.locator('.actions .active').getAttribute('data-action'), mode);
      assert.equal(await page.locator('.actions .active').getAttribute('aria-pressed'), 'true');
    }
    await page.evaluate(() => receive({ data: { type: 'busy', value: true } }));
    assert.equal(await page.locator('[data-action="scroll"]').isDisabled(), true);
    await page.evaluate(() => receive({ data: { type: 'busy', value: false } }));
    await page.screenshot({ path: path.resolve(__dirname, '../dist/launcher-active.png') });
    const openAudio = async () => {
      await page.locator('[data-action="video"]').click();
      await page.locator('[data-video="videoScreen"]').click();
      assert.equal(await page.locator('#audioDialog').isVisible(), true);
    };
    const videoCount = () => page.evaluate(() => sent.filter((item) => item.action.startsWith('video')).length);
    await openAudio(); assert.equal(await videoCount(), 0);
    await page.screenshot({ path: path.resolve(__dirname, '../dist/launcher-audio.png') });
    await page.locator('#cancelAudio').click(); assert.equal(await videoCount(), 0);
    await openAudio(); await page.locator('#silentVideo').click();
    assert.deepEqual(await page.evaluate(() => sent.findLast(item => item.action === 'videoScreen')), { action: 'videoScreen', systemAudio: false, microphoneAudio: false });
    await openAudio(); await page.locator('#confirmAudio').click();
    assert.deepEqual(await page.evaluate(() => sent.findLast(item => item.action === 'videoScreen')), { action: 'videoScreen', systemAudio: true, microphoneAudio: false });
    await openAudio(); await page.locator('#microphoneAudio').check(); await page.locator('#confirmAudio').click();
    assert.deepEqual(await page.evaluate(() => sent.findLast(item => item.action === 'videoScreen')), { action: 'videoScreen', systemAudio: true, microphoneAudio: true });
    assert.deepEqual(errors, []);
    await page.close();
    console.log('PASS Windows launcher: active mode, version/About, audio choice/cancel/silent/system/microphone');

    const popup = await browser.newPage({ viewport: { width: 272, height: 240 } });
    await popup.addInitScript(() => {
      window.messages = []; window.chrome = {
        runtime: { getManifest: () => ({ version: '1.5.7' }), getURL: (value) => value, sendMessage: async (message) => { messages.push(message); return { started: false, error: 'Test keeps popup open' }; } },
        tabs: { query: async () => [{ id: 1, windowId: 1, url: 'https://example.com' }], create: async () => {} },
        storage: { local: { get: async () => ({}), set: async (values) => { window.saved = values; } } }
      };
    });
    await popup.goto(origin + '/extension/popup.html');
    for (const id of ['customCapture', 'visibleCapture', 'fullCapture']) {
      await popup.locator('#' + id).click();
      assert.equal(await popup.locator('.compact-tools .active').getAttribute('id'), id);
    }
    await popup.locator('#aboutButton').click();
    assert.equal(await popup.locator('#aboutVersion').textContent(), '1.5.7');
    assert.match(await popup.locator('#aboutDialog').textContent(), /jairlinethai/);
    await popup.screenshot({ path: path.resolve(__dirname, '../dist/extension-about.png') });
    await popup.close();
    console.log('PASS extension popup: capture modes, version/About');

    const recorder = await browser.newPage({ viewport: { width: 1100, height: 800 } });
    await recorder.addInitScript(() => {
      window.mediaCalls = [];
      window.MediaRecorder = class {
        static isTypeSupported() { return true; }
        constructor() { this.state = 'inactive'; }
        start() { this.state = 'recording'; }
        stop() { this.state = 'inactive'; this.ondataavailable?.({ data: new Blob(['test']) }); this.onstop?.(); }
      };
      const track = (kind) => ({ kind, stop() {}, addEventListener() {} });
      navigator.mediaDevices.getDisplayMedia = async (options) => {
        mediaCalls.push(options); const video = track('video'), audio = options.audio ? [track('audio')] : [];
        return { getTracks: () => [video, ...audio], getVideoTracks: () => [video], getAudioTracks: () => audio };
      };
    });
    await recorder.goto(origin + '/extension/recorder.html');
    await recorder.locator('#startButton').click(); await recorder.locator('#cancelAudio').click();
    assert.equal(await recorder.evaluate(() => mediaCalls.length), 0);
    await recorder.locator('#startButton').click(); await recorder.locator('#silentVideo').click();
    await recorder.locator('#countdown').waitFor({ state: 'visible' });
    assert.equal(await recorder.locator('#countdownValue').textContent(), '3');
    await recorder.locator('#cancelCountdown').click();
    await recorder.locator('#intro').waitFor({ state: 'visible' });
    assert.equal(await recorder.evaluate(() => recorder), null);
    await recorder.locator('#startButton').click(); await recorder.locator('#silentVideo').click();
    await recorder.locator('#recording').waitFor({ state: 'visible' });
    assert.equal(await recorder.evaluate(() => mediaCalls.at(-1).audio), false);
    await recorder.locator('#cancelButton').click();
    await recorder.locator('#startButton').click(); await recorder.locator('#confirmAudio').click();
    await recorder.locator('#recording').waitFor({ state: 'visible' });
    assert.equal(await recorder.evaluate(() => mediaCalls.at(-1).audio), true);
    await recorder.locator('#cancelButton').click();
    await recorder.close();
    console.log('PASS extension recorder: asks first, cancel starts nothing, silent/system audio constraints');

    const scroll = await browser.newPage({ viewport: { width: 1000, height: 700 } });
    await scroll.setContent('<div id="a" style="position:absolute;left:30px;top:70px;width:350px;height:500px;overflow-y:auto"><div style="height:1500px">A</div></div><div id="b" style="position:absolute;left:420px;top:150px;width:300px;height:350px;overflow-y:auto"><div style="height:1800px">B</div></div>');
    await scroll.addScriptTag({ content: fs.readFileSync(path.join(roots.extension, 'scroll-controller.js'), 'utf8') });
    await scroll.evaluate(() => { window.choice = __snapcraftScroll.choose(); });
    for (const [x, y, left, top, width, height] of [[200, 200, 30, 70, 350, 500], [550, 250, 420, 150, 300, 350]]) {
      await scroll.mouse.move(x, y);
      const box = await scroll.locator('#__snapcraft_scroll_pick > div').first().boundingBox();
      assert.deepEqual([box.x, box.y, box.width, box.height], [left, top, width, height]);
      assert.equal(await scroll.locator('#__snapcraft_scroll_pick > div').first().evaluate((el) => getComputedStyle(el).borderStyle), 'solid');
    }
    await scroll.screenshot({ path: path.resolve(__dirname, '../dist/extension-scroll-hover.png') });
    await scroll.mouse.move(850, 600);
    assert.equal(await scroll.locator('#__snapcraft_scroll_pick > div').first().evaluate((el) => getComputedStyle(el).borderStyle), 'dashed');
    await scroll.keyboard.press('Escape'); await scroll.close();
    console.log('PASS extension scroll hover: distinct nested bounds, unavailable-state border, Esc');
  } finally {
    await browser?.close(); await new Promise((resolve) => server.close(resolve));
  }
})().catch((error) => { console.error(error); process.exitCode = 1; });
