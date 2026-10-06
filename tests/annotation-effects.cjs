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
  await new Promise(resolve => server.listen(0,'127.0.0.1',resolve));
  let browser;
  let failures=0;
  try {
    browser=await chromium.launch({headless:true});
    async function run(name, test) {
      const page=await browser.newPage({viewport:{width:1100,height:800}});
      const errors=[];page.on('pageerror',e=>errors.push(e.message));
      try {
        const fixture=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=800;c.height=500;const x=c.getContext('2d');x.fillStyle='white';x.fillRect(0,0,800,500);return c.toDataURL();});
        await page.goto(`http://127.0.0.1:${server.address().port}/editor.html?image=${encodeURIComponent(fixture)}`);
        await page.locator('#loading').waitFor({state:'hidden'});
        await test(page);assert.deepEqual(errors,[]);console.log('PASS '+name);
      } catch(e) {failures++;console.error('FAIL '+name+': '+e.message);} finally {await page.close();}
    }
    async function change(page,id,value) { await page.locator(id).evaluate((el,v)=>{el.value=v;el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));},String(value)); }
    async function text(page,tool,value) {
      await page.locator(`[data-tool="${tool}"]`).click();
      const b=await page.locator('#canvas').boundingBox();await page.mouse.click(b.x+100,b.y+100);
      await page.locator('.canvas-text-editor').fill(value);await page.keyboard.press('Control+Enter');
    }
    await run('5px strokes render without changing text/highlighter defaults',async page=>{
      await page.locator('[data-tool="line"]').click();
      const b=await page.locator('#canvas').boundingBox();await page.mouse.move(b.x+100,b.y+100);await page.mouse.down();await page.mouse.move(b.x+300,b.y+100);await page.mouse.up();
      const pixels=await page.evaluate(()=>{render(false);return [97,98,99,100,101,102,103].map(y=>ctx.getImageData(200,y,1,1).data[1]);});
      assert(pixels[1]<100 && pixels[4]<100 && pixels[0]>100 && pixels[5]<200 && pixels[6]>240,`Line must occupy five pixels (including half-covered edge pixels), not two: ${pixels}`);
      await page.locator('[data-tool="text"]').click();assert.equal(await page.locator('#sizeInput').inputValue(),'6');
      await page.locator('[data-tool="highlight"]').click();assert.equal(await page.locator('#sizeInput').inputValue(),'24');
    });
    await run('shadow controls apply to every annotation and do not leak',async page=>{
      assert.equal(await page.locator('#shadowEnabled').count(),1,'Shared shadow checkbox missing');
      await page.locator('[data-tool="line"]').click();await page.locator('#shadowEnabled').check();
      await page.locator('#shadowSettingsButton').click();await change(page,'#shadowColor','#245bd5');await change(page,'#shadowBlur',0);await change(page,'#shadowOffsetX',0);await change(page,'#shadowOffsetY',12);
      const b=await page.locator('#canvas').boundingBox();await page.mouse.move(b.x+80,b.y+80);await page.mouse.down();await page.mouse.move(b.x+300,b.y+80);await page.mouse.up();
      const pixel=await page.evaluate(()=>{render(false);return [...ctx.getImageData(200,92,1,1).data];});assert.deepEqual(pixel,[36,91,213,255]);
      await page.locator('#shadowEnabled').uncheck();
      assert.deepEqual(await page.evaluate(()=>{render(false);return [...ctx.getImageData(200,92,1,1).data];}),[255,255,255,255]);
      await page.locator('#undoButton').click();assert.equal(await page.evaluate(()=>objects[0].shadow.enabled),true);
      await page.locator('#redoButton').click();assert.equal(await page.evaluate(()=>objects[0].shadow.enabled),false);
      const tools=['pen','highlight','line','arrow','box','ellipse','block','ellipseFill','number','text','bubble','blur'];
      for(const tool of tools) {
        await page.evaluate(tool=>{objects=[tool==='pen'||tool==='highlight'?{tool,points:[{x:100,y:100},{x:230,y:130}],color:'#ef3340',size:5,opacity:1}:{tool,x1:100,y1:100,x2:230,y2:160,text:'ABC',number:1,color:'#ef3340',size:6,opacity:1,blur:16,bubbleWidth:180,bubbleHeight:70}];selected=0;setTool('select');syncControls();render();},tool);
        const before=await page.evaluate(()=>{render(false);return canvas.toDataURL();});
        await page.locator('#shadowEnabled').check();
        const after=await page.evaluate(()=>{render(false);return canvas.toDataURL();});
        assert.notEqual(after,before,`${tool} must render a shadow`);
        assert.equal(await page.evaluate(()=>objects[0].color),'#ef3340','Shadow color must not replace primary color');
      }
      await page.evaluate(()=>{objects=[{tool:'line',x1:50,y1:50,x2:250,y2:50,color:'#ef3340',size:5,shadow:{enabled:true,color:'#245bd5',blur:0,x:0,y:12}},{tool:'line',x1:50,y1:150,x2:250,y2:150,color:'#ef3340',size:5}];render(false);});
      assert.deepEqual(await page.evaluate(()=>[...ctx.getImageData(200,162,1,1).data]),[255,255,255,255]);
    });
    await run('text outline has independent live color/width and exports exact pixels',async page=>{
      await text(page,'text','NEO');
      assert.equal(await page.locator('#outlineEnabled').count(),1,'Text outline checkbox missing');
      await page.locator('#outlineEnabled').check();await page.locator('#textSettingsButton').click();await change(page,'#outlineColor','#245bd5');await change(page,'#outlineWidth',6);
      const result=await page.evaluate(()=>{const out=neoSnapEditor.exportImage();let blue=0;const p=ctx.getImageData(95,95,130,50).data;for(let i=0;i<p.length;i+=4)if(p[i]<70&&p[i+1]<130&&p[i+2]>170)blue++;return {blue,color:objects[0].color,base64:out.base64};});
      assert(result.blue>50,'Outline must render blue glyph-edge pixels');assert.equal(result.color,'#ef3340');assert(result.base64.length>100);
      await page.locator('#undoButton').click();assert.equal(await page.evaluate(()=>objects[0].outline.width),2);
      await page.locator('#redoButton').click();assert.equal(await page.evaluate(()=>objects[0].outline.width),6);
      await page.locator('#shadowEnabled').check();assert.equal(await page.evaluate(()=>objects[0].outline.enabled),true);
      const equal=await page.evaluate(async()=>{render(false);const expected=canvas.toDataURL();const output=await exportBlob();const actual=await new Promise(resolve=>{const r=new FileReader();r.onload=()=>resolve(r.result);r.readAsDataURL(output);});return expected===actual;});assert.equal(equal,true);
    });
    await run('speech bubble creation wrapping move resize edit history and cancel',async page=>{
      assert.equal(await page.locator('[data-tool="bubble"]').count(),1,'Speech bubble tool missing');
      await text(page,'bubble','Neo Snap\n'+'a'.repeat(80));
      let o=await page.evaluate(()=>structuredClone(objects[0]));assert.equal(o.tool,'bubble');assert.equal(o.size,6);
      const layout=await page.evaluate(()=>annotationEffects.bubbleLayout(ctx,objects[0]));assert(layout.lines.length>3,'Long words must wrap');assert(layout.lines.every(line=>{return line.length<80;}));
      await page.locator('[data-tool="select"]').click();let b=await page.locator('#canvas').boundingBox();
      await page.mouse.move(b.x+130,b.y+130);await page.mouse.down();await page.mouse.move(b.x+160,b.y+155);await page.mouse.up();
      let moved=await page.evaluate(()=>structuredClone(objects[0]));assert.equal(moved.x1,o.x1+30);assert.equal(moved.y1,o.y1+25);
      const box=await page.evaluate(()=>resizeBounds(objects[0]));await page.mouse.move(b.x+box.x+box.width,b.y+box.y+box.height);await page.mouse.down();await page.mouse.move(b.x+box.x+150,b.y+box.y+box.height);await page.mouse.up();
      assert.equal(await page.evaluate(()=>objects[0].size),6,'Resize must wrap, not stretch the font');assert.equal(Math.round(await page.evaluate(()=>objects[0].bubbleWidth)),150);
      await page.mouse.dblclick(b.x+moved.x1+25,b.y+moved.y1+25);await page.locator('.canvas-text-editor').fill('Edited bubble');await page.keyboard.press('Control+Enter');
      assert.equal(await page.evaluate(()=>objects[0].text),'Edited bubble');assert.equal(await page.evaluate(()=>objects[0].x1),moved.x1);
      assert.deepEqual(await page.evaluate(()=>annotationEffects.bubbleLayout(ctx,objects[0]).lines.map(line=>line.trim())),['Edited','bubble'],'Wrap ordinary words without splitting them');
      await page.locator('#undoButton').click();assert.equal(await page.evaluate(()=>objects[0].text),o.text);await page.locator('#redoButton').click();assert.equal(await page.evaluate(()=>objects[0].text),'Edited bubble');
      await page.locator('[data-tool="bubble"]').click();await page.mouse.click(b.x+450,b.y+150);await page.locator('.canvas-text-editor').fill('cancel');await page.keyboard.press('Escape');assert.equal(await page.evaluate(()=>objects.length),1);
      assert.equal(await page.locator('[data-tool="bubble"] svg').count(),1,'Tool icon must be bundled');
      fs.mkdirSync(path.resolve(__dirname,'../dist/annotation-preview'),{recursive:true});await page.screenshot({path:path.resolve(__dirname,'../dist/annotation-preview/desktop.png')});
      await page.setViewportSize({width:480,height:720});await page.locator('#textSettingsButton').click();await page.screenshot({path:path.resolve(__dirname,'../dist/annotation-preview/narrow.png')});
      assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'No horizontal page overflow');
    });
    await run('shared palette targets effects independently and preserves a single slider undo',async page=>{
      await page.locator('[data-tool="text"]').click();await change(page,'#colorInput','#245bd5');
      await text(page,'bubble','Blue bubble');assert.equal(await page.evaluate(()=>objects[0].bubbleBorder),'#245bd5','New bubble border follows current primary color until explicitly configured');
      await page.locator('#shadowEnabled').check();await page.locator('#shadowSettingsButton').click();
      await page.locator('[data-effect-color="shadowColor"]').click();await page.locator('#paletteGrid [data-color="#ed3fa2"]').click();
      assert.equal(await page.evaluate(()=>objects[0].shadow.color),'#ed3fa2');assert.equal(await page.locator('#colorInput').inputValue(),'#245bd5');
      const count=await page.evaluate(()=>undoStack.length);
      await page.locator('#shadowBlur').evaluate(el=>{for(const v of [8,12,16]){el.value=v;el.dispatchEvent(new Event('input'));}el.dispatchEvent(new Event('change'));});
      assert.equal(await page.evaluate(()=>undoStack.length),count+1);await page.locator('#undoButton').click();assert.equal(await page.evaluate(()=>objects[0].shadow.blur),6);
      await page.evaluate(()=>{selected=0;syncControls();});await page.locator('#textSettingsButton').click();await page.locator('[data-effect-color="bubbleFill"]').click();await page.locator('#paletteGrid [data-color="#fff9cc"]').click();
      assert.equal(await page.evaluate(()=>objects[0].bubbleFill),'#fff9cc');assert.equal(await page.evaluate(()=>objects[0].color),'#245bd5');
      await page.locator('[data-tool="number"]').click();const b=await page.locator('#canvas').boundingBox();await page.mouse.click(b.x+450,b.y+250);
      assert.equal(await page.evaluate(()=>objects[1].shadow.enabled),true,'Shared preset survives tool changes');
    });
    await run('blur shadow preserves protected region and cropped bubble commits on export',async page=>{
      assert.equal(await page.locator('#shadowEnabled').count(),1,'Shared shadow checkbox missing');
      const safe=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=800;c.height=500;const t=c.getContext('2d');t.fillStyle='white';t.fillRect(0,0,800,500);for(let x=100;x<230;x+=2){t.fillStyle='black';t.fillRect(x,100,1,60);}baseImage=c;objects=[{tool:'blur',x1:100,y1:100,x2:230,y2:160,size:6,color:'#ef3340',opacity:1,blur:16}];render(false);const before=[...ctx.getImageData(100,100,130,60).data];objects[0].shadow={enabled:true,color:'#245bd5',blur:0,x:0,y:12};render(false);const after=ctx.getImageData(100,100,130,60).data;return before.every((v,i)=>v===after[i]);});assert.equal(safe,true,'Shadow must not weaken blur');
      await page.evaluate(()=>{objects=[];cropRect={x:50,y:50,width:500,height:350};canvas.width=500;canvas.height=350;fitCanvas();});
      await page.locator('[data-tool="bubble"]').click();const b=await page.locator('#canvas').boundingBox();await page.mouse.click(b.x+50,b.y+60);await page.locator('.canvas-text-editor').fill('Pending');
      const exported=await page.evaluate(()=>neoSnapEditor.exportImage());assert(exported.base64.length>100);assert.equal(await page.evaluate(()=>objects[0].text),'Pending');assert.equal(await page.evaluate(()=>objects[0].x1),100);
      await page.evaluate(()=>{cropRect={x:0,y:0,width:80,height:350};canvas.width=80;canvas.height=350;objects=[];fitCanvas();});
      const small=await page.locator('#canvas').boundingBox();await page.mouse.click(small.x+20,small.y+50);await page.locator('.canvas-text-editor').fill('longlonglongword');await page.keyboard.press('Control+Enter');const narrow=await page.evaluate(()=>annotationEffects.bubbleLayout(ctx,objects[0]));assert(narrow.width<=80);assert(narrow.lines.length>1);
    });
    await run('cropped shadows keep image coordinates and visual controls stay compact on a long image',async page=>{
      const pixel=await page.evaluate(()=>{cropRect={x:50,y:50,width:500,height:350};canvas.width=500;canvas.height=350;objects=[{tool:'line',x1:100,y1:100,x2:300,y2:100,size:5,color:'#ef3340',opacity:1,shadow:{enabled:true,color:'#245bd5',blur:0,x:0,y:12}}];render(false);return [...ctx.getImageData(150,62,1,1).data];});assert.deepEqual(pixel,[36,91,213,255]);
      await page.evaluate(()=>{const c=document.createElement('canvas');c.width=800;c.height=1800;const t=c.getContext('2d');t.fillStyle='#ffffff';t.fillRect(0,0,800,1800);for(let y=0;y<1800;y+=80){t.fillStyle='#eaf2ff';t.fillRect(0,y,800,1);}baseImage=c;cropRect={x:0,y:0,width:800,height:1800};canvas.width=800;canvas.height=1800;objects=[{tool:'text',x1:70,y1:55,text:'Neo Snap',size:12,color:'#245bd5',opacity:1,outline:{enabled:true,color:'#ffffff',width:3},shadow:{enabled:true,color:'#17201d',blur:6,x:3,y:4}},{tool:'bubble',x1:70,y1:155,text:'ช่องคำพูด\nลากย้ายและแก้ไขข้อความได้',size:6,color:'#17201d',opacity:1,bubbleWidth:350,bubbleFill:'#fff9cc',bubbleBorder:'#245bd5',bubbleBorderWidth:5,shadow:{enabled:true,color:'#3d4248',blur:6,x:4,y:6},outline:{enabled:false,color:'#000000',width:2}}];selected=1;setTool('select');syncControls();updateImageInfo();fitCanvas();});
      fs.mkdirSync(path.resolve(__dirname,'../dist/annotation-preview'),{recursive:true});
      await page.locator('#shadowSettingsButton').click();await page.screenshot({path:path.resolve(__dirname,'../dist/annotation-preview/effects-desktop.png')});
      await page.setViewportSize({width:480,height:720});await page.locator('#textSettingsButton').click();await page.screenshot({path:path.resolve(__dirname,'../dist/annotation-preview/effects-narrow.png')});
      const rectangles=await page.locator('.style-row > :not([hidden])').evaluateAll(els=>els.map(e=>{const r=e.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom};}));
      assert(rectangles.every(r=>r.left>=0&&r.right<=480),'Every visible style control fits inside the narrow viewport');
      await page.locator('#stage').evaluate(el=>{el.scrollTop=500;});await page.waitForTimeout(50);
      assert.equal(await page.locator('#textSettings').isVisible(),false,'Scrolling closes detached settings');
      assert.equal(await page.evaluate(()=>objects[1].shadow.enabled),true,'Scrolling must preserve annotation effects');
    });
    if(failures)throw new Error(`${failures} annotation behavior tests failed`);
  } finally {await browser?.close();await new Promise(resolve=>server.close(resolve));}
})().catch(e=>{console.error(e);process.exitCode=1;});
