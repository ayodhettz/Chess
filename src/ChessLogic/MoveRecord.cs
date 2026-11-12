namespace ChessLogic
{
    public sealed class MoveRecord
    {
        public Player Player { get; }
        public PieceType PieceType { get; }
        public MoveType MoveType { get; }
        public Position From { get; }
        public Position To { get; }
        public PieceType? CapturedPieceType { get; }
        public PieceType? PromotionType { get; }

        internal MoveRecord(Player player, PieceType pieceType, Move move, PieceType? capturedPieceType)
        {
            Player = player;
            PieceType = pieceType;
            MoveType = move.Type;
            From = move.From;
            To = move.To;
            CapturedPieceType = capturedPieceType;
            PromotionType = (move as PawnPromotion)?.newType;
        }

        public override string ToString()
        {
            if (MoveType == MoveType.CastlingKS) return "O-O";
            if (MoveType == MoveType.CastlingQS) return "O-O-O";

            string from = $"{(char)('a' + From.Column)}{8 - From.Row}";
            string to = $"{(char)('a' + To.Column)}{8 - To.Row}";
            string separator = CapturedPieceType.HasValue ? "x" : "-";
            string promotion = PromotionType.HasValue ? $"={PromotionType}" : "";
            string enPassant = MoveType == MoveType.EnPassant ? " e.p." : "";
            return $"{from}{separator}{to}{promotion}{enPassant}";
        }
    }
}
