using ChessLogic;

namespace Chess.Tests;

public class BotDifficultyTests
{
    [Theory]
    [InlineData(BotDifficulty.Easy)]
    [InlineData(BotDifficulty.Medium)]
    [InlineData(BotDifficulty.Hard)]
    public async Task LevelsRespectBudgetsAndPreserveSource(BotDifficulty difficulty)
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        string before = GameSave.Serialize(game);
        BotDecision decision = await new ChessBot().ChooseMoveAsync(game, difficulty, TestContext.Current.CancellationToken);
        BotSearchLimits limits = BotSearchLimits.For(difficulty);
        Assert.InRange(decision.Nodes, 1, limits.Nodes);
        Assert.InRange(decision.Depth, 1, limits.Depth);
        Assert.Equal(before, GameSave.Serialize(game));
        Assert.NotNull(decision.Move);
        game.ApplyMove(decision.Move);
        Assert.Single(game.History);
    }
}
