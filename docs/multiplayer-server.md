# Multiplayer server foundation

This is backend infrastructure for private browser/Windows rooms. There is no browser
board, Windows online UI, draw/resignation command, or public deployment in this PR.
Existing offline play remains available. The next milestone completes online game
actions, followed by browser play, WPF integration, and hosted recovery.

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

## Protocol version 1

JSON uses camelCase properties and string enums (`white`, `black`, `queen`, `knight`,
`waiting`, `active`, `finished`, `closed`). `Chess.Contracts` defines the wire records.

| Hub method | Arguments | Response |
| --- | --- | --- |
| `CreateRoom` | preferred seat: `white` or `black` | private `SeatTicket` |
| `JoinRoom` | room code | private `SeatTicket` for the vacant seat |
| `ResumeRoom` | room code, credential | private `SeatTicket`; replaces the old connection |
| `GetState` | none | current seated room snapshot |
| `SubmitMove` | `{ expectedRevision, move: { from, to, promotion? } }` | accepted snapshot |

`RoomChanged` sends snapshots to the room's currently bound player connections. A
ticket contains the room code, seat, credential, and state. Only that method's caller
receives its credential. Credentials are 256-bit random values, hashed in memory;
they must never enter invitation URLs, logs, or another player's snapshot.

Snapshots contain protocol version, code, revision, status, each player's connected
flag, turn, FEN, ordered SAN moves from the standard start, legal move intents, result,
optional closure reason, and expiry time. `Result.reason` uses ChessLogic end-reason
names, such as `Checkmate`. The winner is `null` for a draw. There are no custom FEN starts.

The revision increases for seat binding, presence changes, and accepted moves. Clients
must ignore older snapshots because concurrent delivery can arrive out of order.
Include the latest revision with a move. On a stale response, obtain fresh state;
do not automatically retry commands in version 1. Promotion must be explicit on a
promotion move, including underpromotion, and absent on other moves.

Both players must be connected to play. Disconnection reserves the seat and pauses
move acceptance; a valid credential can resume it. A stale connection loses its seat
binding. Room codes identify an invitation but cannot reclaim an occupied seat.

Errors are SignalR `HubException` messages prefixed by stable codes: `not_seated`,
`already_seated`, `room_unavailable`, `room_expired`, `room_full`, `invalid_credential`,
`stale_revision`, `wrong_turn`, `invalid_move`, `opponent_unavailable`, `game_finished`,
`capacity`, and `rate_limit`. Rejected commands leave the chess position unchanged.

## Limits and deployment boundary

Defaults: 128 rooms, 512 connections, 512 plies per game, a 15-minute waiting-room
lifetime, and a four-hour hard game lifetime measured from creation. Each connection
can make 60 room calls per ten seconds. Incoming hub messages are limited to 4 KiB.
Configure capacity and durations under `Rooms` (for example `Rooms__MaxRooms`).

A game reaching its move limit closes with `move_limit`; this is a room closure,
not a chess draw. Expiry closes with `expired` and releases seats/capacity during a
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
