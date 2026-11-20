# Complete-game save files

Use **File → Save game…** to write a `.chess.json` file, and **File → Load game…** to resume it.
Saving includes the starting FEN, played actions, game result, repetition state, and the entire
remaining redo branch. A successful load replaces the current game and clears UI selection.
An invalid file leaves the current game and menu untouched. File operations are unavailable
during promotion or while the FEN editor is open; pause and result menus support them.
File operations also wait for active bot/suggestion searches to finish or be cancelled.
Loading uses the current match mode and human colour; those UI choices are not stored
in the chess JSON. A loaded bot turn receives an automatic reply.

Files use versioned JSON. The loader validates and replays every action, including undone future
actions, before replacing the game. Special moves, resignation, and current/intended-move draw
claims therefore pass through the same rule checks as normal play. FEN import remains position-only.

Saving writes a temporary file beside the destination and replaces the old file only after the
replacement is complete. Failed writes do not deliberately remove the previous save.

Limits are 1 MiB and 1,024 actions per file. Unknown versions, unknown fields, illegal moves,
invalid claims, bad promotion choices, and invalid undo cursors are rejected. Save files preserve
the active timeline, not variations discarded by making a new move after undo. Direct board edits
are not tracked. Use normal game actions and FEN loading for positions that need to be saved.

Version 1 files remain supported. Files containing PGN metadata or recorded PGN results use
version 2; those headers and undoable recorded outcomes survive complete-game saving as well.
Files with draw-offer actions use version 3, preserving pending offers and responses through undo/redo.

The code APIs are `GameSave.Serialize` / `GameSave.Deserialize` for text and `GameFiles.Save` /
`GameFiles.Load` for files. Loading returns a new `GameState`; it never mutates an existing one.
