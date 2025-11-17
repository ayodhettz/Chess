using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Bishop : Piece
    {
        public override PieceType Type => PieceType.Bishop;
        public override Player Colour { get; }

        private static readonly Direction[] dirs = new Direction[]
        {
            Direction.NorthEast,
            Direction.NorthWest,
            Direction.SouthEast,
            Direction.SouthWest
        };
        public Bishop(Player colour)
        {
            Colour = colour;
        }
        public override Piece Copy()
        {
            return new Bishop(Colour) { HasMoved = this.HasMoved };
        }
        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return MovePositionsinDirs(from, board, dirs)
                .Select(to => new NormalMove(from, to));
        }
    }
}
