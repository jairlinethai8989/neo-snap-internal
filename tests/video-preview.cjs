const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {execFileSync}=require('node:child_process');
const {chromium}=require(process.env.PLAYWRIGHT_PATH||'C:/Users/jairlinethai/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'../src/SnapCraft/Assets'),output=path.resolve(__dirname,'../dist/stroke-preview');
(async()=>{
  fs.mkdirSync(output,{recursive:true});
  const clip=path.join(output,'fixture.mp4');
  execFileSync(process.env.FFMPEG_PATH||'ffmpeg',['-hide_banner','-loglevel','error','-y','-f','lavfi','-i','testsrc2=size=640x360:rate=10','-t','3','-c:v','libx264','-pix_fmt','yuv420p','-movflags','+faststart',clip]);
  const server=http.createServer((req,res)=>{
    const pathname=new URL(req.url,'http://localhost').pathname;
    const file=pathname==='/fixture.mp4'?clip:path.resolve(root,'.'+pathname);
    if(file!==clip&&(!file.startsWith(root+path.sep)||!fs.existsSync(file)))return res.writeHead(404).end();
    const size=fs.statSync(file).size;
    const mime={'.html':'text/html','.js':'text/javascript','.css':'text/css','.mp4':'video/mp4'}[path.extname(file)]||'application/octet-stream';
    const range=req.headers.range?.match(/bytes=(\d+)-(\d*)/);
    if(range){const start=+range[1],end=range[2]?Math.min(+range[2],size-1):size-1;res.writeHead(206,{'Content-Type':mime,'Content-Range':`bytes ${start}-${end}/${size}`,'Content-Length':end-start+1,'Accept-Ranges':'bytes'});fs.createReadStream(file,{start,end}).pipe(res);}
    else{res.writeHead(200,{'Content-Type':mime,'Content-Length':size,'Accept-Ranges':'bytes'});fs.createReadStream(file).pipe(res);}
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));let browser;
  try{
    browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:720,height:480}});
    await page.addInitScript(()=>{window.sent=[];window.chrome={webview:{postMessage:message=>sent.push(message),addEventListener:(_,callback)=>window.receive=callback}};});
    const origin=`http://127.0.0.1:${server.address().port}`;
    await page.goto(origin+'/video-preview.html?clip='+encodeURIComponent(origin+'/fixture.mp4'));
    assert.equal(await page.locator('#clipVideo').count(),1,'Stopping recording needs an actual local video preview');
    assert.equal(await page.locator('#dragClip svg').count(),1,'The drag control must show its icon');
    await page.waitForFunction(()=>document.querySelector('video').readyState>=2);
    assert.equal(await page.evaluate(()=>document.querySelector('video').paused),true,'Preview does not autoplay');
    await page.evaluate(()=>document.querySelector('video').play());await page.waitForFunction(()=>document.querySelector('video').currentTime>.3);
    const varied=await page.evaluate(()=>{const v=document.querySelector('video'),c=document.createElement('canvas');c.width=32;c.height=18;const x=c.getContext('2d');x.drawImage(v,0,0,32,18);return new Set(x.getImageData(0,0,32,18).data).size;});
    assert(varied>20,'Preview must decode nonblank video frames');
    await page.evaluate(()=>{const v=document.querySelector('video');v.pause();v.currentTime=1;});
    await page.locator('#copyClip').click();
    assert.equal(await page.evaluate(()=>sent.at(-1).action),'copyClip');
    assert.equal(await page.locator('#copyClip').getAttribute('aria-busy'),'true');
    await page.evaluate(()=>receive({data:{type:'result',action:'copyClip',success:true,text:'คัดลอกคลิปแล้ว'}}));
    assert.equal(await page.locator('#copyClip').getAttribute('aria-pressed'),'true');
    await page.locator('#saveClip').click();
    assert.equal(await page.evaluate(()=>sent.at(-1).action),'saveClip');
    await page.evaluate(()=>receive({data:{type:'result',action:'saveClip',success:false,text:'บันทึกไม่สำเร็จ'}}));
    assert.equal(await page.locator('#saveClip').isDisabled(),false);
    assert.equal(await page.locator('#clipVideo').isVisible(),true);
    assert.match(await page.locator('#clipStatus').textContent(),/ไม่สำเร็จ/);
    await page.screenshot({path:path.join(output,'video-preview.png')});
    for(const viewport of [{width:480,height:360},{width:360,height:640}]){
      await page.setViewportSize(viewport);
      assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true,'Preview must not overflow horizontally');
      const actions=await page.locator('.clip-actions').boundingBox();assert(actions.y+actions.height<=viewport.height,'Actions stay accessible');
    }
    for (const product of ['SnapZy', 'Neo Snap']) {
      for (const language of ['en', 'th']) {
        await page.goto(origin+'/video-preview.html?product='+encodeURIComponent(product)+'&language='+language+'&clip='+encodeURIComponent(origin+'/fixture.mp4'));
        assert.equal(await page.locator('html').getAttribute('lang'), language, `${product} preview language`);
        assert.equal((await page.locator('#copyClip').textContent()).trim(), language === 'en' ? 'Copy clip' : 'คัดลอกคลิป');
        assert.equal((await page.locator('#saveClip').textContent()).trim(), language === 'en' ? 'Save MP4' : 'บันทึก MP4');
        assert.equal(await page.title(), `${product} | ${language === 'en' ? 'Video' : 'วิดีโอ'}`);
        await page.locator('#copyClip').click();
        assert.equal(await page.locator('#clipStatus').textContent(), language === 'en' ? 'Copying…' : 'กำลังคัดลอก…');
        await page.evaluate(next => receive({data:{type:'language', language:next}}), language === 'en' ? 'th' : 'en');
        assert.equal(await page.locator('html').getAttribute('lang'), language === 'en' ? 'th' : 'en');
      }
    }
    console.log('PASS video preview: decoded playback, seek, copy success, save failure, compact layouts');
  }finally{await browser?.close();await new Promise(resolve=>server.close(resolve));}
})().catch(error=>{console.error(error);process.exitCode=1;});
