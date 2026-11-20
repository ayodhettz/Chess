import { cp, mkdir, copyFile, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../', import.meta.url));
const output = path.resolve(root, '../Chess.Server/wwwroot');
const assets = path.resolve(root, '../ChessUI/Assets');
await mkdir(output, { recursive: true });
await cp(path.join(root, 'app'), output, { recursive: true });
await mkdir(path.join(output, 'vendor'), { recursive: true });
try {
  await copyFile(path.join(root, 'node_modules/@microsoft/signalr/dist/browser/signalr.min.js'), path.join(output, 'vendor/signalr.min.js'));
  await copyFile(path.join(root, 'SignalR-LICENSE.txt'), path.join(output, 'vendor/signalr-LICENSE.txt'));
} catch (error) {
  throw new Error('Run npm ci in src/Chess.Web before building the server.', { cause: error });
}
await mkdir(path.join(output, 'assets/fonts'), { recursive: true });
for (const side of ['W', 'B']) {
  for (const piece of ['King', 'Queen', 'Rook', 'Bishop', 'Knight', 'Pawn'])
    await copyFile(path.join(assets, `${piece}${side}.png`), path.join(output, 'assets', `${piece}${side}.png`));
}
await cp(path.join(assets, 'Fonts'), path.join(output, 'assets/fonts'), { recursive: true });
const pkg = JSON.parse(await readFile(path.join(root, 'package.json'), 'utf8'));
console.log(`Built browser assets with SignalR ${pkg.dependencies['@microsoft/signalr']}.`);

