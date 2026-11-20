using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class ChessBotTests
{
    [Fact]
    public void SelfPlayProducesLegalMovesWithoutChangingTheSourceDuringSearch()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        for (int ply = 0; ply < 12 && !game.IsGameOver(); ply++)
        {
            string before = GameSave.Serialize(game);
            BotDecision decision = new ChessBot().ChooseMove(game, maxDepth: 2, nodeBudget: 500,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(before, GameSave.Serialize(game));
            Assert.InRange(decision.Nodes, 1, 500);
            if (decision.ClaimDraw) game.ClaimDraw(decision.Move);
            else game.ApplyMove(decision.Move!);
        }
        Assert.NotEmpty(game.History);
    }

    [Fact]
    public void RepetitionHistoryIsAvailableToTheBot()
    {
        GameState game = Fen.Parse("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNB1KBNR w KQkq - 0 1");
        KnightCycle(game); KnightCycle(game);
        BotDecision decision = new ChessBot().ChooseMove(game, maxDepth: 2,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(decision.ClaimDraw);
        Assert.Equal(0, decision.Score);
        Assert.Equal(3, game.CurrentPositionRepetitions);
    }

    [Theory]
    [InlineData("7k/5Q2/6K1/8/8/8/8/8 w - - 0 1", Player.White)]
    [InlineData("8/8/8/8/8/6k1/5q2/7K b - - 0 1", Player.Black)]
    public void FindsMateForEitherSide(string fen, Player winner)
    {
        GameState game = Fen.Parse(fen);
        BotDecision decision = new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken, maxDepth: 2);
        game.ApplyMove(Assert.IsAssignableFrom<Move>(decision.Move));
        Assert.Equal(EndReason.Checkmate, game.Result!.Reason);
        Assert.Equal(winner, game.Result.Winner);
        Assert.True(decision.Score > 99_000);
    }

    [Fact]
    public void TakesAnUndefendedQueen()
    {
        GameState game = Fen.Parse("7k/8/8/3q4/8/8/8/K2R4 w - - 0 1");
        BotDecision decision = new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken, maxDepth: 2);
        Assert.Equal(At("d1"), decision.Move!.From);
        Assert.Equal(At("d5"), decision.Move.To);
    }

    [Fact]
    public void LooksAtTheOpponentsReplyBeforeTakingAPoisonedPawn()
    {
        GameState game = Fen.Parse("3q3k/8/8/3p4/8/8/8/K2R4 w - - 0 1");
        BotDecision decision = new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken, maxDepth: 2, nodeBudget: 10_000);
        Assert.Equal(2, decision.Depth);
        Assert.NotEqual(At("d5"), decision.Move!.To);
    }

    [Fact]
    public void PromotesRatherThanLeavingAPawnOnTheSeventhRank()
    {
        GameState game = Fen.Parse("7k/P7/8/8/8/8/8/7K w - - 0 1");
        BotDecision decision = new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken, maxDepth: 2);
        Assert.Equal(PieceType.Queen, Assert.IsType<PawnPromotion>(decision.Move).newType);
        game.ApplyMove(decision.Move!);
        Assert.IsType<Queen>(game.Board[At("a8")]);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(99)]
    public void ClaimsADrawInsteadOfContinuingALosingPosition(int clock)
    {
        GameState game = Fen.Parse($"3q3k/8/8/3p4/8/8/8/K2R4 w - - {clock} 42");
        BotDecision decision = new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken, maxDepth: 2, nodeBudget: 10_000);
        Assert.True(decision.ClaimDraw);
        Assert.True(game.CanClaimDraw(decision.Move));
        game.ClaimDraw(decision.Move);
        Assert.Equal(EndReason.FiftyMoveRule, game.Result!.Reason);
    }

    [Fact]
    public void PrefersMateToAnAvailableDrawClaim()
    {
        GameState game = Fen.Parse("7k/5Q2/6K1/8/8/8/8/8 w - - 100 42");
        BotDecision decision = new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(decision.ClaimDraw);
        game.ApplyMove(decision.Move!);
        Assert.Equal(EndReason.Checkmate, game.Result!.Reason);
    }

    [Fact]
    public void AcceptsALosingDrawOfferButDeclinesWhenItCanMate()
    {
        GameState losing = Fen.Parse("3q3k/8/8/3p4/8/8/8/K2R4 b - - 0 42");
        losing.OfferDraw();
        Play(losing, "h8", "h7");
        BotDecision decision = new ChessBot().ChooseMove(losing, cancellationToken: TestContext.Current.CancellationToken, maxDepth: 2);
        Assert.True(decision.AcceptDraw);
        losing.AcceptDraw(losing.CurrentPlayer);
        Assert.Equal(EndReason.DrawAgreement, losing.Result!.Reason);

        GameState winning = Fen.Parse("7k/p4Q2/6K1/8/8/8/8/8 b - - 0 42");
        winning.OfferDraw();
        Play(winning, "a7", "a6");
        Assert.False(new ChessBot().ChooseMove(winning, cancellationToken: TestContext.Current.CancellationToken, maxDepth: 2).AcceptDraw);
    }

    [Fact]
    public void SearchPreservesTheCompleteLiveGameIncludingRedo()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        KnightCycle(game); KnightCycle(game);
        game.OfferDraw();
        Play(game, "e2", "e4");
        game.Undo();
        string before = GameSave.Serialize(game);
        BotDecision decision = new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken, maxDepth: 2, nodeBudget: 300);
        Assert.Equal(before, GameSave.Serialize(game));
        Assert.True(game.CanRedo);
        Assert.True(decision.Nodes <= 300);
        Assert.True(decision.ClaimDraw || decision.Move != null);
    }

    [Fact]
    public void TinyBudgetStillReturnsALegalMove()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        BotDecision decision = new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken, nodeBudget: 1);
        Assert.Equal(0, decision.Depth);
        Assert.Equal(1, decision.Nodes);
        game.ApplyMove(decision.Move!);
    }

    [Fact]
    public void CompletedGamesHaveNoDecisionAndCancellationDoesNotMutate()
    {
        GameState game = Fen.Parse("7k/6Q1/6K1/8/8/8/8/8 b - - 0 1");
        Assert.Null(new ChessBot().ChooseMove(game, cancellationToken: TestContext.Current.CancellationToken).Move);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new ChessBot().ChooseMove(game, cancellationToken: cancellation.Token));
        Assert.Equal(EndReason.Checkmate, game.Result!.Reason);
    }

    [Fact]
    public async Task AsyncSearchSnapshotsBeforeTheCallerChangesItsGame()
    {
        GameState game = Fen.Parse("7k/5Q2/6K1/8/8/8/8/8 w - - 0 1");
        Task<BotDecision> task = new ChessBot().ChooseMoveAsync(game, TestContext.Current.CancellationToken);
        game.Resign();
        BotDecision decision = await task;
        Assert.NotNull(decision.Move);
        Assert.True(decision.Score > 99_000);
        Assert.Equal(EndReason.Resignation, game.Result!.Reason);
    }
}

