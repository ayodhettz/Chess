namespace ChessLogic
{
    public class DoubleStep : Move
    {
        public override MoveType Type => MoveType.DoubleStep;
        public override Position From { get; }
        public override Position To { get; }
        
        private readonly Position skippedPos;

        public DoubleStep(Position from, Position to)
        {
            From = from;
            To = to;
            skippedPos = new Position((from.Row + to.Row) / 2, from.Column);
        }

        public override void Execute(Board board)
        {
            Player player = board[From].Colour;
            board.SetPawnSkipPosition(player, skippedPos);
            new NormalMove(From, To).Execute(board);
        }
    }
}
