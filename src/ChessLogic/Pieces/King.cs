using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class King : Piece
    {
        public override PieceType Type => PieceType.King;
        public override Player Colour { get; }
        private static readonly Direction[] directions = new Direction[]
        {
            Direction.North, Direction.NorthEast, Direction.East, Direction.SouthEast,
            Direction.South, Direction.SouthWest, Direction.West, Direction.NorthWest
        };
        public King(Player colour)
        {
            Colour = colour;
        }
        public override Piece Copy()
        {
            return new King(Colour) { HasMoved = this.HasMoved };
        }

        private IEnumerable<Position> ValidMovePositions(Position from, Board board)
        {
            foreach (Direction dir in directions)
            {
                Position to = from + dir;
                if (Board.IsInside(to) && (board.IsEmpty(to) || board[to].Colour != Colour))
                {
                    yield return to;
                }
            }
        }

        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            foreach(Position to in ValidMovePositions(from, board))
            {
                yield return new NormalMove(from, to);
            }
        }
    }
}
