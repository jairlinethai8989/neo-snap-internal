const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(process.env.EDITOR_ASSETS_ROOT || path.join(__dirname, '../src/SnapCraft/Assets'));
(async () => {
  const server = http.createServer((req, res) => {
    const file = path.resolve(root, '.' + new URL(req.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) return res.writeHead(404).end();
    res.setHeader('Content-Type', {'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml'}[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  let browser;
  try {
    browser = await chromium.launch({headless:true});
    const page = await browser.newPage({viewport:{width:1200,height:800}});
    const errors=[]; page.on('pageerror', e=>errors.push(e.message));
    page.on('console', m=>{if(m.type()==='warn') errors.push(m.text());});
    await page.addInitScript(() => { window.messages=[]; window.chrome={webview:{postMessage:m=>messages.push(m)}}; });
    const screenshots=path.resolve(__dirname,'../dist/editor-ocr-check'); fs.mkdirSync(screenshots,{recursive:true});
    for (const product of ['SnapZy','Neo Snap']) {
      await page.setViewportSize({width:1200,height:800});
      await page.goto(`http://127.0.0.1:${server.address().port}/editor.html?warm=1&product=${encodeURIComponent(product)}&language=en&version=${product==='SnapZy'?'1.0.1':'0.1.21'}`);
      await page.waitForFunction(()=>messages.some(m=>m.action==='editorPrepared'));
      const button=page.locator('.top-actions #ocrButton');
      assert.equal(await button.count(),1,'OCR must be a dedicated top menu command');
      await button.click();
      assert.equal(await page.evaluate(()=>messages.some(m=>m.action==='ocr')),false,'Empty warm editor must not open OCR');
      await page.evaluate(async ()=>{
        const image=document.createElement('canvas'); image.width=400;image.height=250;
        const g=image.getContext('2d');g.fillStyle='#fff';g.fillRect(0,0,400,250);g.fillStyle='#111';g.font='24px sans-serif';g.fillText('OCR fixture',20,40);
        await neoSnapEditor.openCapture(image.toDataURL());
      });
      await page.locator('[data-tool="pen"]').click();
      for (const language of ['en','th']) {
        await page.evaluate(value=>editorI18n.setLanguage(value),language);
        const label=language==='en'?'Scan Text':'อ่านข้อความ OCR';
        assert.equal(await button.getAttribute('aria-label'),label);
        assert.equal(await button.getAttribute('title'),label);
        assert.equal(await button.locator('svg[data-lucide="scan-text"]').count(),1);
        assert(await button.locator('svg').evaluate(s=>{const b=s.getBBox();return b.width>0&&b.height>0;}),'OCR icon must be visibly drawn');
        await button.click();
        assert.equal(await page.evaluate(()=>messages.at(-1).action),'ocr');
        assert.equal(await page.evaluate(()=>activeTool),'pen','OCR must not replace the selected drawing tool');
        await page.locator('#editorHelpButton').click();
        const help=page.locator('[data-help-for="ocrButton"]');
        assert.equal(await help.locator('b').textContent(),label);
        assert.equal(await help.locator('svg[data-lucide="scan-text"]').count(),1);
        assert.match(await help.locator('p').textContent(),language==='en'?/selected region/:/พื้นที่ที่เลือก/);
        assert.doesNotMatch(await page.locator('[data-help-for="workToolsButton"]').textContent(),/read selected text|อ่านข้อความ/,'OCR is no longer hidden inside Work tools');
        await page.keyboard.press('Escape');
        for(const width of [1200,760,380]) {
          await page.setViewportSize({width,height:600});
          const boxes=await page.locator('.top-actions > button').evaluateAll(nodes=>nodes.map(n=>{const r=n.getBoundingClientRect();return {x:r.x,y:r.y,right:r.right,bottom:r.bottom};}));
          for(let i=0;i<boxes.length;i++) {
            const b=boxes[i];assert(b.x>=0&&b.right<=width,'Top commands must fit without horizontal clipping');
            for(const a of boxes.slice(0,i)) assert(b.x>=a.right||b.right<=a.x||b.y>=a.bottom||b.bottom<=a.y,'Top commands must not overlap');
          }
          await page.screenshot({path:path.join(screenshots,`${product.replace(/\s/g,'-')}-${language}-${width}.png`)});
        }
        await page.setViewportSize({width:1200,height:800});
      }
      await page.evaluate(()=>{delete window.chrome.webview;});
      await button.click();
      assert.match(await page.locator('#toast').textContent(),/Windows/,'Standalone browser must explain the native OCR requirement');
    }
    assert.deepEqual(errors,[]);
    console.log('PASS dedicated OCR: both products / localized scan icon and Help / direct native message / preserved drawing state / compact layouts');
  } finally {await browser?.close();await new Promise(r=>server.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
