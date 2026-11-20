# Our first chess bot

![The bot's opening move and search summary](bot.png)

Use **Bot → Play bot move** to let the bot play the side whose turn it is.
Play your own move, then use the command again for its reply. It also works on loaded
FEN positions and saved games. This first version is invoked one turn at a time;
automatic opponent mode and difficulty selection are future work.

The search runs in the background. **Escape** or **Bot → Cancel thinking** cancels it
without changing the position. Board actions, file operations, and history navigation
are disabled while it thinks. Closing the window cancels the search too.

## How it chooses a move

The search and scoring are implemented here in C#, in `ChessLogic/ChessBot.cs`.
There is no external engine, opening book, network service, or trained model.
It reuses this project's legal-move generation and game rules.

1. **Copy the position.** Keep the real game untouched while exploring. The copy
   includes repetition counts, move counters, and pending draw offers.
2. **Try legal moves.** For each move, search the opponent's replies and our responses.
   One move by one player is one *ply*. A depth of three means our move, their reply,
   then our next move.
3. **Score the leaves.** Material is measured in hundredths of a pawn: pawn 100,
   knight 320, bishop 330, rook 500, queen 900. Small bonuses reward pawn advancement
   and central squares for pawns, knights, and bishops. The score is relative to the
   player whose turn it is at that position. Checkmate overrides material; quicker
   mates and longer resistance to a forced loss are preferred. Draws score zero.
4. **Assume the opponent chooses their best reply.** Negamax implements this by
   negating each child's score. A tempting capture can be rejected when the opponent
   can win the capturing piece on the next move.
5. **Skip branches that cannot improve the answer.** Alpha-beta pruning uses scores
   already found to stop exploring inferior alternatives. Captures and promotions
   are searched first to help it find useful bounds sooner.
6. **Deepen gradually.** Search depth one, then two, then three. The default budget
   is 4,000 counted search visits across all iterations. If it runs out, keep the
   last fully completed depth. If even depth one cannot finish, use the first legal
   move in capture/promotion order. The displayed depth is the depth actually completed.

For example, taking a pawn initially gains 100 points, but if the opponent can then
capture our rook, the two-ply search sees the larger loss and can choose another move.

## Draws, history, and saves

A valid current or intended-move draw claim is another zero-score choice. The bot can
claim instead of continuing a losing line, or continue when it finds something better.
It accepts an opponent's pending offer when a completed search scores the position
at zero or below; otherwise its move declines the offer through the normal rules.
It does not initiate offers or resign in this version.

The selected action is applied through the normal game methods, so SAN history,
undo/redo, draw handling, complete saves, and PGN work as they do for human actions.
The bot's search tree is temporary and is not stored in a save file.

## Current limits and next steps

This is a deterministic baseline, not a rated engine. Its score is a rough evaluation,
not a winning probability. A node budget bounds search visits, not elapsed time;
cost varies with the position and computer. Cancellation is checked during search.

The shallow horizon can miss tactics just beyond its depth. There is no quiescence
search, transposition table, advanced king safety, or endgame knowledge yet. Next steps
are automatic play with a chosen human colour, quiescence search to finish tactical
exchanges before scoring, and faster search to support deeper difficulty levels.

Tests cover mates for both colours, captures and defended pieces, promotion, draw
choices, bounded search, cancellation, snapshot isolation, and WPF history integration.
