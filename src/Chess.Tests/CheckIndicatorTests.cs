using ChessLogic;

namespace Chess.Tests;

public class CheckIndicatorTests
{
    [Theory]
    [InlineData("4k3/8/8/8/8/8/4r3/4K3 w - - 0 1", 7, 4)]
    [InlineData("4k3/4R3/8/8/8/8/8/4K3 b - - 0 1", 0, 4)]
    public void CheckedKingUsesLogicalCoordinates(string fen, int row, int column)
    {
        GameState game = Fen.Parse(fen);
        Assert.Equal(new Position(row, column), game.CheckedKingSquare);
        Move escape = game.AllLegalMoves(game.CurrentPlayer).First();
        game.ApplyMove(escape);
        Assert.Null(game.CheckedKingSquare);
        game.Undo();
        Assert.Equal(new Position(row, column), game.CheckedKingSquare);
    }

    [Fact]
    public void MateKeepsTheCheckedKingButStalemateDoesNot()
    {
        Assert.Equal(new Position(0, 7), Fen.Parse("7k/6Q1/5K2/8/8/8/8/8 b - - 0 1").CheckedKingSquare);
        Assert.Null(Fen.Parse("7k/5K2/6Q1/8/8/8/8/8 b - - 0 1").CheckedKingSquare);
        Assert.Null(Fen.Parse(Fen.InitialPosition).CheckedKingSquare);
    }
}
