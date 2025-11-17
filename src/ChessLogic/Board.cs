using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Board
    {
        private readonly Piece[,] pieces = new Piece[8, 8];

        //get or set a piece onto a position on the board by providing the row and column || by providing a Position object
        public Piece this[int row, int col]
        {
            get { return pieces[row, col]; }
            set { pieces[row, col] = value; }
        }

        public Piece this[Position pos]
        {
            get { return pieces[pos.Row, pos.Column]; }
            set { pieces[pos.Row, pos.Column] = value; }
        }

        public static Board Initial()
        {
            Board board = new Board();
            board.AddStartingPieces();
            return board;
        }

        private void AddStartingPieces()
        {
            this[0,0] = new Rook(Player.Black);
            this[0,1] = new Knight(Player.Black);
            this[0,2] = new Bishop(Player.Black);
            this[0,3] = new Queen(Player.Black);
            this[0,4] = new King(Player.Black);
            this[0,5] = new Bishop(Player.Black);
            this[0,6] = new Knight(Player.Black);
            this[0,7] = new Rook(Player.Black);
            for(int col = 0; col < 8; col++)
            {
                this[1, col] = new Pawn(Player.Black);
                this[6, col] = new Pawn(Player.White);
            }
            this[7,0] = new Rook(Player.White);
            this[7,1] = new Knight(Player.White);
            this[7,2] = new Bishop(Player.White);
            this[7,3] = new Queen(Player.White);
            this[7,4] = new King(Player.White);
            this[7,5] = new Bishop(Player.White);
            this[7,6] = new Knight(Player.White);
            this[7,7] = new Rook(Player.White);
        }
    }
}
