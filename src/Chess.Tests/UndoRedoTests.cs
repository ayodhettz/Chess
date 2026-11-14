using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class UndoRedoTests
{
    [Theory]
    [InlineData(Fen.InitialPosition)]
    [InlineData("7k/6Q1/6K1/8/8/8/8/8 b - - 0 1")]
    public void ANewGameHasNoActionsToNavigate(string fen)
    {
        GameState game = Fen.Parse(fen);
        State expected = Capture(game);
        Assert.False(game.CanUndo);
        Assert.False(game.CanRedo);
        Assert.False(game.Undo());
        Assert.False(game.Redo());
        AssertState(expected, game);
    }

    [Fact]
    public void OpeningCanBeRewoundAndReplayedWithoutReplacingPublicCollections()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Board board = game.Board;
        var history = game.History;
        List<State> states = new() { Capture(game) };
        foreach (var (from, to) in new[] { ("e2", "e4"), ("c7", "c5"), ("g1", "f3"), ("b8", "c6") })
        {
            Play(game, from, to);
            states.Add(Capture(game));
        }

        for (int index = states.Count - 2; index >= 0; index--)
        {
            Assert.True(game.Undo());
            AssertState(states[index], game);
        }
        Assert.False(game.CanUndo);
        for (int index = 1; index < states.Count; index++)
        {
            Assert.True(game.Redo());
            AssertState(states[index], game);
        }
        Assert.False(game.CanRedo);
        Assert.Same(board, game.Board);
        Assert.Same(history, game.History);
    }

    public static IEnumerable<object?[]> SpecialMoves()
    {
        yield return new object?[] { Fen.InitialPosition, "e2", "e4", null };
        yield return new object?[] { Fen.InitialPosition.Replace(" w ", " b "), "e7", "e5", null };
        foreach (string colour in new[] { "w", "b" })
        {
            string rank = colour == "w" ? "1" : "8";
            foreach (string file in new[] { "g", "c" })
                yield return new object?[] { $"r3k2r/8/8/8/8/8/8/R3K2R {colour} KQkq - 13 42", "e" + rank, file + rank, null };
        }
        yield return new object?[] { "4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 42", "e5", "d6", null };
        yield return new object?[] { "4k3/8/8/8/3Pp3/8/8/4K3 b - d3 0 42", "e4", "d3", null };
        yield return new object?[] { "4k3/8/8/8/8/8/r7/R3K3 w - - 13 42", "a1", "a2", null };
        foreach (PieceType promotion in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
        {
            yield return new object?[] { "4k3/P7/8/8/8/8/8/4K3 w - - 9 42", "a7", "a8", promotion };
            yield return new object?[] { "1r2k3/P7/8/8/8/8/8/4K3 w - - 9 42", "a7", "b8", promotion };
            yield return new object?[] { "4k3/8/8/8/8/8/p7/4K3 b - - 9 42", "a2", "a1", promotion };
        }
    }

    [Theory]
    [MemberData(nameof(SpecialMoves))]
    public void SpecialMovesRestoreEveryPieceFlagAndCounter(string fen, string from, string to, PieceType? promotion)
    {
        GameState game = Fen.Parse(fen);
        State before = Capture(game);
        Play(game, from, to, promotion);
        State after = Capture(game);

        for (int repeat = 0; repeat < 3; repeat++)
        {
            Assert.True(game.Undo());
            AssertState(before, game);
            Assert.True(game.Redo());
            AssertState(after, game);
        }
    }

    [Fact]
    public void UndoingADoubleStepRestoresItsFutureAvailability()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "e2", "e4");
        Assert.True(game.Undo());
        Assert.False(game.Board.GetPiece(At("e2")).HasMoved);
        Play(game, "e2", "e3");
        Assert.False(game.CanRedo);
        Assert.Null(game.Board.GetPawnSkipPosition(Player.White));
    }

    [Fact]
    public void IllegalActionsDoNotClearTheRedoBranch()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "e2", "e4"); Play(game, "e7", "e5");
        State future = Capture(game);
        game.Undo();
        State before = Capture(game);
        Assert.Throws<ArgumentException>(() => game.ApplyMove(new NormalMove(At("e7"), At("e4"))));
        Assert.Throws<ArgumentNullException>(() => game.ApplyMove(null!));
        Assert.Throws<InvalidOperationException>(() => game.ClaimDraw());
        AssertState(before, game);
        Assert.True(game.CanRedo);
        Assert.True(game.Redo());
        AssertState(future, game);
    }

    [Fact]
    public void ANewMoveReplacesOnlyTheAbandonedFuture()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "e2", "e4");
        State root = Capture(game);
        Play(game, "e7", "e5");
        game.Undo();
        Play(game, "c7", "c5");
        State branch = Capture(game);
        Assert.False(game.CanRedo);
        game.Undo();
        AssertState(root, game);
        game.Redo();
        AssertState(branch, game);
        Assert.Null(game.Board[At("e5")]);
        Assert.IsType<Pawn>(game.Board[At("c5")]);
    }

    [Fact]
    public void UndoingRepetitionRemovesTheAbandonedOccurrences()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        KnightCycle(game); KnightCycle(game);
        Assert.True(game.CanClaimDraw());
        while (game.CanUndo) game.Undo();
        Assert.Equal(1, game.CurrentPositionRepetitions);
        KnightCycle(game);
        Assert.Equal(2, game.CurrentPositionRepetitions);
        Assert.False(game.CanClaimDraw());
        Assert.False(game.CanRedo);
    }

    [Fact]
    public void RepetitionClaimCanBeUndoneAndRedoneWithoutAddingAnOccurrence()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        KnightCycle(game); KnightCycle(game);
        AssertResultAction(game, () => game.ClaimDraw(), EndReason.ThreefoldRepetition);
        Assert.Equal(3, game.CurrentPositionRepetitions);
    }

    [Fact]
    public void IntendedMoveClaimDoesNotBecomeAPlayedMoveOnRedo()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        KnightCycle(game);
        Play(game, "g1", "f3"); Play(game, "g8", "f6"); Play(game, "f3", "g1");
        Move intended = FindMove(game, "f6", "g8");
        AssertResultAction(game, () => game.ClaimDraw(intended), EndReason.ThreefoldRepetition);
        Assert.Equal(7, game.History.Count);
        Assert.IsType<Knight>(game.Board[At("f6")]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AResultActionAlsoReplacesAnAbandonedFuture(bool resign)
    {
        GameState game = Fen.Parse(Fen.InitialPosition.Replace("0 1", "100 42"));
        Play(game, "g1", "f3");
        game.Undo();
        if (resign) game.Resign(); else game.ClaimDraw();
        Assert.False(game.CanRedo);
        game.Undo();
        Assert.Empty(game.History);
        Assert.Equal(100, game.HalfMoveClock);
        Assert.True(game.CanClaimDraw());
        game.Redo();
        Assert.Empty(game.History);
        Assert.Equal(resign ? EndReason.Resignation : EndReason.FiftyMoveRule, game.Result!.Reason);
    }

    [Theory]
    [InlineData(Fen.InitialPosition)]
    [InlineData("4k3/8/8/8/8/8/8/3QK3 w - - 0 1")]
    public void ResignationRestoresThePreviousResultAndPosition(string fen)
    {
        GameState game = Fen.Parse(fen);
        AssertResultAction(game, game.Resign, EndReason.Resignation);
    }

    [Fact]
    public void CheckmateCanBeTakenBackAndReplayed()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "f2", "f3"); Play(game, "e7", "e5"); Play(game, "g2", "g4");
        AssertResultAction(game, () => Play(game, "d8", "h4"), EndReason.Checkmate);
    }

    [Fact]
    public void StalemateCanBeTakenBackAndReplayed()
    {
        GameState game = Fen.Parse("7k/8/6K1/5Q2/8/8/8/8 w - - 0 1");
        AssertResultAction(game, () => Play(game, "f5", "f7"), EndReason.Stalemate);
    }

    [Fact]
    public void FivefoldDrawRestoresRepetitionCountsOnEitherSideOfTheBoundary()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        KnightCycle(game); KnightCycle(game); KnightCycle(game);
        Play(game, "g1", "f3"); Play(game, "g8", "f6"); Play(game, "f3", "g1");
        AssertResultAction(game, () => Play(game, "f6", "g8"), EndReason.FivefoldRepetition);
        Assert.Equal(5, game.CurrentPositionRepetitions);
    }

    [Fact]
    public void SeventyFiveMoveDrawRestoresTheClockAndClaimEligibility()
    {
        GameState game = Fen.Parse("r3k3/8/8/8/8/8/8/R3K3 w - - 149 82");
        AssertResultAction(game, () => Play(game, "a1", "a2"), EndReason.SeventyFiveMoveRule);
    }

    [Fact]
    public void LoadingFenDoesNotRestoreNavigationHistory()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "e2", "e4"); Play(game, "e7", "e5"); game.Undo();
        GameState loaded = Fen.Parse(game.ToFen());
        Assert.False(loaded.CanUndo);
        Assert.False(loaded.CanRedo);
    }

    private static void AssertResultAction(GameState game, Action action, EndReason reason)
    {
        State before = Capture(game);
        action();
        State ended = Capture(game);
        Assert.Equal(reason, game.Result!.Reason);
        Assert.True(game.Undo());
        AssertState(before, game);
        Assert.True(game.Redo());
        AssertState(ended, game);
    }

    private static State Capture(GameState game) => new(game.ToFen(),
        string.Join(";", game.Board.AllPositions().Select(pos => $"{pos.Row},{pos.Column}:{game.Board.GetPiece(pos).HasMoved}")),
        game.Board.GetPawnSkipPosition(Player.White), game.Board.GetPawnSkipPosition(Player.Black),
        game.History.ToArray(), game.CurrentPositionRepetitions, game.Result, game.CanClaimDraw());

    private static void AssertState(State expected, GameState game)
    {
        State actual = Capture(game);
        Assert.Equal(expected.Fen, actual.Fen);
        Assert.Equal(expected.Flags, actual.Flags);
        Assert.Equal(expected.WhiteSkip, actual.WhiteSkip);
        Assert.Equal(expected.BlackSkip, actual.BlackSkip);
        Assert.Equal(expected.History, game.History);
        Assert.Equal(expected.Repetitions, actual.Repetitions);
        Assert.Same(expected.Result, actual.Result);
        Assert.Equal(expected.Claimable, actual.Claimable);
    }

    private sealed record State(string Fen, string Flags, Position? WhiteSkip, Position? BlackSkip,
        MoveRecord[] History, int Repetitions, Result? Result, bool Claimable);
}
