using Chess.Contracts;
using Chess.Server;
using ChessLogic;
using Microsoft.Extensions.Options;

namespace Chess.Server.Tests;

public class CrazyhouseRoomTests
{
    private static RoomDirectory Store(params string[] ids)
    {
        RoomDirectory rooms = new(TimeProvider.System, Options.Create(new RoomLimits()));
        foreach (string id in ids) rooms.Connect(id);
        return rooms;
    }

    [Fact]
    public void CapturesDropsRetriesAndResumePreserveAuthoritativeReserves()
    {
        RoomDirectory rooms = Store("w", "b", "resume");
        SeatTicket host = rooms.Create("w", Seat.White, variant: ChessVariant.Crazyhouse).Ticket;
        rooms.Join("b", host.Code);
        foreach (var (id, from, to) in new[] { ("w", "e2", "e4"), ("b", "d7", "d5"), ("w", "e4", "d5"), ("b", "d8", "d5") })
            rooms.Move(id, new(rooms.Snapshot(id).Revision, new(from, to)));
        RoomSnapshot before = rooms.Snapshot("w");
        Assert.Equal(ChessVariant.Crazyhouse, before.Variant);
        Assert.Contains(new PocketPiece(Seat.White, ReservePiece.Pawn, 1), before.Pockets!);
        MoveIntent drop = new("@pawn", "e4", Drop: ReservePiece.Pawn);
        Assert.Contains(drop, before.LegalMoves);
        RoomCommand command = new(Guid.NewGuid(), before.Revision, OnlineAction.Move, drop);
        RoomSnapshot after = rooms.Submit("w", command).Result.State;
        Assert.True(rooms.Submit("w", command).Result.WasDuplicate);
        Assert.DoesNotContain(after.Pockets!, p => p.Owner == Seat.White);
        Assert.Contains(new PocketPiece(Seat.Black, ReservePiece.Pawn, 1), after.Pockets!);
        Assert.Equal("@e4", after.Moves[^1]);
        rooms.Disconnect("w");
        RoomSnapshot resumed = rooms.Resume("resume", host.Code, host.Credential).Ticket.State;
        Assert.Equal(after.Fen, resumed.Fen);
        Assert.Equal(after.Pockets, resumed.Pockets);
    }

    [Fact]
    public void DifferentVariantsNeverPairAndChangingQueueRemovesTheOldPreference()
    {
        RoomDirectory rooms = Store("standard", "crazy", "crazy2", "standard2");
        Assert.Null(rooms.FindMatch("standard"));
        Assert.Null(rooms.FindMatch("crazy", ChessVariant.Crazyhouse));
        SeatTicket crazy = rooms.FindMatch("crazy2", ChessVariant.Crazyhouse)!;
        Assert.Equal(rooms.FindMatch("crazy", ChessVariant.Crazyhouse)!.Code, crazy.Code);
        Assert.Equal(ChessVariant.Crazyhouse, crazy.State.Variant);
        Assert.Null(rooms.FindMatch("standard", ChessVariant.Crazyhouse));
        Assert.Null(rooms.FindMatch("standard2"));
    }

    [Fact]
    public void LegacyClientsCannotOccupyCrazyhouseSeats()
    {
        RoomDirectory rooms = Store("host", "old", "new", "resumed");
        SeatTicket host = rooms.Create("host", Seat.White, variant: ChessVariant.Crazyhouse).Ticket;
        Assert.Equal("unsupported_variant", Assert.Throws<RoomException>(() => rooms.Join("old", host.Code, false)).Code);
        Assert.Equal(Seat.Black, rooms.Join("new", host.Code).Ticket.Seat);
        rooms.Disconnect("host");
        Assert.Equal("unsupported_variant", Assert.Throws<RoomException>(() => rooms.Resume("old", host.Code, host.Credential, false)).Code);
        Assert.Equal(Seat.White, rooms.Resume("resumed", host.Code, host.Credential).Ticket.Seat);
    }

    [Theory]
    [InlineData("e2", "e4", ReservePiece.Pawn, null)]
    [InlineData("@pawn", "a1", ReservePiece.Pawn, null)]
    [InlineData("@pawn", "e4", ReservePiece.Pawn, Promotion.Queen)]
    [InlineData("@queen", "e4", ReservePiece.Queen, null)]
    public void ForgedDropsCannotChangeTheBoard(string from, string to, ReservePiece piece, Promotion? promotion)
    {
        RoomDirectory rooms = Store("w", "b");
        SeatTicket host = rooms.Create("w", Seat.White, variant: ChessVariant.Crazyhouse).Ticket;
        rooms.Join("b", host.Code);
        RoomSnapshot before = rooms.Snapshot("w");
        Assert.Equal("invalid_move", Assert.Throws<RoomException>(() => rooms.Move("w", new(before.Revision, new(from, to, promotion, piece)))).Code);
        Assert.Equal(before.Fen, rooms.Snapshot("w").Fen);
        Assert.Equal(before.Revision, rooms.Snapshot("w").Revision);
    }

    [Fact]
    public void ComputerRoomsUseTheCustomVariantAndRejectInvalidVariants()
    {
        RoomDirectory rooms = Store("human", "invalid");
        rooms.Create("human", Seat.Black, computer: true, difficulty: BotDifficulty.Easy, variant: ChessVariant.Crazyhouse);
        ComputerWork work = Assert.Single(rooms.TakeComputerTurns());
        Assert.Equal(GameVariant.Crazyhouse, work.Position.Variant);
        BotDecision decision = new ChessBot().ChooseMove(work.Position, maxDepth: 1, nodeBudget: 100, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(rooms.CompleteComputerTurn(work, decision)!.State.Moves);
        Assert.Equal("invalid_variant", Assert.Throws<RoomException>(() => rooms.Create("invalid", Seat.White, variant: (ChessVariant)99)).Code);
    }
}
