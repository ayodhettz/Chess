namespace ChessLogic
{
    internal sealed record GameAction(string Kind, string? From = null, string? To = null, PieceType? Promotion = null)
    {
        internal static GameAction ForMove(string kind, Move move) =>
            new(kind, Square(move.From), Square(move.To), (move as PawnPromotion)?.newType);

        internal Move Resolve(GameState game)
        {
            Position from = ParseSquare(From);
            Position to = ParseSquare(To);
            return game.LegalMoves(from).SingleOrDefault(move => move.To == to &&
                (move is PawnPromotion promotion ? promotion.newType == Promotion : Promotion == null))
                ?? throw new FormatException("The saved move is not legal in this position.");
        }

        internal void Replay(GameState game)
        {
            switch (Kind)
            {
                case "move": game.ApplyMove(Resolve(game)); break;
                case "resign" when From == null && To == null && Promotion == null: game.Resign(); break;
                case "claim" when From == null && To == null && Promotion == null: game.ClaimDraw(); break;
                case "claim": game.ClaimDraw(Resolve(game)); break;
                default: throw new FormatException("The saved action type is not supported.");
            }
        }

        private static string Square(Position pos) => $"{(char)('a' + pos.Column)}{(char)('8' - pos.Row)}";

        private static Position ParseSquare(string? square)
        {
            if (square == null || square.Length != 2 || square[0] is < 'a' or > 'h' || square[1] is < '1' or > '8')
                throw new FormatException("Saved moves must use squares from a1 to h8.");
            return new Position('8' - square[1], square[0] - 'a');
        }
    }
}
