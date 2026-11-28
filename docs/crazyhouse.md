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

## Playing

Choose **Crazyhouse** under **Rules** before starting a Windows local/computer game
or a browser game. The Windows online lobby also offers Standard and Crazyhouse.
Private invitations use the host's rules automatically; random searches pair only
players searching for the same variant. All three bot difficulties support drops.

Your reserve shows how many captured pieces you can use. Select a reserve piece,
then a highlighted empty square to drop it instead of moving a board piece.
The opponent's reserve is visible too. In local two-player games, “Your reserve”
follows the side to move. Online drops are validated by the server and recovered
with the rest of the position after reconnect or browser reload.

Rematches keep the variant. Windows saves, PGN and FEN identify Crazyhouse
automatically when loaded; the opponent and human colour still follow UI settings.

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
