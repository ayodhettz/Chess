using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    //all pieces inherit from this class
    public abstract class Piece
    {
        public abstract PieceType Type { get; }
        public abstract Player Colour { get; }
        public bool HasMoved { get; set; } = false;
        public abstract Piece Copy();

        public abstract IEnumerable<Move> GetMoves(Position from, Board board);
        protected IEnumerable<Position> MovePositionsInDir(Position from, Board board, Direction dir)
        {
            for(Position pos = from + dir; Board.IsInside(pos); pos += dir)
            {
                if(board[pos] == null)
                {
                    yield return pos;
                    continue;
                }
                Piece piece = board[pos];
                if(piece.Colour != Colour)
                {
                    yield return pos;
                }

                yield break;
            }
        }

        protected IEnumerable<Position> MovePositionsinDirs(Position from, Board board, IEnumerable<Direction> dirs)
        {
            return dirs.SelectMany(dir => MovePositionsInDir(from, board, dir));
        }

        public virtual bool CanCaptureOpponentKing(Position from, Board board)
        {
            return GetMoves(from, board).Any(move => board[move.To] is King && board[move.To].Colour != Colour);
        }
    }
}
