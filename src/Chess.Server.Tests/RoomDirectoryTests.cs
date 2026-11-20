using System.Text.Json;
using Chess.Contracts;
using Microsoft.Extensions.Options;

namespace Chess.Server.Tests;

public class RoomDirectoryTests
{
    private readonly TestClock clock = new();
    private RoomDirectory Store(RoomLimits? limits = null) => new(clock, Options.Create(limits ?? new()));

    [Theory]
    [InlineData(Promotion.Knight, "bxa8=N")]
    [InlineData(Promotion.Queen, "bxa8=Q")]
    public void PromotionMustBeExplicitAndSupportsUnderpromotion(Promotion promotion, string san)
    {
        RoomDirectory store = Ready();
        Play(store, [("a2", "a4"), ("h7", "h5"), ("a4", "a5"), ("h5", "h4"),
            ("a5", "a6"), ("h4", "h3"), ("a6", "b7"), ("h3", "g2")]);
        long revision = store.Snapshot("a").Revision;
        Assert.Throws<RoomException>(() => store.Move("a", new(revision, new("b7", "a8"))));
        RoomSnapshot promoted = store.Move("a", new(revision, new("b7", "a8", promotion))).State;
        Assert.Equal(san, promoted.Moves[^1]);
    }

    [Fact]
    public void CastlingAndEnPassantUseGeneratedSpecialMoves()
    {
        RoomDirectory castle = Ready();
        RoomSnapshot castled = Play(castle, [("e2", "e4"), ("e7", "e5"), ("g1", "f3"),
            ("b8", "c6"), ("f1", "c4"), ("g8", "f6"), ("e1", "g1")]);
        Assert.Equal("O-O", castled.Moves[^1]);
        Assert.EndsWith("RNBQ1RK1 b kq - 5 4", castled.Fen);
        RoomDirectory capture = Ready();
        RoomSnapshot captured = Play(capture, [("e2", "e4"), ("a7", "a6"), ("e4", "e5"), ("d7", "d5"), ("e5", "d6")]);
        Assert.Equal("exd6", captured.Moves[^1]);
        Assert.Contains("/p2P4/8/", captured.Fen);
    }

    private static RoomSnapshot Play(RoomDirectory store, (string From, string To)[] moves)
    {
        RoomSnapshot state = store.Snapshot("a");
        foreach (var (from, to) in moves)
            state = store.Move(state.Turn == Seat.White ? "a" : "b", new(state.Revision, new(from, to))).State;
        return state;
    }

    [Fact]
    public void SeatsAreReservedAndCredentialsAreNotInSnapshots()
    {
        RoomDirectory store = Store(); store.Connect("a"); store.Connect("b"); store.Connect("c");
        SeatTicket owner = store.Create("a", Seat.Black).Ticket;
        SeatTicket guest = store.Join("b", owner.Code.ToLowerInvariant()).Ticket;
        Assert.Equal(Seat.Black, owner.Seat); Assert.Equal(Seat.White, guest.Seat);
        Assert.NotEqual(owner.Credential, guest.Credential);
        Assert.Equal(64, owner.Credential.Length);
        Assert.DoesNotContain(owner.Credential, JsonSerializer.Serialize(guest.State));
        Assert.Equal("room_full", Assert.Throws<RoomException>(() => store.Join("c", owner.Code)).Code);
        Assert.Equal("already_seated", Assert.Throws<RoomException>(() => store.Create("a", Seat.White)).Code);
    }

    [Fact]
    public void RejectedCommandsLeaveThePositionAndRevisionUnchanged()
    {
        RoomDirectory store = Ready();
        RoomSnapshot before = store.Snapshot("a");
        Assert.Equal("wrong_turn", Assert.Throws<RoomException>(() => store.Move("b", new(before.Revision, new("e7", "e5")))).Code);
        Assert.Equal("invalid_move", Assert.Throws<RoomException>(() => store.Move("a", new(before.Revision, new("e2", "e5")))).Code);
        Assert.Equal("invalid_move", Assert.Throws<RoomException>(() => store.Move("a", new(before.Revision, new("a9", "e5")))).Code);
        Assert.Equal("invalid_move", Assert.Throws<RoomException>(() => store.Move("a", new(before.Revision, new("e2", "e4", (Promotion)99)))).Code);
        Assert.Equal("stale_revision", Assert.Throws<RoomException>(() => store.Move("a", new(before.Revision - 1, new("e2", "e4")))).Code);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(store.Snapshot("a")));
    }

    [Fact]
    public async Task ConcurrentMovesAtTheSameRevisionCommitExactlyOnce()
    {
        RoomDirectory store = Ready(); long revision = store.Snapshot("a").Revision;
        Task<bool>[] attempts = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            try { store.Move("a", new(revision, new("e2", "e4"))); return true; }
            catch (RoomException error) { Assert.Equal("stale_revision", error.Code); return false; }
        }, TestContext.Current.CancellationToken)).ToArray();
        bool[] results = await Task.WhenAll(attempts);
        Assert.Single(results, value => value);
        Assert.Single(store.Snapshot("a").Moves);
    }

    [Fact]
    public void ResumeReplacesOldConnectionAndPreservesFullHistory()
    {
        RoomDirectory store = Store(); store.Connect("a"); store.Connect("b"); store.Connect("new");
        SeatTicket owner = store.Create("a", Seat.White).Ticket; store.Join("b", owner.Code);
        store.Move("a", new(store.Snapshot("a").Revision, new("e2", "e4")));
        Assert.Equal("invalid_credential", Assert.Throws<RoomException>(() => store.Resume("new", owner.Code, new string('0', 64))).Code);
        SeatTicket resumed = store.Resume("new", owner.Code, owner.Credential).Ticket;
        Assert.Equal(new[] { "e4" }, resumed.State.Moves);
        Assert.Equal("not_seated", Assert.Throws<RoomException>(() => store.Snapshot("a")).Code);
        Assert.Null(store.Disconnect("a"));
        Assert.True(store.Snapshot("new").WhiteConnected);
    }

    [Fact]
    public void DisconnectedOpponentsKeepTheirSeatButMovesWaitForThem()
    {
        RoomDirectory store = Store(); store.Connect("a"); store.Connect("b"); store.Connect("c");
        SeatTicket owner = store.Create("a", Seat.White).Ticket; SeatTicket guest = store.Join("b", owner.Code).Ticket;
        store.Disconnect("b");
        Assert.False(store.Snapshot("a").BlackConnected);
        Assert.Equal("room_full", Assert.Throws<RoomException>(() => store.Join("c", owner.Code)).Code);
        Assert.Equal("opponent_unavailable", Assert.Throws<RoomException>(() => store.Move("a", new(store.Snapshot("a").Revision, new("e2", "e4")))).Code);
        Assert.True(store.Resume("c", owner.Code, guest.Credential).Ticket.State.BlackConnected);
    }

    [Fact]
    public void ExpiryClosesRoomsAndReleasesTheirCapacity()
    {
        RoomDirectory store = Store(new() { MaxRooms = 1 }); store.Connect("a"); store.Connect("b");
        SeatTicket owner = store.Create("a", Seat.White).Ticket;
        Assert.Equal("capacity", Assert.Throws<RoomException>(() => store.Create("b", Seat.Black)).Code);
        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal("room_expired", Assert.Throws<RoomException>(() => store.Join("b", owner.Code)).Code);
        RoomUpdate expired = Assert.Single(store.ExpireRooms());
        Assert.Equal(RoomStatus.Closed, expired.State.Status); Assert.Equal("expired", expired.State.ClosedReason);
        Assert.Equal(new[] { "a" }, expired.Recipients);
        Assert.NotNull(store.Create("b", Seat.Black));
        Assert.Equal("not_seated", Assert.Throws<RoomException>(() => store.Snapshot("a")).Code);
    }

    [Fact]
    public void ActiveRoomsUseTheHardLifetimeAndGameLengthIsBounded()
    {
        RoomDirectory store = Ready(new() { MaxPlies = 2 });
        clock.Advance(TimeSpan.FromMinutes(20));
        RoomSnapshot first = store.Move("a", new(store.Snapshot("a").Revision, new("e2", "e4"))).State;
        RoomSnapshot last = store.Move("b", new(first.Revision, new("e7", "e5"))).State;
        Assert.Equal(RoomStatus.Closed, last.Status); Assert.Equal("move_limit", last.ClosedReason); Assert.Empty(last.LegalMoves);
        Assert.Equal("game_finished", Assert.Throws<RoomException>(() => store.Move("a", new(last.Revision, new("g1", "f3")))).Code);
        clock.Advance(TimeSpan.FromHours(4));
        Assert.Single(store.ExpireRooms());
    }

    [Fact]
    public void ConnectionAndCommandCapacityAreBounded()
    {
        RoomDirectory store = Store(new() { MaxConnections = 1 }); store.Connect("a");
        Assert.Equal("capacity", Assert.Throws<RoomException>(() => store.Connect("b")).Code);
        store.Create("a", Seat.White);
        for (int i = 0; i < 59; i++) store.Snapshot("a");
        Assert.Equal("rate_limit", Assert.Throws<RoomException>(() => store.Snapshot("a")).Code);
        clock.Advance(TimeSpan.FromSeconds(11)); Assert.NotNull(store.Snapshot("a"));
        store.Disconnect("a"); store.Connect("b");
    }

    private RoomDirectory Ready(RoomLimits? limits = null)
    {
        RoomDirectory store = Store(limits); store.Connect("a"); store.Connect("b");
        SeatTicket ticket = store.Create("a", Seat.White).Ticket; store.Join("b", ticket.Code); return store;
    }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
