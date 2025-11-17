using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class GameState
    {
        public Board Board { get; }
        public Player CurrentPlayer { get; private set; }
        public GameState(Board board, Player currentPlayer)
        {
            Board = board;
            CurrentPlayer = currentPlayer;
        }
    }
}
