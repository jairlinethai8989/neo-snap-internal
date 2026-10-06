const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../../../outputs/chrome-capture-extension');
(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1000, height: 800 } });
    for (const file of ['scroll-motion.js', 'stitch.js']) await page.addScriptTag({ content: fs.readFileSync(path.join(root, file), 'utf8') });
    const result = await page.evaluate(async () => {
      const width = 480, height = 640, header = 260, shift = 150;
      const body = document.createElement('canvas'); body.width = width; body.height = 1400;
      const b = body.getContext('2d'); b.fillStyle = '#fff'; b.fillRect(0, 0, width, 1400); b.font = '14px Arial';
      for (let y = 0; y < 1400; y += 29) {
        b.fillStyle = '#151515'; b.fillText(`Row ${y} / ${y * 719 % 997}`, 10 + y % 43, y + 18);
        b.fillStyle = '#ccc'; b.fillRect(0, y + 25, width, 1);
      }
      const urls = [], shifts = [0], frames = [];
      for (let index = 0; index < 5; index += 1) {
        const tile = document.createElement('canvas'); tile.width = width; tile.height = height;
        const t = tile.getContext('2d'); t.fillStyle = '#155eef'; t.fillRect(0, 0, width, header);
        t.fillStyle = '#fff'; t.font = '18px Arial'; t.fillText('FROZEN HEADERS', 12, 45);
        t.drawImage(body, 0, index * shift, width, height - header, 0, header, width, height - header);
        urls.push(tile.toDataURL());
        frames.push(await readScrollFrame(urls.at(-1), { x: 0, y: 0, width, height, viewportWidth: width, viewportHeight: height }));
        if (index) shifts.push(matchScrollFrames(frames[index - 1], frames[index], shift));
      }
      const stitched = await stitchScrollTiles(urls, { x: 0, y: 0, width, height, viewportWidth: width, viewportHeight: height }, shifts);
      const image = new Image(); image.src = stitched; await image.decode();
      const output = document.createElement('canvas'); output.width = image.width; output.height = image.height;
      const out = output.getContext('2d'); out.drawImage(image, 0, 0);
      const actual = out.getImageData(0, header, width, image.height - header).data;
      const expected = b.getImageData(0, 0, width, image.height - header).data;
      let errors = 0;
      for (let i = 0; i < actual.length; i += 1) if (actual[i] !== expected[i]) errors += 1;
      return { shifts, height: image.height, errors, header: detectScrollHeader(frames[0], frames[1]), stationary: scrollFrameChanged(frames[0], frames[0]) };
    });
    assert.deepEqual(result.shifts, [0, 150, 150, 150, 150]);
    assert.equal(result.height, 1240);
    assert.equal(result.errors, 0, 'Every body pixel must be preserved with header only once');
    assert(result.header >= 260 && result.header < 290);
    assert.equal(result.stationary, false);
    await page.setContent('<canvas width="600" height="600" style="position:absolute;left:30px;top:60px"></canvas><div class="scroll" style="position:absolute;left:620px;top:320px;width:10px;height:340px;overflow-y:scroll"><div style="height:3000px"></div></div>');
    await page.addScriptTag({ content: fs.readFileSync(path.join(root, 'scroll-controller.js'), 'utf8') });
    await page.evaluate(() => { window.choice = __snapcraftScroll.choose(); });
    await page.mouse.click(300, 500);
    const crop = await page.evaluate(() => choice);
    assert.equal(crop.y, 60, 'Capture starts at frozen header, not scrollbar top');
    assert.equal(crop.height, 600);
    assert.equal(crop.proxy, true);
    assert.equal((await page.evaluate(() => __snapcraftScroll.bounds())).y, 60);
    await page.evaluate(() => __snapcraftScroll.cleanup());
    await page.setContent('<div role="grid" style="position:absolute;left:30px;top:60px;width:600px;height:600px"><div style="height:260px;background:blue">FROZEN</div><div style="height:340px;overflow-y:auto"><div style="height:3000px">BODY</div></div></div>');
    await page.addScriptTag({ content: fs.readFileSync(path.join(root, 'scroll-controller.js'), 'utf8') });
    await page.evaluate(() => { window.choice = __snapcraftScroll.choose(); });
    await page.mouse.click(300, 500);
    const grid = await page.evaluate(() => choice);
    assert.equal(grid.y, 60, 'Separate DOM frozen header is included');
    assert.equal(grid.height, 600);
    await page.evaluate(() => __snapcraftScroll.cleanup());
    console.log('PASS frozen scroll: 260px pinned header, exact body pixels, proxy header bounds, stationary detection');
  } finally { await browser.close(); }
})().catch((error) => { console.error(error); process.exitCode = 1; });
