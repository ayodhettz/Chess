using System.Text;

namespace ChessLogic
{
    internal static class PositionKey
    {
        internal static string Create(Board board, Player player)
        {
            StringBuilder key = new StringBuilder();
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Piece piece = board[row, col];
                    key.Append(piece == null ? '.' : (char)('A' + (int)piece.Type + 6 * (int)piece.Colour));
                }
            }

            key.Append('|').Append(player);
            AppendCastlingRights(key, board, Player.White, 7);
            AppendCastlingRights(key, board, Player.Black, 0);

            // A skip square changes repetition only when en passant is legal.
            Position? skip = board.GetPawnSkipPosition(player.Opponent());
            if (skip != null && board.PositionsOfPlayer(player)
                .Where(pos => board[pos] is Pawn)
                .SelectMany(pos => board[pos].GetMoves(pos, board))
                .Any(move => move.Type == MoveType.EnPassant && move.IsLegal(board)))
            {
                key.Append('|').Append(skip.Row).Append(',').Append(skip.Column);
            }

            return key.ToString();
        }

        private static void AppendCastlingRights(StringBuilder key, Board board, Player player, int row)
        {
            bool kingUnmoved = board[row, 4] is King king && king.Colour == player && !king.HasMoved;
            foreach (int col in new[] { 0, 7 })
            {
                bool rookUnmoved = board[row, col] is Rook rook && rook.Colour == player && !rook.HasMoved;
                key.Append(kingUnmoved && rookUnmoved ? '1' : '0');
            }
        }
    }
}
