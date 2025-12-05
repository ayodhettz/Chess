namespace ChessLogic
{
    public class King : Piece
    {
        public override PieceType Type => PieceType.King;
        public override Player Colour { get; }
        private static readonly Direction[] directions = new Direction[]
        {
            Direction.North, Direction.NorthEast, Direction.East, Direction.SouthEast,
            Direction.South, Direction.SouthWest, Direction.West, Direction.NorthWest
        };
        public King(Player colour)
        {
            Colour = colour;
        }
        public override Piece Copy()
        {
            return new King(Colour) { HasMoved = this.HasMoved };
        }

        private IEnumerable<Position> ValidMovePositions(Position from, Board board)
        {
            foreach (Direction dir in directions)
            {
                Position to = from + dir;
                if (Board.IsInside(to) && (board.IsEmpty(to) || board[to].Colour != Colour))
                {
                    yield return to;
                }
            }
        }

        public override IEnumerable<Move> GetMoves(Position from, Board board)
        {
            foreach(Position to in ValidMovePositions(from, board))
            {
                yield return new NormalMove(from, to);
            }

            if(KingSideCastle(from, board))
            {
                yield return new Castle(MoveType.CastlingKS, from);
            }

            if(QueenSideCastle(from, board))
            {
                yield return new Castle(MoveType.CastlingQS, from);
            }
        }

        public override bool CanCaptureOpponentKing(Position from, Board board)
        {
            return ValidMovePositions(from, board).Any(to=> board[to] is King && board[to].Colour != Colour);
        }

        private static bool IsUnmovedRook(Position pos, Board board)
        {
            if (board.IsEmpty(pos))
            {
                return false;
            }
            else
            {
                Piece piece = board[pos];
                return piece.Type == PieceType.Rook && !piece.HasMoved;
            }
        }

        private static bool AllEmpty(IEnumerable<Position> positions, Board board)
        { 
            return positions.All(pos => board.IsEmpty(pos));
        }

        private bool KingSideCastle(Position from, Board board)
        {
            if (HasMoved)
            {
                return false;
            }

            Position rookPos = new Position(from.Row, 7);
            Position[] between = new Position[]
            {
                new (from.Row, 5),
                new (from.Row, 6)
            };

            return IsUnmovedRook(rookPos, board) && AllEmpty(between, board);
        }

        private bool QueenSideCastle(Position from, Board board)
        {
            if (HasMoved)
            {
                return false;
            }
            Position rookPos = new Position(from.Row, 0);
            Position[] between = new Position[]
            {
                new (from.Row, 1),
                new (from.Row, 2),
                new (from.Row, 3)
            };
            return IsUnmovedRook(rookPos, board) && AllEmpty(between, board);
        }

    }
}
