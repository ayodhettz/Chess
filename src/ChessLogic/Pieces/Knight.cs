using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Knight : Piece
    {
        public override PieceType Type => PieceType.Knight;
        public override Player Colour { get; }
        public Knight(Player colour)
        {
            Colour = colour;
        }
        public override Piece Copy()
        {
            return new Knight(Colour) { HasMoved = this.HasMoved };
        }

        private static IEnumerable<Position> PotentialToPositions(Position from)
        {
            foreach(Direction vertical in new Direction[] { Direction.North, Direction.South })
            {
                foreach(Direction horizontal in new Direction[] { Direction.East, Direction.West })
                {
                    yield return from + vertical + vertical + horizontal;
                    yield return from + vertical + horizontal + horizontal;
                }
            }
        }

        private IEnumerable<Position> ValidMovePositions(Position from, Board board)
        {
            return PotentialToPositions(from).Where(pos => Board.IsInside(pos) && (board.IsEmpty(pos) || board[pos].Colour != Colour));
        }

        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return ValidMovePositions(from, board).Select(to => new NormalMove(from, to));
        }
    }
}
