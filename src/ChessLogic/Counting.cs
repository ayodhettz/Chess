using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Counting
    {
        private readonly Dictionary<PieceType, int> whiteCount= new();
        private readonly Dictionary<PieceType, int> blackCount= new();

        public int TotalCount { get;private set; }
        public Counting()
        {
            foreach ( PieceType type in Enum.GetValues(typeof(PieceType)))
            {
                whiteCount[type] = 0;
                blackCount[type] = 0;
            }
        }

        public void Increment(Player colour, PieceType type)
        {
            if (colour == Player.White)
            {
                whiteCount[type]++;
            }
            else if(colour == Player.Black)
            {
                blackCount[type]++;
            }

            TotalCount++;
        }

        public int White(PieceType type)
        {
            return whiteCount[type];
        }

        public int Black(PieceType type)
        {
            return blackCount[type];
        }
    }
}
