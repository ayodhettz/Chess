using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class GameStateTests
{
    [Fact]
    public void BothPlayersHaveTwentyInitialMoves()
    {
        GameState game = new(Board.Initial(), Player.White);
        Assert.Equal(20, game.AllLegalMoves(Player.White).Count());
        Assert.Equal(20, game.AllLegalMoves(Player.Black).Count());
        Assert.Empty(game.LegalMoves(At("e7")));
    }

    [Fact]
    public void BlackEnPassantExpiresAfterTheImmediateReply()
    {
        GameState game = new(Board.Initial(), Player.White);
        Play(game, "e2", "e4"); Play(game, "a7", "a6");
        Play(game, "e4", "e5"); Play(game, "d7", "d5");
        Assert.Contains(game.LegalMoves(At("e5")), move => move.Type == MoveType.EnPassant);
        Play(game, "g1", "f3"); Play(game, "a6", "a5");
        Assert.DoesNotContain(game.LegalMoves(At("e5")), move => move.Type == MoveType.EnPassant);
    }

    [Fact]
    public void WhiteEnPassantExpiresAfterTheImmediateReply()
    {
        GameState game = new(Board.Initial(), Player.White);
        Play(game, "a2", "a3"); Play(game, "e7", "e5");
        Play(game, "a3", "a4"); Play(game, "e5", "e4");
        Play(game, "d2", "d4");
        Assert.Contains(game.LegalMoves(At("e4")), move => move.Type == MoveType.EnPassant);
        Play(game, "g8", "f6"); Play(game, "a4", "a5");
        Assert.DoesNotContain(game.LegalMoves(At("e4")), move => move.Type == MoveType.EnPassant);
    }

    [Theory]
    [InlineData("e2", "e5")]
    [InlineData("e7", "e6")]
    [InlineData("a3", "a4")]
    public void IllegalMovesLeaveTheGameUnchanged(string from, string to)
    {
        GameState game = new(Board.Initial(), Player.White);
        Assert.Throws<ArgumentException>(() => game.ApplyMove(new NormalMove(At(from), At(to))));
        Assert.Equal(Player.White, game.CurrentPlayer);
        Assert.Empty(game.History);
        Assert.Equal(32, game.Board.AllPositions().Count());
        Assert.Equal(0, game.HalfMoveClock);
    }

    [Fact]
    public void InvalidSquaresAndNullMoveAreHandled()
    {
        GameState game = new(Board.Initial(), Player.White);
        Assert.Empty(game.LegalMoves(new Position(-1, 8)));
        Assert.Empty(game.LegalMoves(null!));
        Assert.Throws<ArgumentNullException>(() => game.ApplyMove(null!));
    }

    [Fact]
    public void CallerSuppliedExecutionIsNeverInvoked()
    {
        GameState game = new(Board.Initial(), Player.White);
        game.ApplyMove(new UntrustedMove());
        Assert.IsType<Pawn>(game.Board[At("e4")]);
        Assert.Equal(Player.Black, game.CurrentPlayer);
    }

    [Fact]
    public void FoolsmateEndsTheGameAndRejectsFurtherMoves()
    {
        GameState game = new(Board.Initial(), Player.White);
        Play(game, "f2", "f3"); Play(game, "e7", "e5");
        Play(game, "g2", "g4"); Play(game, "d8", "h4");
        Assert.Equal(EndReason.Checkmate, game.Result!.Reason);
        Assert.Equal(Player.Black, game.Result.Winner);
        Assert.Empty(game.AllLegalMoves(Player.White));
        Assert.Throws<InvalidOperationException>(() => game.ApplyMove(new NormalMove(At("a2"), At("a3"))));
        Assert.Throws<InvalidOperationException>(() => game.Resign());
    }

    [Fact]
    public void InitialStalemateIsRecognized()
    {
        GameState game = new(Parse("7k/5Q2/6K1/8/8/8/8/8"), Player.Black);
        Assert.Equal(EndReason.Stalemate, game.Result!.Reason);
        Assert.Equal(Player.None, game.Result.Winner);
    }

    [Fact]
    public void ResignationAwardsTheOpponentAndStopsPlay()
    {
        GameState game = new(Board.Initial(), Player.White);
        game.Resign();
        Assert.Equal(EndReason.Resignation, game.Result!.Reason);
        Assert.Equal(Player.Black, game.Result.Winner);
        Assert.Empty(game.LegalMoves(At("e2")));
        Assert.False(game.CanClaimDraw());
    }

    [Fact]
    public void ResignationCannotAwardAWinToABareKing()
    {
        GameState game = new(Parse("4k3/8/8/8/8/8/8/3QK3"), Player.White);
        game.Resign();
        Assert.Equal(EndReason.Resignation, game.Result!.Reason);
        Assert.Equal(Player.None, game.Result.Winner);
    }

    [Fact]
    public void HistoryRecordsMovesAndPawnMovesResetTheClock()
    {
        GameState game = new(Board.Initial(), Player.White);
        Play(game, "g1", "f3"); Play(game, "g8", "f6");
        Assert.Equal(2, game.HalfMoveClock);
        Play(game, "e2", "e4");
        Assert.Equal(0, game.HalfMoveClock);
        Assert.Equal(3, game.History.Count);
        Assert.Equal("g1-f3", game.History[0].ToString());
        Assert.Equal(PieceType.Pawn, game.History[2].PieceType);
        Assert.Equal(MoveType.DoubleStep, game.History[2].MoveType);
        Assert.Throws<NotSupportedException>(() => ((IList<MoveRecord>)game.History).Clear());
    }

    [Fact]
    public void EnPassantRecordsCaptureAndRemovesThePawn()
    {
        GameState game = new(Board.Initial(), Player.White);
        Play(game, "e2", "e4"); Play(game, "a7", "a6");
        Play(game, "e4", "e5"); Play(game, "d7", "d5");
        Play(game, "e5", "d6");
        Assert.Null(game.Board[At("d5")]);
        Assert.Equal(PieceType.Pawn, game.History[^1].CapturedPieceType);
        Assert.Equal("e5xd6 e.p.", game.History[^1].ToString());
        Assert.Equal(0, game.HalfMoveClock);
    }

    private sealed class UntrustedMove : Move
    {
        public override MoveType Type => MoveType.DoubleStep;
        public override Position From => At("e2");
        public override Position To => At("e4");
        public override void Execute(Board board) => throw new InvalidOperationException("Caller code executed.");
    }
}
