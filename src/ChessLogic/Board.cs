using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class Board
    {
        private readonly Piece[,] pieces = new Piece[8, 8];

        private readonly Dictionary<Player, Position> pawnSkipPosition = new Dictionary<Player, Position>
        {
            { Player.White, null},
            { Player.Black, null}
        };

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
            this[0, 0] = new Rook(Player.Black);
            this[0, 1] = new Knight(Player.Black);
            this[0, 2] = new Bishop(Player.Black);
            this[0, 3] = new Queen(Player.Black);
            this[0, 4] = new King(Player.Black);
            this[0, 5] = new Bishop(Player.Black);
            this[0, 6] = new Knight(Player.Black);
            this[0, 7] = new Rook(Player.Black);
            for (int col = 0; col < 8; col++)
            {
                this[1, col] = new Pawn(Player.Black);
                this[6, col] = new Pawn(Player.White);
            }
            this[7, 0] = new Rook(Player.White);
            this[7, 1] = new Knight(Player.White);
            this[7, 2] = new Bishop(Player.White);
            this[7, 3] = new Queen(Player.White);
            this[7, 4] = new King(Player.White);
            this[7, 5] = new Bishop(Player.White);
            this[7, 6] = new Knight(Player.White);
            this[7, 7] = new Rook(Player.White);
        }

        public static bool IsInside(Position pos)
        {
            return pos.Row >= 0 && pos.Row < 8 && pos.Column >= 0 && pos.Column < 8;
        }

        public bool IsEmpty(Position pos)
        {
            return this[pos] == null;
        }

        public IEnumerable<Position> AllPositions()
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Position pos = new Position(row, col);

                    if (!IsEmpty(pos))
                    {
                        yield return pos;
                    }
                }
            }
        }

        public IEnumerable<Position> PositionsOfPlayer(Player player)
        {
            return AllPositions().Where(pos => this[pos].Colour == player);
        }

        public bool IsInCheck(Player player)
        {
            return AllPositions()
                .Where(pos => this[pos].Colour != player)
                .Any(pos => this[pos].CanCaptureOpponentKing(pos, this));
        }

        public Board Copy()
        {
            Board newBoard = new Board();
            foreach (Position pos in AllPositions())
            {
                newBoard[pos] = this[pos].Copy();
            }
            return newBoard;
        }

        public Position GetPawnSkipPosition(Player player)
        {
            return pawnSkipPosition[player];
        }

        public void SetPawnSkipPosition(Player player, Position pos)
        {
            pawnSkipPosition[player] = pos;
        }

        public Counting CountPieces()
        {
            Counting counting = new Counting();

            foreach (Position pos in AllPositions())
            {
                Piece piece = this[pos];
                counting.Increment(piece.Colour, piece.Type);
            }

            return counting;
        }

        public bool InsufficientMaterial()
        {
            Counting counting = CountPieces();

            return IsKingVsKing(counting) ||
                   IsKingBishopVsKing(counting) ||
                   IsKingKnightVsKing(counting) ||
                   IsKingBishopVsKingBishop(counting);
        }

        private static bool IsKingVsKing(Counting counting)
        {
            return counting.TotalCount == 2;
        }

        private static bool IsKingBishopVsKing(Counting counting)
        {
            return counting.TotalCount == 3 && (counting.White(PieceType.Bishop) == 1 || counting.Black(PieceType.Bishop) == 1);
        }

        private static bool IsKingKnightVsKing(Counting counting)
        {
            return counting.TotalCount == 3 && (counting.White(PieceType.Knight) == 1 || counting.Black(PieceType.Knight) == 1);
        }

        private bool IsKingBishopVsKingBishop(Counting counting)
        {
            if(counting.TotalCount == 4)
            {
                return false;
            }
            if(counting.White(PieceType.Bishop) != 1 && counting.Black(PieceType.Bishop) != 1)
            {
                return false;
            }

            Position wBishopPos = FindPiece( Player.White, PieceType.Bishop);    
            Position bBishopPos = FindPiece( Player.Black, PieceType.Bishop);

            return wBishopPos.SquareColor() == bBishopPos.SquareColor();
        }

        private Position FindPiece(Player colour, PieceType type)
        {
            return PositionsOfPlayer(colour).First(Position=> this[Position].Type == type);
        }
    }
}
