namespace ChessLogic
{
    public abstract class Move
    {
        public abstract MoveType Type { get; }
        public abstract Position From { get; }
        public abstract Position To { get; }
        public abstract void Execute(Board board);
        public virtual bool IsLegal(Board board)
        {
            Player player = board[From].Colour;
            Board copy = board.Copy();
            Execute(copy);
            return !copy.IsInCheck(player);
            //not so efficient especially for computer generated moves
        }
    }
}
