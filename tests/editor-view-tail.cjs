const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require(process.env.PLAYWRIGHT_PATH||'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../src/SnapCraft/Assets');
(async()=>{
  const server=http.createServer({maxHeaderSize:512*1024},(req,res)=>{const file=path.resolve(root,'.'+new URL(req.url,'http://localhost').pathname);if(!file.startsWith(root+path.sep)||!fs.existsSync(file))return res.writeHead(404).end();res.setHeader('Content-Type',{'.html':'text/html','.js':'text/javascript','.css':'text/css','.svg':'image/svg+xml','.ttf':'font/ttf'}[path.extname(file)]||'application/octet-stream');fs.createReadStream(file).pipe(res);});
  await new Promise(r=>server.listen(0,'127.0.0.1',r));let browser,failures=0;
  try{
    browser=await chromium.launch({headless:true});
    async function run(name,test){const page=await browser.newPage({viewport:{width:1100,height:800}});const errors=[];page.on('pageerror',e=>errors.push(e.message));try{
      const fixture=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=1600;c.height=1400;const t=c.getContext('2d');t.fillStyle='white';t.fillRect(0,0,c.width,c.height);t.fillStyle='#245bd5';for(let x=0;x<1600;x+=2)t.fillRect(x,1200,1,60);return c.toDataURL();});
      await page.goto(`http://127.0.0.1:${server.address().port}/editor.html?image=${encodeURIComponent(fixture)}`);await page.locator('#loading').waitFor({state:'hidden'});await test(page);assert.deepEqual(errors,[]);console.log('PASS '+name);
    }catch(e){failures++;console.error('FAIL '+name+': '+e.message.slice(0,600));}finally{await page.close();}}
    await run('100% view preserves native backing pixels export and history',async page=>{
      assert.equal(await page.locator('[data-view="actual"]').count(),1,'100% view control is missing');
      await page.evaluate(()=>neoSnapEditor.markKept(imageSnapshot()));
      const before=await page.evaluate(()=>({png:neoSnapEditor.exportImage().base64,history:undoStack.length,dirty:neoSnapEditor.hasUnkeptChanges(),width:canvas.width,height:canvas.height}));
      assert.equal(before.dirty,false);
      assert((await page.locator('#canvas').boundingBox()).width<1600);
      await page.locator('[data-view="actual"]').click();assert.equal((await page.locator('#canvas').boundingBox()).width,1600);
      assert.equal(await page.locator('[data-view="actual"]').getAttribute('aria-pressed'),'true');
      await page.locator('#stage').evaluate(el=>{el.scrollLeft=300;el.scrollTop=900;});
      const after=await page.evaluate(()=>({png:neoSnapEditor.exportImage().base64,history:undoStack.length,dirty:neoSnapEditor.hasUnkeptChanges(),width:canvas.width,height:canvas.height}));assert.deepEqual(after,before);
      const decoded=await page.evaluate(async()=>{const bitmap=await createImageBitmap(await exportBlob());const c=document.createElement('canvas');c.width=bitmap.width;c.height=bitmap.height;const t=c.getContext('2d');t.drawImage(bitmap,0,0);return {w:bitmap.width,h:bitmap.height,a:[...t.getImageData(300,1220,1,1).data],b:[...t.getImageData(301,1220,1,1).data]};});
      assert.deepEqual(decoded,{w:1600,h:1400,a:[36,91,213,255],b:[255,255,255,255]},'One-pixel details survive view changes and PNG export');
      await page.locator('[data-view="fit"]').click();assert((await page.locator('#canvas').boundingBox()).width<1600);
      const centerBefore=await page.evaluate(()=>{const a=stage.getBoundingClientRect(),b=canvas.getBoundingClientRect();return [(a.left+stage.clientWidth/2-b.left)*canvas.width/b.width,(a.top+stage.clientHeight/2-b.top)*canvas.height/b.height];});
      await page.locator('[data-view="actual"]').click();const centerAfter=await page.evaluate(()=>{const a=stage.getBoundingClientRect(),b=canvas.getBoundingClientRect();return [(a.left+stage.clientWidth/2-b.left)*canvas.width/b.width,(a.top+stage.clientHeight/2-b.top)*canvas.height/b.height];});assert(Math.abs(centerBefore[1]-centerAfter[1])<3,'Zoom preserves the visible image center');
    });
    async function setupBubble(page){await page.evaluate(()=>{objects=[{tool:'bubble',x1:200,y1:180,bubbleWidth:260,bubbleHeight:100,text:'Neo Snap',size:6,color:'#17201d',opacity:1,bubbleFill:'#fff9cc',bubbleBorder:'#245bd5',bubbleBorderWidth:5,shadow:{enabled:true,color:'#17201d',blur:4,x:3,y:3}}];selected=0;setTool('select');syncControls();render();});}
    async function dragTip(page,x,y){const h=await page.locator('#bubbleTailHandle').boundingBox();assert(h,'The selected bubble pointer has a visible handle');const b=await page.locator('#canvas').boundingBox();const model=await page.evaluate(()=>({width:canvas.width,crop:cropRect}));const scale=b.width/model.width;await page.mouse.move(h.x+h.width/2,h.y+h.height/2);await page.mouse.down();await page.mouse.move(b.x+(x-model.crop.x)*scale,b.y+(y-model.crop.y)*scale,{steps:5});await page.mouse.up();}
    await run('bubble tip moves around all four sides without moving text/body',async page=>{
      assert.equal(await page.locator('#bubbleTailHandle').count(),1,'Draggable bubble pointer handle is missing');await setupBubble(page);
      const count=await page.evaluate(()=>undoStack.length);
      for(const [x,y,edge] of [[100,230,'left'],[550,230,'right'],[330,80,'top'],[330,390,'bottom']]){
        await dragTip(page,x,y);const g=await page.evaluate(()=>annotationEffects.bubbleGeometry(ctx,objects[0]));assert.equal(g.edge,edge);assert(Math.abs(g.tip.x-x)<1&&Math.abs(g.tip.y-y)<1);
        const item=await page.evaluate(()=>structuredClone(objects[0]));assert.equal(item.x1,200);assert.equal(item.y1,180);assert.equal(item.text,'Neo Snap');
        const pixel=await page.evaluate(()=>{render(false);const g=annotationEffects.bubbleGeometry(ctx,objects[0]);const middle={x:(g.tip.x+(g.start.x+g.end.x)/2)/2,y:(g.tip.y+(g.start.y+g.end.y)/2)/2};return [...ctx.getImageData(Math.round(middle.x),Math.round(middle.y),1,1).data];});assert.deepEqual(pixel,[255,249,204,255],'Pointer must render its fill along the dragged direction');
        const interaction=await page.evaluate(()=>{const i=objects[0],g=annotationEffects.bubbleGeometry(ctx,i);return {hit:hitTest(g.tip,i),bounds:bounds(i),tip:g.tip};});assert.equal(interaction.hit,true);assert(interaction.tip.x>=interaction.bounds.x&&interaction.tip.x<=interaction.bounds.x+interaction.bounds.width&&interaction.tip.y>=interaction.bounds.y&&interaction.tip.y<=interaction.bounds.y+interaction.bounds.height);
      }
      assert.equal(await page.evaluate(()=>undoStack.length),count+4,'One undo step per pointer drag');await page.locator('#undoButton').click();assert.equal(await page.evaluate(()=>annotationEffects.bubbleGeometry(ctx,objects[0]).edge),'top');await page.locator('#redoButton').click();assert.equal(await page.evaluate(()=>annotationEffects.bubbleGeometry(ctx,objects[0]).edge),'bottom');
      await page.evaluate(()=>{selected=0;syncControls();render();});await dragTip(page,330,225);const inside=await page.evaluate(()=>annotationEffects.bubbleGeometry(ctx,objects[0]));assert(inside.tip.y<=180||inside.tip.y>=280||inside.tip.x<=200||inside.tip.x>=460,'An inside tip produces a valid exterior pointer');
      await dragTip(page,550,230);
      fs.mkdirSync(path.resolve(__dirname,'../dist/view-tail-preview'),{recursive:true});await page.screenshot({path:path.resolve(__dirname,'../dist/view-tail-preview/tail-desktop.png')});
    });
    await run('legacy silhouette is unchanged and cropped inside tips remain reachable',async page=>{
      await setupBubble(page);
      const identical=await page.evaluate(()=>{const i=objects[0],layout=annotationEffects.bubbleLayout(ctx,i),g=annotationEffects.bubbleGeometry(ctx,i);const c=document.createElement('canvas');c.width=800;c.height=600;const t=c.getContext('2d');annotationEffects.bubblePath(t,i.x1,i.y1,layout);t.fill();const old=t.getImageData(0,0,800,600).data;t.clearRect(0,0,800,600);annotationEffects.bubblePath(t,i.x1,i.y1,layout,g);t.fill();const next=t.getImageData(0,0,800,600).data;return old.every((v,n)=>v===next[n]);});assert.equal(identical,true);
      await page.evaluate(()=>{cropRect={x:100,y:100,width:700,height:500};canvas.width=700;canvas.height=500;objects[0].x1=100;objects[0].y1=100;fitCanvas();render();});await dragTip(page,220,102);
      const g=await page.evaluate(()=>annotationEffects.bubbleGeometry(ctx,objects[0]));assert(g.tip.x>=100&&g.tip.x<=800&&g.tip.y>=100&&g.tip.y<=600,'An inside drag near the crop edge must not hide the pointer outside the image');
    });
    await run('custom tip survives body move resize edit crop export and interrupted gestures',async page=>{
      assert.equal(await page.locator('#bubbleTailHandle').count(),1,'Draggable bubble pointer handle is missing');await setupBubble(page);await dragTip(page,550,230);
      const relative=await page.evaluate(()=>structuredClone(objects[0].tail));let b=await page.locator('#canvas').boundingBox(),scale=b.width/1600;
      await page.mouse.move(b.x+230*scale,b.y+220*scale);await page.mouse.down();await page.mouse.move(b.x+270*scale,b.y+250*scale);await page.mouse.up();
      assert.deepEqual(await page.evaluate(()=>objects[0].tail),relative,'Move translates the whole pointer without changing its offset');assert(Math.abs(await page.evaluate(()=>objects[0].x1)-240)<.01);
      const body=await page.evaluate(()=>annotationEffects.bubbleGeometry(ctx,objects[0]).body);await page.mouse.move(b.x+(body.x+body.width)*scale,b.y+(body.y+body.height)*scale);await page.mouse.down();await page.mouse.move(b.x+(body.x+300)*scale,b.y+(body.y+140)*scale);await page.mouse.up();
      assert.equal(Math.round(await page.evaluate(()=>objects[0].bubbleWidth)),300);assert.equal(await page.evaluate(()=>objects[0].size),6);assert.deepEqual(await page.evaluate(()=>objects[0].tail),relative);
      await page.mouse.dblclick(b.x+270*scale,b.y+245*scale);await page.locator('.canvas-text-editor').fill('Edited');await page.keyboard.press('Control+Enter');assert.deepEqual(await page.evaluate(()=>objects[0].tail),relative);
      const prior=await page.evaluate(()=>structuredClone(objects[0].tail));const h=await page.locator('#bubbleTailHandle').boundingBox();await page.mouse.move(h.x+h.width/2,h.y+h.height/2);await page.mouse.down();await page.mouse.move(b.x+620*scale,b.y+280*scale);await page.keyboard.press('Escape');await page.mouse.move(b.x+700*scale,b.y+300*scale);await page.mouse.up();assert.equal(await page.evaluate(()=>gesture),null);const changed=await page.evaluate(()=>structuredClone(objects[0].tail));assert.notDeepEqual(changed,prior);
      await page.locator('#undoButton').click();assert.deepEqual(await page.evaluate(()=>objects[0].tail),prior);
      await page.evaluate(()=>{cropRect={x:100,y:100,width:700,height:500};canvas.width=700;canvas.height=500;selected=0;fitCanvas();syncControls();render();});
      await dragTip(page,750,400);
      const output=await page.evaluate(async()=>{const g=annotationEffects.bubbleGeometry(ctx,objects[0]);render(false);const before=canvas.toDataURL();const png=await exportBlob();const after=await new Promise(r=>{const f=new FileReader();f.onload=()=>r(f.result);f.readAsDataURL(png);});return {same:before===after,tip:g.tip,crop:cropRect};});assert.equal(output.same,true,'Selection/tip handles never contaminate PNG');assert(output.tip.x>=100&&output.tip.x<=800&&output.tip.y>=100&&output.tip.y<=600);
      assert(Math.abs(output.tip.x-750)<1&&Math.abs(output.tip.y-400)<1,'Crop coordinates preserve the requested pointer position');
      await page.setViewportSize({width:480,height:720});await page.screenshot({path:path.resolve(__dirname,'../dist/view-tail-preview/tail-narrow.png')});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
    });
    if(failures)throw new Error(`${failures} view/pointer tests failed`);
  }finally{await browser?.close();await new Promise(r=>server.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
