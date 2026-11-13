using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class DrawTests
{
    [Fact]
    public void ThreefoldRequiresAClaimAndCountsTheInitialPosition()
    {
        GameState game = new(Board.Initial(), Player.White);
        KnightCycle(game);
        Assert.False(game.CanClaimDraw());
        KnightCycle(game);
        Assert.Equal(3, game.CurrentPositionRepetitions);
        Assert.False(game.IsGameOver());
        Assert.True(game.CanClaimDraw());

        game.ClaimDraw();

        Assert.Equal(EndReason.ThreefoldRepetition, game.Result!.Reason);
        Assert.Equal(Player.None, game.Result.Winner);
        Assert.Throws<InvalidOperationException>(() => game.ClaimDraw());
    }

    [Fact]
    public void IntendedMoveCanClaimRepetitionWithoutChangingTheBoard()
    {
        GameState game = new(Board.Initial(), Player.White);
        KnightCycle(game);
        Play(game, "g1", "f3"); Play(game, "g8", "f6"); Play(game, "f3", "g1");
        Move intended = FindMove(game, "f6", "g8");
        Assert.False(game.CanClaimDraw());
        Assert.True(game.CanClaimDraw(intended));

        game.ClaimDraw(intended);

        Assert.Equal(EndReason.ThreefoldRepetition, game.Result!.Reason);
        Assert.IsType<Knight>(game.Board[At("f6")]);
        Assert.Equal(7, game.History.Count);
        Assert.Equal(Player.Black, game.CurrentPlayer);
    }

    [Fact]
    public void FivefoldEndsTheGameAutomatically()
    {
        GameState game = new(Board.Initial(), Player.White);
        for (int i = 0; i < 4; i++) KnightCycle(game);
        Assert.Equal(EndReason.FivefoldRepetition, game.Result!.Reason);
    }

    [Fact]
    public void InvalidClaimLeavesGameActive()
    {
        GameState game = new(Board.Initial(), Player.White);
        Assert.Throws<InvalidOperationException>(() => game.ClaimDraw());
        Assert.False(game.IsGameOver());
        Assert.Empty(game.History);
    }

    [Fact]
    public void FiftyMoveBoundarySupportsAnIntendedMoveAndCurrentPositionClaim()
    {
        GameState game = QuietGame(99);
        Assert.Equal(99, game.HalfMoveClock);
        Assert.False(game.CanClaimDraw());
        Move intended = game.AllLegalMoves(game.CurrentPlayer).First(move => game.Board.IsEmpty(move.To));
        Assert.True(game.CanClaimDraw(intended));
        Assert.Equal(99, game.History.Count);

        game.ApplyMove(intended);

        Assert.Equal(100, game.HalfMoveClock);
        Assert.False(game.IsGameOver());
        Assert.True(game.CanClaimDraw());
        game.ClaimDraw();
        Assert.Equal(EndReason.FiftyMoveRule, game.Result!.Reason);
    }

    [Fact]
    public void SeventyFiveMovesEndsTheGameAutomatically()
    {
        GameState game = QuietGame(150);
        Assert.Equal(150, game.HalfMoveClock);
        Assert.Equal(EndReason.SeventyFiveMoveRule, game.Result!.Reason);
    }

    [Fact]
    public void CaptureResetsTheHalfMoveClock()
    {
        GameState game = new(Parse("4k3/8/8/8/8/8/r7/R3K3"), Player.White);
        Play(game, "e1", "f1");
        Assert.Equal(1, game.HalfMoveClock);
        Play(game, "a2", "a1");
        Assert.Equal(0, game.HalfMoveClock);
        Assert.Equal(PieceType.Rook, game.History[^1].CapturedPieceType);
    }

    [Fact]
    public void RepetitionIncludesSideToMoveAndCastlingRights()
    {
        Board board = Board.Initial();
        string original = PositionKey.Create(board, Player.White);
        Assert.NotEqual(original, PositionKey.Create(board, Player.Black));
        board.GetPiece(At("h1")).HasMoved = true;
        Assert.NotEqual(original, PositionKey.Create(board, Player.White));
    }

    [Fact]
    public void UncapturableSkipSquareDoesNotChangeRepetition()
    {
        Board board = Board.Initial();
        new DoubleStep(At("e2"), At("e4")).Execute(board);
        string withSkip = PositionKey.Create(board, Player.Black);
        board.SetPawnSkipPosition(Player.White, null!);
        Assert.Equal(withSkip, PositionKey.Create(board, Player.Black));
    }

    [Theory]
    [InlineData("k7/8/8/3pP3/8/8/8/4K3", false)]
    [InlineData("k3r3/8/8/3pP3/8/8/8/4K3", true)]
    public void OnlyLegalEnPassantChangesRepetition(string placement, bool equalWithoutSkip)
    {
        Board board = Parse(placement);
        board.SetPawnSkipPosition(Player.Black, At("d6"));
        string withSkip = PositionKey.Create(board, Player.White);
        board.SetPawnSkipPosition(Player.Black, null!);
        Assert.Equal(equalWithoutSkip, withSkip == PositionKey.Create(board, Player.White));
    }
}
