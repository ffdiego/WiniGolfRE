const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
const root = path.resolve('simulator/src/Winigolf.Web/bin/Release/net10.0/browser-wasm/AppBundle');
const types = {'.html':'text/html', '.js':'text/javascript', '.json':'application/json', '.wasm':'application/wasm'};
const server = http.createServer((req, res) => {
  const file = path.resolve(root, '.' + decodeURIComponent(new URL(req.url, 'http://localhost').pathname));
  if (!file.startsWith(root + path.sep) && file !== root) { res.writeHead(403).end(); return; }
  const actual = file === root ? path.join(root, 'index.html') : file;
  fs.readFile(actual, (err, data) => {
    if (err) { res.writeHead(404).end(); return; }
    res.writeHead(200, {'Content-Type':types[path.extname(actual)] || 'application/octet-stream'}); res.end(data);
  });
});
(async () => {
  await new Promise(resolve => server.listen(8765, '127.0.0.1', resolve));
  const browser = await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe', headless:true, args:['--enable-unsafe-swiftshader']});
  if (!process.env.MOBILE_ONLY) {
  const page = await browser.newPage({viewport:{width:1100,height:800}});
  page.on('console', msg => console.log('console:', msg.type(), msg.text()));
  page.on('pageerror', err => console.log('pageerror:', err.toString()));
  const response = await page.goto('http://127.0.0.1:8765/', {waitUntil:'networkidle'});
  await page.waitForTimeout(8000);
  console.log('HTTP', response.status(), 'error:', await page.locator('#error').textContent());
  console.log('performance:', await page.locator('#performance').textContent());
  if (process.env.PERF_ONLY) { await page.screenshot({path:'artifacts/web-test/optimized.png'}); await browser.close(); server.close(); return; }
  await page.screenshot({path:'artifacts/web-test/before.png'});
  const box = await page.locator('canvas').boundingBox();
  await page.mouse.move(box.x + box.width*.5, box.y + box.height*.5);
  await page.mouse.down(); await page.mouse.move(box.x + box.width*.6, box.y + box.height*.65, {steps:10}); await page.mouse.up();
  await page.waitForTimeout(3000);
  console.log('after shot error:', await page.locator('#error').textContent());
  await page.screenshot({path:'artifacts/web-test/after.png'});
  await page.close();
  if (process.env.DESKTOP_ONLY) { await browser.close(); server.close(); return; }
  }
  const mobile = await browser.newPage({viewport:{width:390,height:844}, isMobile:true, hasTouch:true, deviceScaleFactor:3});
  mobile.on('console', msg => console.log('mobile console:', msg.type(), msg.text()));
  mobile.on('pageerror', err => console.log('mobile pageerror:', err.toString()));
  await mobile.goto('http://127.0.0.1:8765/', {waitUntil:'networkidle'});
  await mobile.waitForTimeout(5000);
  const mb = await mobile.locator('canvas').boundingBox();
  console.log('mobile canvas:', JSON.stringify(mb), 'style:', await mobile.locator('canvas').getAttribute('style'));
  console.log('mobile canvas properties:', await mobile.locator('canvas').evaluate(c => JSON.stringify({width:c.width,height:c.height,dpr:devicePixelRatio, innerWidth,innerHeight, viewport:c.getContext('webgl')?.getParameter(c.getContext('webgl').VIEWPORT)?.toString()})));
  const cdp = await mobile.context().newCDPSession(mobile);
  await cdp.send('Input.dispatchTouchEvent', {type:'touchStart', touchPoints:[{x:mb.x+mb.width*.5,y:mb.y+mb.height*.5}]});
  await cdp.send('Input.dispatchTouchEvent', {type:'touchMove', touchPoints:[{x:mb.x+mb.width*.65,y:mb.y+mb.height*.65}]});
  await cdp.send('Input.dispatchTouchEvent', {type:'touchEnd', touchPoints:[]});
  await mobile.waitForTimeout(4000);
  console.log('mobile error:', await mobile.locator('#error').textContent());
  await mobile.screenshot({path:'artifacts/web-test/mobile.png'});
  await mobile.locator('canvas').screenshot({path:'artifacts/web-test/mobile-canvas.png'});
  await mobile.setViewportSize({width:844,height:390});
  await mobile.waitForTimeout(2000);
  const layout = await mobile.evaluate(() => {
    const c = document.querySelector('canvas'), r = c.getBoundingClientRect(), m = c.parentElement.getBoundingClientRect();
    return {canvas:{x:r.x,y:r.y,width:r.width,height:r.height}, main:{x:m.x,y:m.y,width:m.width,height:m.height}, buffer:[c.width,c.height]};
  });
  console.log('landscape layout:', JSON.stringify(layout));
  if (Math.abs((layout.canvas.x + layout.canvas.width / 2) - (layout.main.x + layout.main.width / 2)) > 1 ||
      Math.abs((layout.canvas.y + layout.canvas.height / 2) - (layout.main.y + layout.main.height / 2)) > 1 ||
      layout.canvas.width > layout.main.width + 1 || layout.canvas.height > layout.main.height + 1 ||
      layout.buffer[0] !== 960 || layout.buffer[1] !== 600) throw Error('Canvas de paisagem incorreto');
  await mobile.screenshot({path:'artifacts/web-test/landscape.png'});
  await browser.close(); server.close();
})().catch(e => { console.error(e); server.close(); process.exitCode = 1; });
