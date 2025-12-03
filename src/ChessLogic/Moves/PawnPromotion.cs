using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class PawnPromotion : Move
    {
        public override MoveType Type => MoveType.PawnPromotion;
        public override Position From { get; }
        public override Position To { get; }
        public readonly PieceType newType;
        public PawnPromotion(Position from, Position to, PieceType newType)
        {
            From = from;
            To = to;
            this.newType = newType;
        }

        private Piece CreatePromotedPiece(Player colour)
        {
            return newType switch
            {
                PieceType.Queen => new Queen(colour),
                PieceType.Rook => new Rook(colour),
                PieceType.Bishop => new Bishop(colour),
                PieceType.Knight => new Knight(colour),
                _ => throw new ArgumentException("Invalid piece type for promotion"),
            };
        }

        public override void Execute(Board board)
        {
            Piece pawn = board[From];
            board[From] = null;

            Piece promotedPiece = CreatePromotedPiece(pawn.Colour);
            promotedPiece.HasMoved = true;
            board[To] = promotedPiece;
        }
    }
}
