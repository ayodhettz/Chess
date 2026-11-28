import {spawn} from 'node:child_process';
import {createInterface} from 'node:readline';
import {fileURLToPath} from 'node:url';
import {test,expect} from '@playwright/test';
const root=fileURLToPath(new URL('../../../../',import.meta.url));
for(const seat of ['white','black']) test(`Windows connection client plays a browser opponent as ${seat}`,async({page,baseURL})=>{
 const messages=[];
 const child=spawn('dotnet',['tools/Chess.Client.Smoke/bin/Release/net10.0/Chess.Client.Smoke.dll',baseURL,seat],{cwd:root,stdio:['pipe','pipe','pipe']});
 const lines=createInterface({input:child.stdout});let errors='';child.stderr.on('data',data=>{errors+=data;});
 lines.on('line',line=>{if(line.startsWith('{')) messages.push(JSON.parse(line));});
 try {
   await expect.poll(()=>messages.some(m=>m.kind==='ready'),{message:'Native connection client did not start',timeout:15000}).toBe(true);
   const ready=messages.find(m=>m.kind==='ready');
   await page.goto(ready.invitation);await page.getByRole('button',{name:'Join game',exact:true}).click();
   await expect(page.locator('#game')).toBeVisible();
   const send=command=>child.stdin.write(JSON.stringify(command)+'\n');
   await expect.poll(()=>messages.some(m=>m.kind==='state'&&m.revision>=2)).toBe(true);
   if(seat==='white') {
     send({action:'move',from:'e2',to:'e4'});await expect(page.locator('#moves')).toContainText('e4');
     await page.locator('[data-square=e7]').click();await page.locator('[data-square=e5]').click();
   } else {
     await page.locator('[data-square=e2]').click();await page.locator('[data-square=e4]').click();
     await expect.poll(()=>messages.some(m=>m.kind==='state'&&m.moves.length===1)).toBe(true);
     send({action:'move',from:'e7',to:'e5'});
   }
   await expect.poll(()=>messages.some(m=>m.kind==='state'&&m.moves.length===2)).toBe(true);
   await expect(page.locator('#moves')).toContainText('e5');
   send({action:'resume'});await expect.poll(()=>messages.some(m=>m.kind==='done'&&m.action==='resume')).toBe(true);
   send({action:'resign'});await expect(page.locator('#turn')).toContainText(`${seat==='white'?'Black':'White'} wins`);
   expect(messages.filter(m=>m.kind==='error')).toEqual([]);
 } finally {child.stdin.end();child.kill();lines.close();}
});
