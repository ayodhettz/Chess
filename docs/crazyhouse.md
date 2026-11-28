# Crazyhouse

Crazyhouse adds reserves to the existing custom chess engine. Capturing a piece
puts it in the capturer's reserve with the capturer's colour. A turn can instead
drop one reserved piece onto an empty square. The drop must leave your king safe.
Pawns cannot be dropped on either back rank; a dropped pawn on its home rank may
double-step. Dropped rooks do not regain castling rights. Captured promoted pieces
return to reserves as pawns. Drops may give check or immediate mate.

The variant follows the [Lichess Crazyhouse rules](https://lichess.org/variant/crazyhouse).
Like that implementation, it does not use automatic insufficient-material or
fifty/seventy-five-move draws. Repetition, draw agreements and resignation remain
available. A legal blocking drop prevents a false checkmate or stalemate result.

## Position and game files

Crazyhouse FEN appends reserves in brackets to piece placement, for example
`4k3/8/8/8/8/8/8/4K3[PNq] w - - 0 1`. Uppercase reserves belong to White.
An empty `[]` still identifies the variant. `Q~` marks a promoted pawn on the board.
Undo, redo and analysis copies preserve reserves and promotion provenance.

Drops use SAN such as `N@f7+` or `@e4`; `P@e4` is accepted on import.
PGN exports identify `[Variant "Crazyhouse"]`. Complete saves use version 5;
standard games keep the existing versions, and versions 1–4 remain readable.

The custom bot searches drops and values reserves. Search uses generated moves
without repeatedly formatting SAN for each speculative branch.
