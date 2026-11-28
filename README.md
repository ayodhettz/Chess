# Chess

A chess project with a Windows WPF app, a browser client, and a shared C#/.NET 10
server. Play against a custom-built bot, share a local board on Windows, or play
online with a friend across Windows and browser clients. The Tournament UI
uses a charcoal gradient, restrained brass accents, and a traditional move scoresheet.

| Start a game | Play against the bot |
| --- | --- |
| ![Choose an opponent and colour](docs/start-game.png) | ![Tournament board and move scoresheet](docs/bot.png) |

The original foundation follows [OttoBotCode's chess tutorial](https://www.youtube.com/@OttoBotCode).

## Feature availability

Web and Windows support computer games with Easy, Medium, and Hard search budgets,
private online rooms, and cancellable random matchmaking. Public deployment must
update both frontend and backend before new features are available to visitors.
New Figma explorations remain design drafts, not the implemented interface.

[Crazyhouse](docs/crazyhouse.md) is available in Windows local/computer play and
cross-platform online games, including browser bots and random matchmaking.
Choose Crazyhouse under Rules, then select captured pieces from your reserve to drop them.

## Playing

Choose **Computer** or **Local two-player** on the start screen. In a computer game,
choose White or Black and select **Take White & play** or **Take Black & play**.
The board faces your side, and the bot moves automatically on its turns.

Choose **Play online** to create or join a private room with a Windows or browser
opponent. [Windows online controls and recovery](docs/windows-online.md).

- Click a piece, then a highlighted destination. Promotion offers all four choices.
- **Suggest move** highlights a recommendation without playing it.
- **Escape** pauses or continues. **Help → Cancel thinking** stops a search;
  **Resume bot** continues a stopped bot turn.
- **Undo** / **Ctrl+Z** and **Redo** / **Ctrl+Y** navigate history. Bot games group
  your move and the reply into a round; local games navigate one action at a time.
- **Open saved game** resumes a complete save from setup. The File menu also
  supports saving/loading games and importing/exporting PGN; **Load FEN** opens the position editor.

Saved chess files restore Standard or Crazyhouse rules; the opponent and human colour
use the current UI settings. See [gameplay controls](docs/gameplay.md) for draw
claims, offers, resignation, and history behavior.

## The custom bot

The bot is implemented in this repository, without an external engine or online service.
It searches legal moves and replies using iterative-deepening negamax and alpha-beta
pruning, then scores material and piece placement. The current baseline searches up
to three plies within a 4,000-visit budget, using the last completed iteration.
It handles promotions, draw claims, and responses to draw offers.

Easy uses 1 ply / 500 visits, Medium 3 / 4,000, and Hard 5 / 30,000. These are search
budgets, with no strength rating. The bot also searches Crazyhouse drops and values
reserve material. It can miss tactics beyond its search horizon. [Read how the bot works](docs/bot.md).

## Build and run

Install [.NET SDK 10.0.404 or a newer .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows.
Install [Node.js 24 LTS](https://nodejs.org/) to build the browser assets used by the server.
From the repository root:

```powershell
npm ci --prefix src/Chess.Web --ignore-scripts
dotnet restore src/Chess.sln
dotnet build src/Chess.sln --configuration Release
dotnet run --project src/ChessUI/ChessUI.csproj
```

Alternatively, open `src/Chess.sln` in a Visual Studio version supporting .NET 10
with the **.NET desktop development** workload, and select `ChessUI` as the startup project.

The rules and search live in `src/ChessLogic`; the desktop interface lives in
`src/ChessUI`. Libre Baskerville and IBM Plex fonts are bundled with their license
notices, so the interface does not depend on installing those fonts separately.

## Online multiplayer development

The [browser client](docs/browser-play.md) supports the custom computer opponent and private online games with
invitation links, reconnect/resume, promotions, draw actions, and resignation.
It uses the same custom C# rules as the desktop app. Run the server, open its URL,
and share a room invitation with someone who can reach that server.

```powershell
npm ci --prefix src/Chess.Web --ignore-scripts
dotnet run --project src/Chess.Server/Chess.Server.csproj --urls http://127.0.0.1:5080
```

Open **http://127.0.0.1:5080** locally. The published Linux server archive includes
all browser assets; players need only a browser. Windows players can use **Play
online** to join the same rooms. Localhost links work only on your computer.
See the [server contract](docs/multiplayer-server.md) and [multiplayer plan](docs/online-multiplayer-plan.md).

CI validates desktop/server tests, browser recovery logic, and real two-browser games.

## Features

- Board rendering and legal-move highlights
- [Tournament interface](docs/tournament-design.md), based on the approved Figma C design
- [Custom chess bot](docs/bot.md): automatic opponent mode, colour selection, and move suggestions
- Turn tracking and king-safety validation
- Castling, en passant, and all four promotion choices
- Checkmate, stalemate, and basic insufficient-material detection
- Pause, restart, and rematch menus
- [Contextual draw claims and draw offers](docs/draws.md), with accept/decline controls
- [SAN notation and PGN import/export](docs/pgn.md) for exchanging games with other tools
- [Save and resume complete games](docs/saved-games.md), including undo/redo history
- [Undo and redo](docs/undo-redo.md), including finished games and special moves

See [gameplay controls and validation](docs/gameplay.md) for draw claims, move history, resignation, and tests.

## Tests

```powershell
dotnet test src/Chess.sln --configuration Release -p:TreatWarningsAsErrors=true
```

The suite covers chess rules, tactical bot decisions, complete
saves and PGN, and WPF interactions such as automatic replies, suggestions, pause,
undo/redo, setup navigation, bundled fonts, and layout at smaller window sizes.
Server tests additionally exercise actual SignalR clients, seat credentials, room
isolation, authoritative moves, reconnects, expiry, and request limits.

## Validation and delivery

Pull requests to `main` and pushes to `main` run the GitHub Actions workflow:

1. Restore and build the solution on Windows.
2. Run all test projects in the solution and upload TRX results when available.
3. Publish and upload `Chess-win-x64.zip` as a workflow artifact.

The ZIP contains a self-contained Windows x64 build, so players do not need to install .NET.
Extract the full archive and run `ChessUI.exe`.

To publish a GitHub Release after merging and checking CI, tag a commit on `main`:

```powershell
git switch main
git pull --ff-only
git tag v0.1.0
git push origin v0.1.0
```

The tag workflow builds and validates the application before publishing the ZIP and
generated release notes. Tags outside `main` are rejected. No signing credentials are configured.
Use a new version tag for each release.

The `Windows build and tests` check is required before merging into `main`.

## Public hosting


The [public hosting guide](docs/public-hosting.md) explains the current Vercel +
Cloudflare Tunnel + PC setup and optional Render Free Docker deployment.
Render requested a payment card on the owner's account, so it is not the current
backend. Hosting files are already on main; there is no hosting PR prerequisite.
The public web game requires its backend to remain online.

Choose Easy (1 ply / 500 nodes), Medium (3 plies / 4,000 nodes), or Hard (5 plies / 30,000 nodes) before starting a computer game in Windows or the browser. Medium retains the original settings. These are search limits, not Elo ratings; the bot uses the last fully completed depth within its node budget. Suggestions use Medium. The web bot still runs on the chess server.

### Find a match

Web and Windows online setup offer **Find a match** as well as private room invitations. Two waiting connections are paired in arrival order with random colours. Search is cancellable; keep the page/window open. Disconnecting removes a waiting entry, and unrenewed entries expire after 30 seconds. If pairing wins a cancellation race, the assigned game opens so its seat is not lost. Matched games use the same private seat credentials, reconnect, draw and resignation rules as private rooms. There are no ratings or skill-based matching; two people must be searching on the same server. The queue and rooms are in memory and disappear on server restart.

