
namespace ChessLogic
{
    //all pieces move in a certain direction
    public class Direction
    {
        public readonly static Direction North = new Direction(-1, 0); //go up, minus one row
        public readonly static Direction South = new Direction(1, 0); //go down, plus one row
        public readonly static Direction East = new Direction(0, 1); //go right, plus one column
        public readonly static Direction West = new Direction(0, -1); //go left, minus one column
        public readonly static Direction NorthEast = North + East;
        public readonly static Direction NorthWest = North + West;
        public readonly static Direction SouthEast = South + East;
        public readonly static Direction SouthWest = South + West;
        public int RowDelta { get; }
        public int ColumnDelta { get; }

        public Direction(int rowDelta, int columnDelta)
        {
            RowDelta = rowDelta;
            ColumnDelta = columnDelta;
        }

        public static Direction operator +(Direction d1, Direction d2)
        {
            return new Direction(d1.RowDelta + d2.RowDelta, d1.ColumnDelta + d2.ColumnDelta);
        }

        public static Direction operator *(Direction d, int scalar)
        {
            return new Direction(d.RowDelta * scalar, d.ColumnDelta * scalar);
        }
    }
}
