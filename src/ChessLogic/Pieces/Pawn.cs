using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Pawn : Piece
    {
        public override PieceType Type => PieceType.Pawn;
        public override Player Colour { get; }
        private readonly Direction forward;
        public Pawn(Player colour)
        {
            Colour = colour;

            if (colour == Player.White)
            {
                forward = Direction.North;
            }
            else
            {
                forward = Direction.South;
            }
        }
        public override Piece Copy()
        {
            return new Pawn(Colour) { HasMoved = this.HasMoved };
        }

        private static bool CanMoveTo(Position pos, Board board)
        {
            return Board.IsInside(pos) && board.IsEmpty(pos);
        }

        private bool CanCaptureAt(Position pos, Board board)
        {
            return Board.IsInside(pos) && !board.IsEmpty(pos) && board[pos].Colour != Colour;
        }

        private IEnumerable<Move> GetForwardMoves(Position from, Board board)
        {
            Position oneStep = from + forward;
            if(CanMoveTo(oneStep, board))
            {
                yield return new NormalMove(from, oneStep);

                Position twoSteps = oneStep + forward;
                if (!HasMoved && CanMoveTo(twoSteps, board))
                {
                    yield return new NormalMove(from, twoSteps);
                }
            }
        }

        private IEnumerable<Move> GetCaptureMoves(Position from, Board board)
        {
            foreach (Direction dir in new Direction[] { Direction.West, Direction.East })
            {
                Position to = from + forward + dir;

                if (CanCaptureAt(to, board))
                {
                    yield return new NormalMove(from, to);
                }
            }
        }

        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            return GetForwardMoves(from, board).Concat(GetCaptureMoves(from, board));
        }
    }
}
