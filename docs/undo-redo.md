# Undo and redo

These file/history controls apply to offline Windows games. Browser and Windows
online matches do not support local game replacement or undo.

![Undo and redo controls beside the board](pgn.png)

Use **Undo** or **Ctrl+Z** to take back the last action. Use **Redo**, **Ctrl+Y**, or
**Ctrl+Shift+Z** to restore it. Each move is one action, including castling, en passant,
and promotion. A resignation or draw claim is a separate action: undo reopens the same
position before taking back any earlier move.

In a **Computer** game, the UI groups your move and its reply into a round. Undo during
thinking cancels the pending reply before taking back your move. Redo restores a round.
If navigation leaves the bot to move, use **Resume bot** when ready; it will not replace
your redo branch automatically. The `GameState` API continues to navigate one action.

Undo/redo restores the board, turn, piece movement flags, castling rights, en passant,
both move counters, move history, repetition counts, and game result. Undoing an automatic
ending takes back the move that caused it. Redoing that move shows the result again.
Undoing an intended-move draw claim does not play that move.

The controls work during play and from pause or result menus. Navigation closes the
previous overlay and shows the restored position or result. It also clears piece selection,
move highlights, and pending draw-claim controls.

History navigation is disabled while a promotion choice or FEN dialog is open.
In the FEN text box, undo and redo continue to edit text instead of moving through the game.

Playing a new move, resigning, or claiming a draw after undo discards the redo branch.
Rejected moves and invalid draw claims leave it intact. Restarting or loading FEN creates
a new timeline. A terminal position loaded directly from FEN has no earlier move to undo.
Navigation history is not included in FEN. Use [complete-game saves](saved-games.md) to preserve it on disk.

## Code API

```csharp
if (game.CanUndo) game.Undo();
if (game.CanRedo) game.Redo();
```

`Undo()` and `Redo()` return `false` when no action is available; the game is unchanged.
Successful actions preserve the existing `Board` and read-only `History` collection objects.
Individual piece objects may be replaced during restoration. Direct edits to `Board` are
not recorded; use `ApplyMove`, `Resign`, and `ClaimDraw` for tracked actions.

The regression suite covers special moves in both directions, promotion captures, repetition
and clock thresholds, draw claims, resignation, checkmate, stalemate, branching, controls,
and routed commands. Run it with `dotnet test src/Chess.sln --configuration Release`.
