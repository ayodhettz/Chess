using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chess.Contracts;

public enum Seat { White, Black }
public enum Promotion { Queen, Rook, Bishop, Knight }
public enum RoomStatus { Waiting, Active, Finished, Closed }
public sealed record MoveIntent(string From, string To, Promotion? Promotion = null);
public sealed record MoveRequest(long ExpectedRevision, MoveIntent Move);
public sealed record GameOutcome(Seat? Winner, string Reason);
public sealed record RoomSnapshot(int ProtocolVersion, string Code, long Revision, RoomStatus Status,
    bool WhiteConnected, bool BlackConnected, Seat Turn, string Fen, string[] Moves,
    MoveIntent[] LegalMoves, GameOutcome? Result, string? ClosedReason, DateTimeOffset ExpiresAt);
public sealed record SeatTicket(string Code, Seat Seat, string Credential, RoomSnapshot State);

public static class OnlineProtocol
{
    public const int Version = 1;
    public const string HubPath = "/hubs/game";
    public static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    }
}
