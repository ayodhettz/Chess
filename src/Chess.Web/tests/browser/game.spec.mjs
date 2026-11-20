import {test,expect} from '@playwright/test';
async function pair(browser,baseURL,seat='white',configure=async()=>{}) {
 const a=await browser.newContext(),b=await browser.newContext();const white=await a.newPage(),black=await b.newPage();await configure(a);
 await white.goto(baseURL);await white.locator(`input[value=${seat}]`).check();await white.getByRole('button',{name:'Create room',exact:true}).click();
 await expect(white.locator('#room-label')).toHaveText(/^[A-Z2-9]{8}$/);
 const link=await white.locator('#invite-link').inputValue();await black.goto(link);await black.getByRole('button',{name:'Join game',exact:true}).click();
 await expect(white.locator('#game-status')).toHaveText(seat==='white'?'Your move.':'Your opponent’s move.');
 return {white:seat==='white'?white:black,black:seat==='white'?black:white,a,b};
}
async function move(page,from,to) {
 await expect(page.locator(`[data-square=${from}]`)).toHaveAttribute('aria-disabled','false');
 await page.locator(`[data-square=${from}]`).click();await page.locator(`[data-square=${to}]`).click();
}
test('two browsers play, reload the seat, and agree a draw',async({browser,baseURL})=>{
 const {white,black,a,b}=await pair(browser,baseURL);
 const secret=await white.evaluate(()=>JSON.parse(sessionStorage.getItem('chess.online.v2')).ticket.credential);
 expect(await white.locator('#invite-link').inputValue()).not.toContain(secret);
 await move(white,'e2','e4');await expect(black.locator('#moves')).toContainText('e4');
 await move(black,'e7','e5');await expect(white.locator('#moves')).toContainText('e5');
 await white.reload();await expect(white.locator('#game-status')).toHaveText('Your move.');
 await white.getByRole('button',{name:'Offer draw',exact:true}).click();await black.getByRole('button',{name:'Accept draw',exact:true}).click();
 await expect(white.locator('#turn')).toContainText('Draw agreed');await expect(black.locator('#turn')).toContainText('Draw agreed');
 await a.close();await b.close();
});
test('black host orientation and off-turn resignation',async({browser,baseURL})=>{
 const {white,black,a,b}=await pair(browser,baseURL,'black');
 await expect(black.locator('#board button').first()).toHaveAttribute('data-square','h1');
 await black.getByRole('button',{name:'Resign',exact:true}).click();await black.locator('#confirm-yes').click();
 await expect(white.locator('#turn')).toContainText('White wins');await a.close();await b.close();
});
test('promotion is an explicit choice validated by the server',async({browser,baseURL})=>{
 const {white,black,a,b}=await pair(browser,baseURL);
 for(const [page,from,to] of [[white,'a2','a4'],[black,'h7','h5'],[white,'a4','a5'],[black,'h5','h4'],[white,'a5','a6'],[black,'h4','h3'],[white,'a6','b7'],[black,'h3','g2']]) await move(page,from,to);
 await move(white,'b7','a8');await expect(white.locator('#promotion-dialog')).toBeVisible();await white.getByRole('button',{name:'Knight',exact:true}).click();
 await expect(black.locator('[data-square=a8]')).toHaveAttribute('aria-label','a8, white knight');await a.close();await b.close();
});
test('intended repetition claim ends without playing the proposed move',async({browser,baseURL})=>{
 const {white,black,a,b}=await pair(browser,baseURL);
 for(const [page,from,to] of [[white,'g1','f3'],[black,'g8','f6'],[white,'f3','g1'],[black,'f6','g8'],[white,'g1','f3'],[black,'g8','f6'],[white,'f3','g1']]) await move(page,from,to);
 await black.locator('#claim-intended').check();await move(black,'f6','g8');
 await expect(white.locator('#turn')).toContainText('Threefold repetition');await expect(white.locator('[data-square=f6]')).toHaveAttribute('aria-label','f6, black knight');await a.close();await b.close();
});
test('mobile layout and keyboard square navigation',async({page})=>{
 await page.setViewportSize({width:390,height:844});await page.goto('/');await page.getByRole('button',{name:'Create room',exact:true}).click();
 await expect(page.locator('#game')).toBeVisible();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.locator('[data-square=a8]').focus();await page.keyboard.press('ArrowRight');await expect(page.locator('[data-square=b8]')).toBeFocused();
 await page.screenshot({path:'test-results/mobile.png',fullPage:true});
});
test('unavailable invitation shows a useful error',async({page})=>{
 await page.goto('/#room=ABCDEFGH');await page.getByRole('button',{name:'Join game',exact:true}).click();await expect(page.locator('#notice')).toContainText('unavailable');
});

test('a disconnected tab resumes its seat and preserves the position',async({browser,baseURL})=>{
 let clientSocket,serverSocket;
 const {white,black,a,b}=await pair(browser,baseURL,'white',async context=>{
   await context.routeWebSocket('**/hubs/game**',socket=>{clientSocket=socket;serverSocket=socket.connectToServer();});
 });
 await move(white,'e2','e4');await expect(black.locator('#moves')).toContainText('e4');
 await a.setOffline(true);
 // Chromium offline emulation alone does not close an existing WebSocket.
 await clientSocket.close({code:1012,reason:'Test transport interruption'});
 await serverSocket.close({code:1012,reason:'Test transport interruption'});
 await expect(black.locator('#opponent-label')).toContainText('offline',{timeout:25000});
 await a.setOffline(false);
 await expect(black.locator('#opponent-label')).toContainText('online',{timeout:20000});
 await expect(white.locator('#connection')).toHaveText('Connected');
 await expect(white.locator('#moves')).toHaveText('1.e4');
 await move(black,'e7','e5');await expect(white.locator('#moves')).toContainText('e5');
 await white.screenshot({path:'test-results/desktop.png',fullPage:true});
 await a.close();await b.close();
});
test('browser assets and same-origin security headers are served locally',async({request})=>{
 for(const path of ['/','/app.js','/session.js','/styles.css','/vendor/signalr.min.js','/assets/KingW.png','/assets/fonts/IBMPlexSans-Regular.ttf']) {
   const response=await request.get(path);expect(response.ok(),path).toBe(true);
   expect(response.headers()['content-security-policy']).toContain("frame-ancestors 'none'");
   expect(response.headers()['x-content-type-options']).toBe('nosniff');
 }
 expect((await request.get('/')).headers()['cache-control']).toBe('no-store');
});
