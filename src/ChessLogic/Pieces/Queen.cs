using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Queen : Piece
    {
        public override PieceType Type => PieceType.Queen;
        public override Player Colour { get; }
        public static readonly Direction[] dirs = new Direction[]
        {
            Direction.North,
            Direction.NorthEast,
            Direction.East,
            Direction.SouthEast,
            Direction.South,
            Direction.SouthWest,
            Direction.West,
            Direction.NorthWest
        };
        public Queen(Player colour)
        {
            Colour = colour;
        }
        public override Piece Copy()
        {
            return new Queen(Colour) { HasMoved = this.HasMoved };
        }
        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return MovePositionsinDirs(from, board, dirs)
                .Select(to => new NormalMove(from, to));
        }
    }
}
