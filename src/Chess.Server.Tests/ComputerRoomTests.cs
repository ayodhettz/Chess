using Chess.Contracts;
using Chess.Server;
using ChessLogic;
using Microsoft.Extensions.Options;

namespace Chess.Server.Tests;

public class ComputerRoomTests
{
    private static RoomDirectory Store() => new(TimeProvider.System, Options.Create(new RoomLimits()));

    [Theory]
    [InlineData(Seat.White, Seat.Black)]
    [InlineData(Seat.Black, Seat.White)]
    public void ComputerOccupiesOnlyTheOppositeSeatAndCannotBeJoined(Seat human, Seat computer)
    {
        RoomDirectory rooms = Store(); rooms.Connect("human"); rooms.Connect("guest");
        SeatTicket ticket = rooms.Create("human", human, computer: true).Ticket;
        Assert.Equal(RoomStatus.Active, ticket.State.Status);
        Assert.Equal(computer, ticket.State.ComputerSeat);
        Assert.True(ticket.State.WhiteConnected); Assert.True(ticket.State.BlackConnected);
        Assert.Equal("room_full", Assert.Throws<RoomException>(() => rooms.Join("guest", ticket.Code)).Code);
    }

    [Fact]
    public void ComputerSearchPreservesStateAndAppliesExactlyOneLegalReply()
    {
        RoomDirectory rooms = Store(); rooms.Connect("human");
        RoomSnapshot state = rooms.Create("human", Seat.White, computer: true).Ticket.State;
        rooms.Move("human", new(state.Revision, new("e2", "e4")));
        ComputerWork work = Assert.Single(rooms.TakeComputerTurns());
        Assert.Empty(rooms.TakeComputerTurns());
        BotDecision decision = new ChessBot().ChooseMove(work.Position, maxDepth: 1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Single(rooms.Snapshot("human").Moves);
        RoomSnapshot replied = rooms.CompleteComputerTurn(work, decision)!.State;
        Assert.Equal(2, replied.Moves.Length); Assert.Equal(Seat.White, replied.Turn);
        Assert.Null(rooms.CompleteComputerTurn(work, decision));
    }

    [Fact]
    public void ResignationInvalidatesAnInFlightComputerResult()
    {
        RoomDirectory rooms = Store(); rooms.Connect("human");
        RoomSnapshot state = rooms.Create("human", Seat.Black, computer: true).Ticket.State;
        ComputerWork work = Assert.Single(rooms.TakeComputerTurns());
        rooms.Submit("human", new(Guid.NewGuid(), state.Revision, OnlineAction.Resign));
        Assert.Null(rooms.CompleteComputerTurn(work, new ChessBot().ChooseMove(work.Position, maxDepth: 1, cancellationToken: TestContext.Current.CancellationToken)));
        Assert.Empty(rooms.Snapshot("human").Moves);
    }

    [Fact]
    public void OfflineHumanPausesSearchAndResumedSeatCanContinue()
    {
        RoomDirectory rooms = Store(); rooms.Connect("human");
        SeatTicket ticket = rooms.Create("human", Seat.Black, computer: true).Ticket;
        rooms.Disconnect("human"); Assert.Empty(rooms.TakeComputerTurns());
        rooms.Connect("replacement"); rooms.Resume("replacement", ticket.Code, ticket.Credential);
        Assert.Single(rooms.TakeComputerTurns());
    }

    [Fact]
    public void HumanCannotPlayTheComputerTurn()
    {
        RoomDirectory rooms = Store(); rooms.Connect("human");
        RoomSnapshot state = rooms.Create("human", Seat.Black, computer: true).Ticket.State;
        Assert.Equal("wrong_turn", Assert.Throws<RoomException>(() => rooms.Move("human", new(state.Revision, new("e2", "e4")))).Code);
    }
}

