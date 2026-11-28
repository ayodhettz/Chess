using Chess.Contracts;
using Microsoft.Extensions.Options;

namespace Chess.Server.Tests;

public class CheckSnapshotTests
{
    [Fact]
    public void SnapshotReportsAndClearsCheckAndRetainsCheckmate()
    {
        RoomDirectory rooms = new(TimeProvider.System, Options.Create(new RoomLimits()));
        rooms.Connect("white"); rooms.Connect("black");
        rooms.Join("black", rooms.Create("white", Seat.White).Ticket.Code);
        RoomSnapshot Move(string actor, string from, string to) => rooms.Move(actor,
            new(rooms.Snapshot(actor).Revision, new(from, to))).State;
        Move("white", "e2", "e4"); Move("black", "f7", "f6");
        Assert.Equal("e8", Move("white", "d1", "h5").CheckedSquare);
        Assert.Null(Move("black", "g7", "g6").CheckedSquare);

        RoomDirectory mate = new(TimeProvider.System, Options.Create(new RoomLimits()));
        mate.Connect("white"); mate.Connect("black");
        mate.Join("black", mate.Create("white", Seat.White).Ticket.Code);
        foreach (var (actor, from, to) in new[] { ("white", "f2", "f3"), ("black", "e7", "e5"), ("white", "g2", "g4"), ("black", "d8", "h4") })
            mate.Move(actor, new(mate.Snapshot(actor).Revision, new(from, to)));
        Assert.Equal("e1", mate.Snapshot("white").CheckedSquare);
        Assert.Equal("Checkmate", mate.Snapshot("white").Result!.Reason);
    }
}
