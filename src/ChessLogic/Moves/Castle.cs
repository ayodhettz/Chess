using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Castle : Move
    {
        public override MoveType Type { get; }
        public override Position From { get; }
        public override Position To { get; }

        private readonly Direction kingMoveDir;
        private readonly Position rookFrom; 
        private readonly Position rookTo;

        public Castle(MoveType type, Position kingPos)
        {
            Type = type;
            From = kingPos;

            if (type == MoveType.CastlingKS)
            {
                kingMoveDir = Direction.East;
                To = new Position(kingPos.Row, 6);
                rookFrom = new Position(kingPos.Row, 7);
                rookTo = new Position(kingPos.Row, 5);
            }
            else if(type == MoveType.CastlingQS)
            {
                kingMoveDir = Direction.West;
                To = new Position(kingPos.Row, 2);
                rookFrom = new Position(kingPos.Row, 0);
                rookTo = new Position(kingPos.Row, 3);
            } else
            {
                throw new ArgumentException("Invalid castling type");
            }
        }

        public override void Execute(Board board)
        {
            new NormalMove(From, To).Execute(board);
            new NormalMove(rookFrom, rookTo).Execute(board);
        }


        public override bool IsLegal(Board board)
        {
            Player player = board[From].Colour;

            if(board.IsInCheck(player))
            {
                return false;
            }

            Board copy = board.Copy();
            Position kingPos = From;

            //create board copy and check if the king lands on a check
            for (int i = 0; i < 2; i++)
            {
                new NormalMove(kingPos, kingPos  + kingMoveDir).Execute(copy);
                kingPos += kingMoveDir;
                if (copy.IsInCheck(player))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
