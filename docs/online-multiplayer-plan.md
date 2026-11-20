# Online multiplayer and browser play

## First playable release

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

## Pull request milestones

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

Each implementation PR targets `main`, uses conventional commits, and includes its
relevant tests and documentation. Do not deploy or advertise a public multiplayer
release until the cross-client and recovery acceptance checks pass.

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
- Suggestion/bot assistance remains an offline feature in this release. Legal validation
  prevents illegal moves, but does not prevent players from using external assistance.

## Verification and delivery

Keep Windows build/tests and the desktop ZIP. Add a Linux job that builds, tests,
and publishes the server independently of WPF. Exercise actual SignalR clients,
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
