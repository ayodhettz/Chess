namespace ChessLogic;

public enum BotDifficulty { Easy, Medium, Hard }

public sealed record BotSearchLimits(int Depth, int Nodes)
{
    public static BotSearchLimits For(BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Easy => new(1, 500),
        BotDifficulty.Medium => new(3, 4_000),
        BotDifficulty.Hard => new(5, 30_000),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };
}
