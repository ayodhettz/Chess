# Browser play

The browser client follows the Tournament palette and reuses the desktop pieces and
fonts. It is served by the ASP.NET server at `/`; SignalR uses `/hubs/game` on the
same origin. There is no CDN dependency and no JavaScript chess engine. The server
provides the position, legal destinations, SAN history, and result.

## Run and share

Install .NET SDK 10.0.404+ and Node.js 24 LTS, then run from the repository root:

```powershell
npm ci --prefix src/Chess.Web --ignore-scripts
dotnet run --project src/Chess.Server/Chess.Server.csproj --urls http://127.0.0.1:5080
```

Open http://127.0.0.1:5080. Create a room, choose a side, and copy the invitation.
The second player opens it and chooses **Join game**. For local testing, use two
separate browser profiles or an incognito window. A localhost invitation cannot
reach another computer. Public HTTPS hosting is a separate milestone.

Published server archives include the browser assets and runtime. Players need
only a browser; they do not install Node, .NET, or the source. Building the server
runs the asset-copy script, with no package downloads during the build itself.

## Playing and recovery

Select a piece and a highlighted destination. Use arrow keys to move board focus,
and Enter or Space to select a square. Promotion asks for queen, rook, bishop, or
knight. The board faces your chosen side and adapts to narrow screens.

Draw offers, acceptance/decline, current-position claims, intended-move claims, and
resignation use server commands. Enable **Claim with an intended move**, then choose
a highlighted move to claim without playing it. There is no online undo or bot hint.
Resignation asks for confirmation and works off turn.

The invitation contains only the room code. A private seat credential is stored in
that tab's session storage, never in the link. Reloading resumes the seat. Keep the
tab open: closing it, clearing storage, or using another device may lose access.
When storage is blocked, the UI explains that reload recovery may be unavailable.
Avoid duplicating a seated tab; resuming a credential replaces its old connection.

Transport reconnect automatically rebinds the seat and refreshes authoritative state.
A move is displayed only after server confirmation. An ambiguous action keeps its
original ID, revision, and payload in session storage; **Recover pending action**
retries that exact command. Accepted retries use the server's bounded receipt cache.
Stale rejected actions refresh the position and require a fresh choice. A lost
Create/Join response before the credential reaches the browser cannot be recovered.

Rooms are temporary and held in one server process. Restarting it loses rooms.
Moves pause while the opponent is offline. Existing server limits and expiry apply.

## Validation

```powershell
npm test --prefix src/Chess.Web
npx --prefix src/Chess.Web playwright install chromium
dotnet build src/Chess.Server/Chess.Server.csproj --configuration Release
npm run test:e2e --prefix src/Chess.Web
```

Playwright starts a local server and tests independent player contexts. CI also
publishes the Linux server including all local scripts, pieces, fonts, and licenses.
