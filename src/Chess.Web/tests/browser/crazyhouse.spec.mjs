import {test,expect} from '@playwright/test';

async function move(page,from,to) {
  await expect(page.locator(`[data-square=${from}]`)).toHaveAttribute('aria-disabled','false');
  await page.locator(`[data-square=${from}]`).click();await page.locator(`[data-square=${to}]`).click();
}

test('Crazyhouse captures, drops and reloads both reserves on mobile and desktop',async({browser,baseURL})=>{
  const a=await browser.newContext(),b=await browser.newContext({viewport:{width:390,height:844}});
  const white=await a.newPage(),black=await b.newPage();
  await white.goto(baseURL);await white.locator('input[name=variant][value=crazyhouse]').check();
  await white.getByRole('button',{name:'Create room',exact:true}).click();
  await expect(white.locator('#room-label')).toHaveText(/^[A-Z2-9]{8}$/);
  await black.goto(await white.locator('#invite-link').inputValue());
  await black.getByRole('button',{name:'Join game',exact:true}).click();
  await expect(white.locator('#match-label')).toContainText('Crazyhouse');
  for(const [page,from,to] of [[white,'e2','e4'],[black,'d7','d5'],[white,'e4','d5'],[black,'d8','d5']]) await move(page,from,to);
  const pawn=white.getByRole('button',{name:'Drop pawn, 1 available',exact:true});
  await expect(pawn).toBeEnabled();await pawn.click();
  await expect(white.locator('[data-square=e4]')).toHaveClass(/legal/);
  await expect(white.locator('[data-square=a8]')).not.toHaveClass(/legal/);
  await white.locator('[data-square=e4]').click();
  await expect(black.locator('#moves')).toContainText('@e4');
  await expect(white.getByRole('button',{name:'Drop pawn, 0 available',exact:true})).toBeDisabled();
  await black.reload();await expect(black.getByRole('button',{name:'Drop pawn, 1 available',exact:true})).toBeEnabled();
  await black.getByRole('button',{name:'Drop pawn, 1 available',exact:true}).click();
  await black.locator('[data-square=e5]').click();
  await expect(white.locator('[data-square=e5]')).toHaveAttribute('aria-label','e5, black pawn');
  await white.reload();await expect(white.locator('#moves')).toContainText('@e5');
  await expect(white.getByRole('button',{name:'Drop pawn, 0 available',exact:true})).toBeDisabled();
  expect(await black.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await white.screenshot({path:'test-results/crazyhouse-desktop.png',fullPage:true});
  await black.screenshot({path:'test-results/crazyhouse-mobile.png',fullPage:true});
  await a.close();await b.close();
});

test('Crazyhouse computer plays with the selected rules',async({page})=>{
  await page.goto('/');await page.locator('input[name=variant][value=crazyhouse]').check();
  await page.locator('input[name=mode][value=computer]').check();
  await page.locator('input[name=difficulty][value=easy]').check();
  await page.locator('input[name=seat][value=black]').check();
  await page.getByRole('button',{name:'Play computer',exact:true}).click();
  await expect(page.locator('#match-label')).toHaveText('Crazyhouse · Computer game');
  await expect(page.locator('#game-status')).toHaveText('Your move.',{timeout:20000});
  await expect(page.locator('#your-pocket')).toBeVisible();
  await expect(page.locator('#moves tr')).toHaveCount(1);
});
