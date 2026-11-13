using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class BoardTests
{
    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/4K3", true)]
    [InlineData("4k3/8/8/8/8/8/8/2B1K3", true)]
    [InlineData("4k3/8/8/8/8/8/8/1N2K3", true)]
    [InlineData("1n2k3/8/8/8/8/8/8/2B1K3", false)]
    [InlineData("r3k3/8/8/8/8/8/8/2B1K3", false)]
    [InlineData("2b1k3/8/8/8/8/8/8/2B1K3", false)]
    [InlineData("4kb2/8/8/8/8/8/8/2B1K3", true)]
    public void MaterialDetectionHandlesMinorPieces(string placement, bool insufficient)
    {
        Assert.Equal(insufficient, Parse(placement).InsufficientMaterial());
    }

    [Fact]
    public void CopyPreservesSkipSquaresAndOwnsItsPieces()
    {
        Board board = Board.Initial();
        board.SetPawnSkipPosition(Player.White, At("e3"));
        board.SetPawnSkipPosition(Player.Black, At("d6"));
        board[At("g1")].HasMoved = true;

        Board copy = board.Copy();

        Assert.Equal(At("e3"), copy.GetPawnSkipPosition(Player.White));
        Assert.Equal(At("d6"), copy.GetPawnSkipPosition(Player.Black));
        Assert.NotSame(board[At("g1")], copy[At("g1")]);
        Assert.True(copy[At("g1")].HasMoved);
        copy.SetPawnSkipPosition(Player.Black, null!);
        copy[At("g1")].HasMoved = false;
        Assert.Equal(At("d6"), board.GetPawnSkipPosition(Player.Black));
        Assert.True(board[At("g1")].HasMoved);
    }
}
