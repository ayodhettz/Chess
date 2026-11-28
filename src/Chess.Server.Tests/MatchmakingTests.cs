using Chess.Contracts;
using Chess.Server;
using Microsoft.Extensions.Options;

namespace Chess.Server.Tests;

public class MatchmakingTests
{
    private static RoomDirectory Store() => new(TimeProvider.System, Options.Create(new RoomLimits()));

    [Fact]
    public void TwoSearchesReceiveDifferentPrivateSeatsInTheSameActiveGame()
    {
        RoomDirectory rooms = Store(); rooms.Connect("a"); rooms.Connect("b");
        Assert.Null(rooms.FindMatch("a"));
        SeatTicket b = rooms.FindMatch("b")!;
        SeatTicket a = rooms.FindMatch("a")!;
        Assert.Equal(a.Code, b.Code); Assert.NotEqual(a.Seat, b.Seat);
        Assert.NotEqual(a.Credential, b.Credential);
        Assert.Equal(RoomStatus.Active, a.State.Status);
        Assert.Equal(a, rooms.FindMatch("a"));
        Assert.Equal(b, rooms.CancelMatch("b"));
        rooms.Disconnect("a"); rooms.Connect("resumed");
        Assert.Equal(a.Seat, rooms.Resume("resumed", a.Code, a.Credential).Ticket.Seat);
    }

    [Fact]
    public void CancellationAndDisconnectionRemoveWaitingPlayers()
    {
        RoomDirectory rooms = Store();
        foreach (string id in new[] { "a", "b", "c" }) rooms.Connect(id);
        Assert.Null(rooms.FindMatch("a")); Assert.Null(rooms.CancelMatch("a"));
        Assert.Null(rooms.FindMatch("b")); rooms.Disconnect("b");
        Assert.Null(rooms.FindMatch("c"));
        Assert.Null(rooms.CancelMatch("c"));
    }

    [Fact]
    public void PrivateRoomsNeverEnterThePublicQueue()
    {
        RoomDirectory rooms = Store(); rooms.Connect("private"); rooms.Connect("public"); rooms.Connect("guest");
        SeatTicket invitation = rooms.Create("private", Seat.White).Ticket;
        Assert.Null(rooms.FindMatch("public"));
        Assert.Equal(RoomStatus.Waiting, rooms.Snapshot("private").Status);
        Assert.Equal(invitation.Code, rooms.Join("guest", invitation.Code).Ticket.Code);
        Assert.Equal("already_seated", Assert.Throws<RoomException>(() => rooms.FindMatch("private")).Code);
    }

    [Fact]
    public void ConcurrentSearchesDoNotAllocateASeatTwice()
    {
        RoomDirectory rooms = Store();
        string[] ids = Enumerable.Range(0, 20).Select(i => i.ToString()).ToArray();
        foreach (string id in ids) rooms.Connect(id);
        Parallel.ForEach(ids, id => rooms.FindMatch(id));
        SeatTicket[] tickets = ids.Select(id => rooms.FindMatch(id)!).ToArray();
        Assert.Equal(10, tickets.Select(t => t.Code).Distinct().Count());
        Assert.All(tickets.GroupBy(t => t.Code), game => Assert.Equal(2, game.Select(t => t.Seat).Distinct().Count()));
    }
}
