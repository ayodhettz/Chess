# Multiplayer server

The server hosts the [browser client](browser-play.md), private rooms, moves,
draw offers/claims, resignation, and retry receipts. The [Windows online client](windows-online.md)
uses the same rooms. Offline desktop play remains available.

## Run locally

Install .NET SDK **10.0.404 or a newer .NET 10 SDK**. The server/test packages use
the patched 10.0.15 release; keep the shared ASP.NET runtime updated for hosted play.

```powershell
dotnet run --project src/Chess.Server/Chess.Server.csproj --urls http://127.0.0.1:5080
dotnet test src/Chess.Server.Tests/Chess.Server.Tests.csproj --configuration Release -p:TreatWarningsAsErrors=true
```

`GET /health` returns service status and protocol version. SignalR is at `/hubs/game`.
This local HTTP command is for development; terminate HTTPS/WSS correctly before
exposing a server publicly. Browser origins must match the request's scheme and host.
Cross-origin browser connections are rejected, including WebSocket requests. Native
clients without an Origin header can connect, but still need a room seat to act.

## Protocol version 2

JSON uses camelCase properties and string enums (`white`, `black`, `queen`, `knight`,
`waiting`, `active`, `finished`, `closed`). `Chess.Contracts` defines the wire records.

| Hub method | Arguments | Response |
| --- | --- | --- |
| `CreateRoom` | preferred seat: `white` or `black` | private `SeatTicket` |
| `JoinRoom` | room code | private `SeatTicket` for the vacant seat |
| `ResumeRoom` | room code, credential | private `SeatTicket`; replaces the old connection |
| `GetState` | none | current seated room snapshot |
| `SubmitMove` | `{ expectedRevision, move: { from, to, promotion? } }` | accepted snapshot |
| `SubmitCommand` | `{ id, expectedRevision, action, move? }` | `CommandResult` receipt and latest snapshot |

New clients should use `SubmitCommand` for all game actions. Its `id` is a non-empty
UUID, generated once per intent. Actions are `move`, `offerDraw`, `acceptDraw`,
`declineDraw`, `claimDraw`, and `resign`. Only `move` and an intended-move `claimDraw`
accept a move payload. A current-position claim has no move payload.

`RoomChanged` sends snapshots to the room's currently bound player connections. A
ticket contains the room code, seat, credential, and state. Only that method's caller
receives its credential. Credentials are 256-bit random values, hashed in memory;
they must never enter invitation URLs, logs, or another player's snapshot.

Snapshots contain protocol version, code, revision, status, each player's connected
flag, turn, FEN, ordered SAN moves from the standard start, legal move intents, result,
optional closure reason, and expiry time. `draw` contains `offeredBy`, current-side
`canOffer` / `canClaim` flags, and legal `claimMoves` for intended claims. `Result.reason`
uses ChessLogic names, such as `Checkmate`. The winner is `null` for a draw; resignation
also identifies `resignedBy`. There are no custom FEN starts. Clients must verify the
protocol version before enabling play.

The revision increases for seat binding, presence changes, and accepted commands.
Clients accept snapshots only for their current room and ignore older revisions,
because concurrent delivery can arrive out of order. Include the latest revision
with a new intent. On a stale response, obtain fresh state. Promotion must be explicit on a
promotion move, including underpromotion, and absent on other moves.

An exact retry of an accepted `SubmitCommand` returns `wasDuplicate: true`, its original
`appliedRevision`, and the latest `state`; it does not mutate or broadcast again. The
last 128 accepted receipts per seat survive disconnect/resume and finished games until
room expiry. Keep every argument unchanged when retrying, including the ID and expected
revision. Reusing a retained ID with different arguments is `command_conflict`. Beyond
the receipt window, the original revision is stale and the client must resync instead.
Never recycle IDs or change the revision on a retry. Rejected commands do not consume IDs.
The older `SubmitMove` method remains available but has no client retry receipt.

Both seats must be occupied before any game action. Moves and draw offers require
both players connected; resignation, valid claims, and offer responses may finish or
update an active game while the opponent is offline. Moves, offers, and claims belong
to the current side; either side can resign, and only the opponent may respond to an
offer. Claims use the shared repetition/half-move history; intended claims end the game
without playing their move. An opponent move implicitly declines an offer.

Disconnection reserves a seat; a valid credential can resume it. A stale connection
loses its seat binding. Room codes cannot reclaim occupied seats.

Errors are SignalR `HubException` messages prefixed by stable codes: `not_seated`,
`already_seated`, `room_unavailable`, `room_expired`, `room_full`, `invalid_credential`,
`stale_revision`, `wrong_turn`, `invalid_move`, `opponent_unavailable`, `game_finished`,
`capacity`, `rate_limit`, `game_not_started`, `invalid_command`, `invalid_action`, and
`command_conflict`. Rejected commands leave the chess position unchanged.

## Limits and deployment boundary

Defaults: 128 rooms, 512 connections, 512 plies and 1,024 accepted commands per game, a 15-minute waiting-room
lifetime, and a four-hour hard game lifetime measured from creation. Each connection
can make 60 room calls per ten seconds. Incoming hub messages are limited to 4 KiB.
Configure capacity and durations under `Rooms` (for example `Rooms__MaxRooms`).

A game reaching its move limit closes with `move_limit`; this is a room closure,
not a chess draw. The command bound similarly closes with `command_limit`; genuine
terminal results on the final action take precedence over these bounds. Expiry closes with `expired` and releases seats/capacity during a
30-second cleanup sweep. Commands reject expired rooms immediately. Automatic chess
outcomes still come from ChessLogic. Room state is in memory and lost on restart.

This implementation is single-process. It serializes mutations before publishing
immutable snapshots. Add durable room state and coordination before multiple replicas.
Per-connection limits are not perimeter protection: public hosting still requires
edge/IP limits, trusted proxy/HTTPS configuration, operational monitoring, and client
expiry/reconnect UX. No paid hosting resources or deployment credentials are created here.

Windows CI continues to build/test the complete solution and publish the desktop app.
An independent Linux workflow tests the server without WPF and publishes
`Chess-server-linux-x64.tar.gz`. Extract that archive and run `./Chess.Server --urls
http://127.0.0.1:5080` on a compatible Linux host for local validation.

### Computer rooms

`CreateComputerRoom(humanSeat)` returns a normal private human seat ticket with the
opposite seat reserved for the custom engine. `RoomSnapshot.computerSeat` is an
optional additive field in protocol 2; existing friend rooms return null. Other
players cannot join the computer seat or obtain its credential. Computer rooms use
the same room limits, expiry, command receipts, result rules, and resume mechanism.
