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
        public Result Result { get; private set; } = null;
        public GameState(Board board, Player currentPlayer)
        {
            Board = board;
            CurrentPlayer = currentPlayer;
        }
        public IEnumerable<Move> LegalMoves(Position pos)
        {
            if (Board.IsEmpty(pos) || Board[pos].Colour != CurrentPlayer)
            {
                return Enumerable.Empty<Move>();
            }

            Piece piece = Board[pos];
            IEnumerable<Move> possibleMoves = piece.GetMoves(pos, Board);
            return possibleMoves.Where(move => move.IsLegal(Board));
        }

        public void ApplyMove(Move move)
        {
            Board.SetPawnSkipPosition(CurrentPlayer, null);
            move.Execute(Board);
            CurrentPlayer = CurrentPlayer.Opponent();
            CheckForGameEnd();
        }

        public IEnumerable<Move> AllLegalMoves(Player player)
        {
            IEnumerable<Move> allMoves = Board.PositionsOfPlayer(player)
                .SelectMany(pos => LegalMoves(pos));

            return allMoves.Where(move => move.IsLegal(Board));
        }

        private void CheckForGameEnd()
        {
            if (!AllLegalMoves(CurrentPlayer).Any())
            {
                if (Board.IsInCheck(CurrentPlayer))
                {
                    Result = Result.WinByMate(CurrentPlayer.Opponent());
                }
                else
                {
                    Result = Result.DrawByStalemate(EndReason.Stalemate);
                }
            }
        }

        public bool IsGameOver()
        {
            return Result != null;
        }
    }
}
