using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Result
    {
        public Player Winner { get; }
        public EndReason Reason { get; }
        public Result(Player winner, EndReason reason)
        {
            Winner = winner;
            Reason = reason;
        }

        public static Result WinByMate(Player winner)
        {
            return new Result(winner, EndReason.Checkmate);
        }

        public static Result WinByResignation(Player winner)
        {
            return new Result(winner, EndReason.Resignation);
        }

        public static Result DrawByStalemate(EndReason reason)
        {
            return new Result(Player.None, reason);
        }
    }
}
