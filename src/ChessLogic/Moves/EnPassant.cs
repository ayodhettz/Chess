using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class EnPassant : Move
    {
        public override MoveType Type => MoveType.EnPassant;
        public override Position From { get; }
        public override Position To { get; }
        private readonly Position capturedPawnPos;

        public EnPassant(Position from, Position to)
        {
            From = from;
            To = to;
            capturedPawnPos = new Position(from.Row, to.Column);
        }

        public override void Execute(Board board)
        {
            new NormalMove(From, To).Execute(board);
            board[capturedPawnPos] = null;
        }
    }
}
