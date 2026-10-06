const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../src/SnapCraft/Assets');
(async () => {
  const server = http.createServer((req, res) => {
    const file = path.resolve(root, '.' + new URL(req.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) return res.writeHead(404).end();
    res.setHeader('Content-Type', {'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.png':'image/png','.ttf':'font/ttf'}[path.extname(file)] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({headless:true});
    const page = await browser.newPage({viewport:{width:1100,height:800}});
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const fixture = await page.evaluate(() => {
      const c = document.createElement('canvas'); c.width=800; c.height=500;
      const x=c.getContext('2d'); x.fillStyle='white'; x.fillRect(0,0,800,500); return c.toDataURL();
    });
    await page.goto(`http://127.0.0.1:${server.address().port}/editor.html?image=${encodeURIComponent(fixture)}`);
    await page.locator('#loading').waitFor({state:'hidden'});
    const box = await page.locator('#canvas').boundingBox();
    await page.locator('[data-tool="number"]').click();
    for(let i=0;i<3;i++) await page.mouse.click(box.x+80+i*100,box.y+80);
    await page.locator('#deleteButton').click();
    await page.mouse.click(box.x+380,box.y+80);
    assert.deepEqual(await page.evaluate(() => objects.map(o=>o.number)),[1,2,3],'Deleting latest number must reuse it');
    await page.locator('#undoButton').click();
    await page.mouse.click(box.x+480,box.y+80);
    assert.deepEqual(await page.evaluate(() => objects.map(o=>o.number)),[1,2,3],'Undo also determines the next number');
    await page.evaluate(()=>{selected=1;}); await page.locator('#deleteButton').click();
    await page.mouse.click(box.x+580,box.y+80);
    assert.deepEqual(await page.evaluate(() => objects.map(o=>o.number)),[1,3,4],'Deleting a middle number must not duplicate the highest');
    await page.locator('#undoButton').click(); await page.locator('#redoButton').click();
    await page.mouse.click(box.x+680,box.y+80);
    assert.deepEqual(await page.evaluate(() => objects.map(o=>o.number)),[1,3,4,5],'Redo must restore the highest number before continuing');
    await page.locator('#clearButton').click(); await page.mouse.click(box.x+80,box.y+80);
    assert.deepEqual(await page.evaluate(() => objects.map(o=>o.number)),[1],'Clear restarts numbering');
    console.log('PASS numbering: delete latest, delete middle, undo, clear');
    await page.locator('#clearButton').click();
    for(const tool of ['pen','line','arrow','box','ellipse']) {
      await page.evaluate(tool=>setTool(tool),tool);
      await page.mouse.move(box.x+100,box.y+150); await page.mouse.down();
      await page.mouse.move(box.x+500,box.y+150+(tool==='box'||tool==='ellipse'?100:0),{steps:8}); await page.mouse.up();
      assert.equal(await page.evaluate(()=>objects.at(-1).size),5,`${tool} defaults to 5px`);
    }
    await page.locator('#clearButton').click(); await page.evaluate(()=>setTool('line'));
    await page.mouse.move(box.x+100,box.y+200); await page.mouse.down(); await page.mouse.move(box.x+500,box.y+200); await page.mouse.up();
    for(const pattern of ['dashed','dotted','solid']) {
      await page.locator(`[data-stroke="${pattern}"]`).click();
      assert.equal(await page.evaluate(()=>objects[0].stroke),pattern,'Pattern changes the selected object');
      assert.equal(await page.locator(`[data-stroke="${pattern}"]`).getAttribute('aria-pressed'),'true');
      const gaps=await page.evaluate(()=>{render(false);let gaps=0;for(let x=102;x<498;x++)if(ctx.getImageData(x,200,1,1).data[1]>200)gaps++;render();return gaps;});
      assert(pattern==='solid'?gaps===0:gaps>50,`${pattern} must render the appropriate gaps, not only change a label`);
    }
    await page.locator('#undoButton').click(); assert.equal(await page.evaluate(()=>objects[0].stroke),'dotted');
    await page.locator('#redoButton').click(); assert.equal(await page.evaluate(()=>objects[0].stroke),'solid');
    await page.evaluate(()=>setTool('highlight')); assert.equal(await page.locator('#sizeInput').inputValue(),'24');
    assert.equal(await page.locator('[data-stroke="dashed"]').isDisabled(),true);
    await page.evaluate(()=>setTool('text')); assert.equal(await page.locator('#sizeInput').inputValue(),'6');
    assert.deepEqual(errors,[]);
    fs.mkdirSync(path.resolve(__dirname,'../dist/stroke-preview'),{recursive:true});
    await page.evaluate(()=>setTool('line')); await page.locator('[data-stroke="dashed"]').click();
    await page.mouse.move(box.x+100,box.y+270);await page.mouse.down();await page.mouse.move(box.x+500,box.y+270);await page.mouse.up();
    await page.locator('[data-stroke="dotted"]').click();
    await page.mouse.move(box.x+100,box.y+340);await page.mouse.down();await page.mouse.move(box.x+500,box.y+340);await page.mouse.up();
    await page.evaluate(()=>setTool('select'));await page.keyboard.press('Escape');
    await page.screenshot({path:path.resolve(__dirname,'../dist/stroke-preview/editor.png')});
    console.log('PASS strokes: 5px defaults, live patterns, rendered gaps, undo/redo, non-stroke tools');
  } finally {await browser?.close();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});
