namespace Chess.Server;

public sealed class RoomLimits
{
    public int MaxRooms { get; init; } = 128;
    public int MaxConnections { get; init; } = 512;
    public int MaxPlies { get; init; } = 512;
    public int MaxCommands { get; init; } = 1024;
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(4);
    public TimeSpan WaitingLifetime { get; init; } = TimeSpan.FromMinutes(15);
}

public sealed class RoomException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
