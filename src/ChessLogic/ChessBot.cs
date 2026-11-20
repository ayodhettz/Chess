namespace ChessLogic;

public sealed record BotDecision(Move? Move, bool ClaimDraw, bool AcceptDraw, int Score, int Depth, int Nodes);

public sealed class ChessBot
{
    private const int Mate = 100_000;

    public Task<BotDecision> ChooseMoveAsync(GameState position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        cancellationToken.ThrowIfCancellationRequested();
        // Snapshot on the caller's thread before background analysis starts.
        GameState copy = position.CopyForAnalysis();
        return Task.Run(() => ChooseMove(copy, cancellationToken: cancellationToken), cancellationToken);
    }

    public BotDecision ChooseMove(GameState position, int maxDepth = 3, int nodeBudget = 4_000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (maxDepth is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        if (nodeBudget < 1) throw new ArgumentOutOfRangeException(nameof(nodeBudget));
        cancellationToken.ThrowIfCancellationRequested();
        GameState game = position.CopyForAnalysis();
        if (game.IsGameOver()) return new(null, false, false, 0, 0, 0);
        return new Search(game, nodeBudget, cancellationToken).Run(maxDepth);
    }

    private sealed class Search(GameState game, int budget, CancellationToken token)
    {
        private int nodes;

        public BotDecision Run(int maxDepth)
        {
            Move[] moves = OrderedMoves();
            bool claim = game.CanClaimDraw();
            BotDecision best = new(claim ? null : moves[0], claim, false, 0, 0, 0);
            // Keep only fully completed iterations when the budget runs out.
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                try
                {
                    int score = claim ? 0 : -Mate;
                    Move? chosen = claim ? null : moves[0];
                    bool chosenClaim = claim;
                    foreach (Move move in moves)
                    {
                        Visit();
                        bool intendedClaim = game.CanClaimDraw(move);
                        int value = Explore(move, depth - 1, -Mate, -score, 1);
                        if (intendedClaim && value < 0) value = 0;
                        if (value > score)
                        {
                            score = value;
                            chosen = move;
                            chosenClaim = intendedClaim && value == 0;
                        }
                    }
                    best = new(chosen, chosenClaim, false, score, depth, nodes);
                    if (score >= Mate - 100) break;
                }
                catch (BudgetExhaustedException) { break; }
            }
            bool accept = game.DrawOfferedBy == game.CurrentPlayer.Opponent() && best.Depth > 0 && best.Score <= 0;
            return best with { Move = accept ? null : best.Move, ClaimDraw = !accept && best.ClaimDraw,
                AcceptDraw = accept, Nodes = nodes };
        }

        private int Explore(Move move, int depth, int alpha, int beta, int ply)
        {
            game.ApplyMove(move);
            // The opponent's best score is the negative of ours.
            try { return -Negamax(depth, alpha, beta, ply); }
            finally { game.Undo(); }
        }

        private int Negamax(int depth, int alpha, int beta, int ply)
        {
            Visit();
            if (game.Result is Result result)
                return result.Winner == Player.None ? 0 : -Mate + ply;
            if (depth == 0) return Math.Max(Evaluate(game), game.CanClaimDraw() ? 0 : -Mate);

            int best = game.CanClaimDraw() ? 0 : -Mate;
            alpha = Math.Max(alpha, best);
            if (alpha >= beta) return best;
            foreach (Move move in OrderedMoves())
            {
                // Claiming is an alternative to playing the intended move.
                if (game.CanClaimDraw(move)) best = Math.Max(best, 0);
                alpha = Math.Max(alpha, best);
                // This branch cannot improve the opponent's existing choice.
                if (alpha >= beta) break;
                int score = Explore(move, depth - 1, -beta, -alpha, ply + 1);
                best = Math.Max(best, score);
                alpha = Math.Max(alpha, best);
                if (alpha >= beta) break;
            }
            return best;
        }

        private Move[] OrderedMoves() => game.AllLegalMoves(game.CurrentPlayer)
            .OrderByDescending(move => (move is PawnPromotion promotion ? Value(promotion.newType) : 0) +
                (move.Type == MoveType.EnPassant ? 100 : game.Board[move.To] is Piece victim
                    ? 10 * Value(victim.Type) - Value(game.Board.GetPiece(move.From).Type) : 0)).ToArray();

        private void Visit()
        {
            token.ThrowIfCancellationRequested();
            if (nodes >= budget) throw new BudgetExhaustedException();
            nodes++;
        }
    }

    private sealed class BudgetExhaustedException : Exception;

    private static int Value(PieceType type) => type switch
    {
        PieceType.Pawn => 100, PieceType.Knight => 320, PieceType.Bishop => 330,
        PieceType.Rook => 500, PieceType.Queen => 900, _ => 0
    };

    private static int Evaluate(GameState game)
    {
        int score = 0;
        foreach (Position square in game.Board.AllPositions())
        {
            Piece piece = game.Board.GetPiece(square);
            int center = 14 - Math.Abs(2 * square.Row - 7) - Math.Abs(2 * square.Column - 7);
            int advance = piece.Colour == Player.White ? 6 - square.Row : square.Row - 1;
            int bonus = piece.Type switch
            {
                PieceType.Pawn => 8 * advance + center,
                PieceType.Knight or PieceType.Bishop => 4 * center,
                _ => 0
            };
            score += (piece.Colour == game.CurrentPlayer ? 1 : -1) * (Value(piece.Type) + bonus);
        }
        return score;
    }
}
