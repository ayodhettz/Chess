# Online play in the Windows app

Choose **Play online** on the Windows start screen. The existing **Computer** and
**Local two-player** modes keep their original offline controls and custom bot.

## Create or join

The default server is https://ayodhettz-chess.vercel.app. You can enter another
HTTPS server address, or use HTTP localhost for development. The public server
currently runs through a Cloudflare tunnel from its owner's PC; it must be awake.

Choose White or Black and **Create room**, then copy the invitation to a friend.
They can open it in a browser or paste it into another Windows client's lobby.
To join, paste a room code or a complete invitation and choose **Join game**. A
complete invitation sets the server address before connecting.

The board faces your seat and highlights only server-provided legal destinations.
Select a piece and destination; promotion asks for all four choices. Arrow keys
navigate squares; Enter/Space activates a square. The scoresheet shows server SAN.
Moves display after acceptance, without optimistic local changes.

## Actions and recovery

Draw offers, acceptance/decline, current-position claims, intended-move claims, and
resignation all use the same versioned commands as the browser. Select **Claim with
an intended move**, then a qualifying destination, to claim without playing it.
Resignation can be sent off turn and asks for confirmation.

Automatic reconnect proves the existing seat credential and refreshes the complete
snapshot. **Reconnect** retries after automatic attempts stop. If a command's response
is lost, **Recover pending action** reuses its exact original ID, revision, action,
and payload, so the server can return its receipt without duplicating a move.

The credential and pending action live in this window's memory. Keep it open: closing
or restarting the app loses access to that reserved seat. The window warns before
leaving. Invitations contain only the room code. The app never places credentials
in the invitation, clipboard, URL, or diagnostic output.

Server restarts and room expiry still end sessions. Online games do not expose local
undo, FEN replacement, file loading, bot hints, or other offline-only game mutation.
Use **New game** to return to setup. There are no accounts or ratings.
Choose **Find a match** and **Cancel search** in the lobby to search for a random
opponent. Colours are assigned randomly; both players must use the same server.
A paired game uses the existing private seat and recovery rules.
See the [hosting guide](public-hosting.md) for the public server's PC dependency.

## Client and checks

`Chess.Client.OnlineSession` is portable .NET 10 code shared by the native UI and
integration tests. It uses the official SignalR client, shared `Chess.Contracts`,
strict HTTPS addresses, and long polling to work through the current public proxy.
JSON serialization and rule authority stay on the existing custom C# server.

Server integration tests cover independent peers, both-seat actions, draw agreement,
origin/code validation, and a deliberately dropped accepted-command response.
Chromium tests play actual browser games against the production native connection
client in both colours. `tools/Chess.Client.Smoke` is that test peer: its output omits
seat credentials. Windows CI also builds/tests/publishes the WPF application.
