# Online multiplayer and browser play

## Original first-playable scope

A Windows player creates a private game and sends an invitation link. Their friend
opens the link in a browser and plays without downloading the repository or installing
.NET. Browser-to-browser and Windows-to-Windows games use the same rooms.

Start with standard chess, two players, no clocks, and invitation-only rooms. Keep
the existing offline game and custom bot available. Accounts, ratings, matchmaking,
spectators, chat, and competitive anti-cheat are separate future work.

## Architecture

- `ChessLogic` remains the authority for legal moves and board outcomes. It already
  targets portable .NET 10; WPF does not enter the server or browser build.
- `Chess.Contracts` defines versioned JSON requests, seat tickets, and immutable room
  snapshots usable by the server and Windows client. Browser code follows the same schema.
- `Chess.Server` uses ASP.NET Core and SignalR. It owns each game's state, accepts
  player intents, validates the seat and expected revision, and publishes accepted state.
- A browser client renders FEN, legal destinations, players, and the scoresheet. It
  sends moves rather than calculating authoritative results. Serve it from the same
  origin as the hub; bundle dependencies locally rather than requiring a CDN at runtime.
- The WPF online mode renders server snapshots and submits commands through a shared
  connection service. Offline undo, bot analysis, local FEN replacement, and local
  resignation must not bypass server authority in online games.

Use one server instance initially. Its room state is in memory: a process restart
ends existing sessions. Add persistence and a distributed room coordinator before
running multiple replicas. SignalR groups alone are not authentication or a game lock.

## Original pull request milestones

1. **Plan:** architecture, protocol boundaries, delivery sequence, and acceptance criteria.
2. **Server foundation:** portable contracts, create/join private rooms, opaque seat
   credentials, authoritative moves including promotion, per-room revisions, disconnect/
   resume snapshots, bounded room lifetime/capacity, and real two-client integration tests.
   This PR is backend infrastructure, not a publicly hosted playable release.
3. **Complete online game actions:** current/intended draw claims, draw offers and
   responses, resignation from either side, terminal-state rules, and action retry
   semantics. Add tests for response ordering and command duplication before clients retry.
4. **Browser play:** room creation and invite joining, Tournament-style responsive
   board, orientation, legal highlights, promotion choice, moves, results, and all game
   actions. Include keyboard interaction and a mobile layout. No source checkout is
   required for a player. Keep session credentials out of invitation links.
5. **Windows integration:** an Online option and room lobby; connect through the same
   hub, render the same snapshots, and disable incompatible offline controls. Verify a
   browser player and a Windows player can finish a game together.
6. **Recovery and deployment:** automatic reconnect with full-state refresh, expired/
   abandoned room UX, deployed HTTPS host, origin/rate limits, health checks, container
   packaging, and repeatable deployment. Select a hosting provider and spending limit
   before creating paid resources. Configure deployment secrets through the host/CI.

### Current delivery status

Server foundations, game actions, browser play, Docker hosting support, browser
computer games, and Windows online integration are on main (through PR #29).
The milestones above record the original sequence rather than pending work.
CI exercises browser/browser and browser/Windows play and recovery.

The public preview uses Vercel and a Cloudflare Tunnel to the owner's PC.
Render requested card details and is not the current backend. See
[public hosting](public-hosting.md); no hosting PR remains to be merged.

Difficulty selection and cancellable random matchmaking are implemented alongside
private invitations. Match search pairs two waiting connections with random colours;
there are no ratings or skill-based matching. New features require deploying the
matching server and frontend, not merely merging their code.

Accounts, spectators, chat, persistent rooms, and hosting independent of the
owner's PC remain future work. Rooms and the proposed queue are process-local.

## Protocol and trust

- Room codes identify invitations; they do not authorize an occupied seat. Use
  cryptographically random, unguessable seat credentials and send them only to their owner.
- Bind each connection to one seat. On reconnect, prove possession of that seat's
  credential and replace the old connection. Never trust a client-supplied player colour.
- Move requests contain origin, destination, optional promotion, and an expected room
  revision. Resolve against server-generated legal moves, never execute a client move object.
- Snapshots include a monotonically increasing revision, FEN, SAN history, presence,
  room status, and result. Clients ignore snapshots older than their current revision.
- Reject illegal, stale, out-of-turn, unseated, full-room, and expired-room operations.
  A rejected command must not alter the game. On ambiguity, fetch current state rather
  than blindly replaying a move. Version 2 adds bounded per-seat command receipts;
  retries must keep the exact original ID, revision, action, and payload.
- Use HTTPS/WSS for hosted play. Bound request sizes, room count, game length, and room
  lifetime. Apply origin checks to WebSockets as well as HTTP endpoints; do not use
  permissive credentialed CORS. Do not log seat credentials or put them in query strings.
- Move suggestions remain an offline Windows feature. Browser computer games run
  the custom bot on the server; neither client exposes hints in human online matches. Legal validation
  prevents illegal moves, but does not prevent players from using external assistance.

## Verification and delivery

Windows CI builds/tests and publishes the desktop ZIP. A Linux job builds, tests,
and publishes the server independently of WPF and verifies the hosting container.
Integration tests exercise actual SignalR clients,
not just direct hub method calls: room isolation, both seats receiving accepted moves,
rejected commands leaving state unchanged, promotion, reconnect replacement, and expiry.

Before a hosted release, complete browser/browser and browser/Windows games covering
castling, en passant, promotion, mate, stalemate, draws, resignation, and disconnections.
Check narrow screens and keyboard play. Reconnect must recover board, turn, revision,
result, and move history without applying any move twice. Verify rejected credentials
cannot obtain a seat or another room's state. Measure idle and active hosting costs.

Hosting will require a public domain/URL, HTTPS, WebSocket support, and a continuously
running ASP.NET process. The first deployment remains single-instance; durable match
recovery and horizontal scaling need an explicit follow-up design.

## References

- [SignalR hubs](https://learn.microsoft.com/aspnet/core/signalr/hubs?view=aspnetcore-10.0)
- [SignalR configuration](https://learn.microsoft.com/aspnet/core/signalr/configuration?view=aspnetcore-10.0)
- [SignalR security considerations](https://learn.microsoft.com/aspnet/core/signalr/security?view=aspnetcore-10.0)
