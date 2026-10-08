const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../src/SnapCraft/Assets');
(async () => {
  const server = http.createServer((req,res) => {
    const file = path.resolve(root, '.' + new URL(req.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) return res.writeHead(404).end();
    res.setHeader('Content-Type', {'.html':'text/html','.js':'text/javascript','.css':'text/css','.ttf':'font/ttf'}[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  let browser;
  try {
    browser = await chromium.launch({headless:true});
    const page = await browser.newPage({viewport:{width:1280,height:800}});
    const errors=[]; page.on('pageerror', e => errors.push(e.message));
    await page.addInitScript(() => { window.messages=[]; window.chrome={webview:{postMessage: m => messages.push(m)}}; });
    await page.goto(`http://127.0.0.1:${server.address().port}/editor.html?warm=1&product=SnapZy&language=en`);
    await page.waitForFunction(() => messages.some(m=>m.action==='editorPrepared'));
    assert.equal(await page.evaluate(()=>baseImage), null);
    const fixture = await page.evaluate(() => { const image=document.createElement('canvas'); image.width=400; image.height=250;const g=image.getContext('2d');g.fillStyle='#ff0000';g.fillRect(0,0,400,250);return image.toDataURL(); });
    await page.evaluate(url=>neoSnapEditor.openCapture(url), fixture);
    assert.equal(await page.evaluate(()=>messages.filter(m=>m.action==='editorReady').length),1);
    assert.deepEqual(await page.evaluate(()=>[canvas.width,canvas.height]),[400,250]);
    assert.deepEqual(await page.evaluate(()=>{render(false);return [...ctx.getImageData(399,249,1,1).data]}),[255,0,0,255]);
    const region = await page.evaluate(async () => {
      cropDraft={x:30,y:40,width:110,height:70};
      const image=await loadImage('data:image/png;base64,'+neoSnapEditor.exportRegion().base64);
      return [image.width,image.height,objects.length,canvas.width,canvas.height];
    });
    assert.deepEqual(region,[110,70,0,400,250]);
    await page.locator('#workToolsButton').click();
    assert.equal(await page.evaluate(()=>messages.at(-1).action),'productivity');
    assert.equal(await page.locator('#workToolsButton').getAttribute('aria-label'),'Work tools');
    assert.deepEqual(errors,[]);
    console.log('PASS warm capture / original pixels / selected OCR region without cropping / native work-tools message');
  } finally { await browser?.close(); await new Promise(r=>server.close(r)); }
})().catch(e=>{console.error(e);process.exitCode=1});
