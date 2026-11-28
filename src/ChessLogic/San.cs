namespace ChessLogic
{
    public static class San
    {
        public static string Format(GameState game, Move requested)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(requested);
            Move[] legal = game.AllLegalMoves(game.CurrentPlayer).ToArray();
            Move move = legal.SingleOrDefault(candidate => candidate.From == requested.From && candidate.To == requested.To &&
                candidate.Type == requested.Type && (candidate is not PawnPromotion promotion ||
                    requested is PawnPromotion selected && selected.newType == promotion.newType))
                ?? throw new ArgumentException("The move is not legal in this position.", nameof(requested));
            return FormatLegal(game, move, legal);
        }

        public static Move Parse(GameState game, string text)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(text);
            string input = text.Trim();
            if (input.Length is 0 or > 32) throw new FormatException("Invalid SAN move.");
            string clean = input.TrimEnd('!', '?');
            if (input.Length - clean.Length > 2) throw new FormatException("Invalid SAN annotation.");
            clean = clean.Replace('0', 'O');
            if (clean.StartsWith("P@", StringComparison.Ordinal)) clean = clean[1..];
            bool hasSuffix = clean.EndsWith('+') || clean.EndsWith('#');
            Move[] legal = game.AllLegalMoves(game.CurrentPlayer).ToArray();
            Move[] matches = legal.Where(move =>
            {
                string canonical = FormatLegal(game, move, legal);
                return clean == (hasSuffix ? canonical : canonical.TrimEnd('+', '#'));
            }).ToArray();
            return matches.Length == 1 ? matches[0] : throw new FormatException($"SAN move '{text}' is illegal or ambiguous.");
        }

        private static string FormatLegal(GameState game, Move move, Move[] legal)
        {
            Piece piece = move is DropMove reserve ? Board.CreatePiece(reserve.PieceType, reserve.Player) : game.Board.GetPiece(move.From);
            string body;
            if (move is DropMove dropped) body = (dropped.PieceType == PieceType.Pawn ? "" : Letter(dropped.PieceType).ToString()) + "@" + Square(move.To);
            else if (move.Type == MoveType.CastlingKS) body = "O-O";
            else if (move.Type == MoveType.CastlingQS) body = "O-O-O";
            else
            {
                bool capture = !game.Board.IsEmpty(move.To) || move.Type == MoveType.EnPassant;
                string origin = "";
                if (piece is Pawn)
                {
                    if (capture) origin = File(move.From).ToString();
                }
                else
                {
                    origin = Letter(piece.Type).ToString();
                    Move[] others = legal.Where(candidate => candidate is not DropMove && candidate.To == move.To && candidate.From != move.From &&
                        game.Board.GetPiece(candidate.From).Type == piece.Type).ToArray();
                    if (others.Length > 0)
                    {
                        if (others.All(candidate => candidate.From.Column != move.From.Column)) origin += File(move.From);
                        else if (others.All(candidate => candidate.From.Row != move.From.Row)) origin += Rank(move.From);
                        else origin += Square(move.From);
                    }
                }
                body = origin + (capture ? "x" : "") + Square(move.To);
                if (move is PawnPromotion promotion) body += "=" + Letter(promotion.newType);
            }

            Board copy = game.Board.Copy();
            copy.SetPawnSkipPosition(game.CurrentPlayer, null);
            copy.ExecuteMove(move, game.CurrentPlayer);
            Player opponent = game.CurrentPlayer.Opponent();
            if (!copy.IsInCheck(opponent)) return body;
            // Checkmate depends on legal replies, not automatic draw status.
            bool reply = new GameState(copy, opponent).Result?.Reason != EndReason.Checkmate;
            return body + (reply ? "+" : "#");
        }

        private static char File(Position pos) => (char)('a' + pos.Column);
        private static char Rank(Position pos) => (char)('8' - pos.Row);
        private static string Square(Position pos) => $"{File(pos)}{Rank(pos)}";
        private static char Letter(PieceType type) => type switch
        {
            PieceType.King => 'K', PieceType.Queen => 'Q', PieceType.Rook => 'R',
            PieceType.Bishop => 'B', PieceType.Knight => 'N',
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
