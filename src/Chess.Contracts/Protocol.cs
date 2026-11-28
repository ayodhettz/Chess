using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chess.Contracts;

public enum Seat { White, Black }
public enum ChessVariant { Standard, Crazyhouse }
public enum ReservePiece { Pawn, Knight, Bishop, Rook, Queen }
public sealed record PocketPiece(Seat Owner, ReservePiece Piece, int Count);
public enum Promotion { Queen, Rook, Bishop, Knight }
public enum RoomStatus { Waiting, Active, Finished, Closed }
public enum OnlineAction { Move, OfferDraw, AcceptDraw, DeclineDraw, ClaimDraw, Resign }
public sealed record MoveIntent(string From, string To, Promotion? Promotion = null, ReservePiece? Drop = null);
public sealed record MoveRequest(long ExpectedRevision, MoveIntent Move);
public sealed record RoomCommand(Guid Id, long ExpectedRevision, OnlineAction Action, MoveIntent? Move = null);
public sealed record CommandResult(Guid Id, bool WasDuplicate, long AppliedRevision, RoomSnapshot State);
public sealed record DrawAvailability(Seat? OfferedBy, bool CanOffer, bool CanClaim, MoveIntent[] ClaimMoves);
public sealed record GameOutcome(Seat? Winner, string Reason, Seat? ResignedBy = null);
public sealed record RoomSnapshot(int ProtocolVersion, string Code, long Revision, RoomStatus Status,
    bool WhiteConnected, bool BlackConnected, Seat Turn, string Fen, string[] Moves,
    MoveIntent[] LegalMoves, GameOutcome? Result, string? ClosedReason, DateTimeOffset ExpiresAt, DrawAvailability Draw, Seat? ComputerSeat = null, string? CheckedSquare = null,
    ChessVariant Variant = ChessVariant.Standard, PocketPiece[]? Pockets = null);
public sealed record SeatTicket(string Code, Seat Seat, string Credential, RoomSnapshot State);

public static class OnlineProtocol
{
    public const int Version = 2;
    public const string HubPath = "/hubs/game";
    public static void ConfigureJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
    }
}
