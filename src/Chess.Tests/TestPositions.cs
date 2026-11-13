using ChessLogic;

namespace Chess.Tests;

internal static class TestPositions
{
    internal static Position At(string square) => new(8 - (square[1] - '0'), square[0] - 'a');

    internal static Move FindMove(GameState game, string from, string to, PieceType? promotion = null)
    {
        return game.LegalMoves(At(from)).Single(move => move.To == At(to) &&
            (move is not PawnPromotion pawnPromotion || pawnPromotion.newType == (promotion ?? PieceType.Queen)));
    }

    internal static void Play(GameState game, string from, string to, PieceType? promotion = null)
        => game.ApplyMove(FindMove(game, from, to, promotion));

    internal static Board Parse(string placement, bool hasMoved = true)
    {
        Board board = new();
        string[] ranks = placement.Split('/');
        for (int row = 0; row < 8; row++)
        {
            int col = 0;
            foreach (char symbol in ranks[row])
            {
                if (char.IsDigit(symbol))
                {
                    col += symbol - '0';
                    continue;
                }

                Player player = char.IsUpper(symbol) ? Player.White : Player.Black;
                Piece piece = char.ToLowerInvariant(symbol) switch
                {
                    'k' => new King(player),
                    'q' => new Queen(player),
                    'r' => new Rook(player),
                    'b' => new Bishop(player),
                    'n' => new Knight(player),
                    'p' => new Pawn(player),
                    _ => throw new ArgumentException("Invalid piece symbol.")
                };
                piece.HasMoved = hasMoved;
                board[row, col++] = piece;
            }
        }
        return board;
    }

    internal static void KnightCycle(GameState game)
    {
        Play(game, "g1", "f3");
        Play(game, "g8", "f6");
        Play(game, "f3", "g1");
        Play(game, "f6", "g8");
    }

    internal static GameState QuietGame(int plies)
    {
        GameState game = new(Parse("r3k3/8/8/8/8/8/8/R3K3"), Player.White);
        Random random = new(42);
        Dictionary<string, int> visits = new();

        for (int ply = 0; ply < plies; ply++)
        {
            Move move = game.AllLegalMoves(game.CurrentPlayer)
                .Where(candidate => game.Board.IsEmpty(candidate.To))
                .OrderBy(_ => random.Next())
                .First(candidate =>
                {
                    Board copy = game.Board.Copy();
                    candidate.Execute(copy);
                    return !copy.IsInCheck(game.CurrentPlayer.Opponent()) &&
                        visits.GetValueOrDefault(Key(copy, game.CurrentPlayer.Opponent())) < 3;
                });
            game.ApplyMove(move);
            string key = Key(game.Board, game.CurrentPlayer);
            visits[key] = visits.GetValueOrDefault(key) + 1;
        }
        return game;
    }

    private static string Key(Board board, Player player) => player + string.Join(";",
        board.AllPositions().Select(pos => $"{pos.Row},{pos.Column},{board[pos].Type},{board[pos].Colour}"));
}
