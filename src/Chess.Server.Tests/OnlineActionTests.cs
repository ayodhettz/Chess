using System.Text.Json;
using Chess.Contracts;
using Microsoft.Extensions.Options;

namespace Chess.Server.Tests;

public class OnlineActionTests
{
    private readonly Clock clock = new();
    private RoomDirectory Ready(RoomLimits? limits = null)
    {
        RoomDirectory store = new(clock, Options.Create(limits ?? new()));
        store.Connect("white"); store.Connect("black");
        SeatTicket host = store.Create("white", Seat.White).Ticket;
        store.Join("black", host.Code); return store;
    }
    private static RoomCommand Command(RoomDirectory store, OnlineAction action, MoveIntent? move = null) =>
        new(Guid.NewGuid(), store.Snapshot("white").Revision, action, move);
    private static CommandResult Act(RoomDirectory store, string actor, OnlineAction action, MoveIntent? move = null) =>
        store.Submit(actor, Command(store, action, move)).Result;
    private static void Move(RoomDirectory store, string from, string to)
    {
        RoomSnapshot state = store.Snapshot("white");
        store.Submit(state.Turn == Seat.White ? "white" : "black", new(Guid.NewGuid(), state.Revision, OnlineAction.Move, new(from, to)));
    }
    private static void Open(RoomDirectory store) { Move(store, "e2", "e4"); Move(store, "e7", "e5"); }

    [Fact]
    public void DrawOffersRequireBothMovesAndOnlyTheOpponentMayRespond()
    {
        RoomDirectory store = Ready();
        Assert.False(store.Snapshot("white").Draw.CanOffer);
        Assert.Equal("invalid_action", Assert.Throws<RoomException>(() => Act(store, "white", OnlineAction.OfferDraw)).Code);
        Open(store);
        Assert.True(store.Snapshot("white").Draw.CanOffer);
        RoomSnapshot offered = Act(store, "white", OnlineAction.OfferDraw).State;
        Assert.Equal(Seat.White, offered.Draw.OfferedBy);
        Assert.False(offered.Draw.CanOffer);
        Assert.Equal("invalid_action", Assert.Throws<RoomException>(() => Act(store, "white", OnlineAction.AcceptDraw)).Code);
        RoomSnapshot accepted = Act(store, "black", OnlineAction.AcceptDraw).State;
        Assert.Equal(RoomStatus.Finished, accepted.Status);
        Assert.Equal("DrawAgreement", accepted.Result!.Reason); Assert.Null(accepted.Result.Winner);
        Assert.Null(accepted.Draw.OfferedBy); Assert.Empty(accepted.LegalMoves); Assert.Equal(2, accepted.Moves.Length);
    }

    [Fact]
    public void ExplicitDeclinesAndOpponentMovesClearPendingOffers()
    {
        RoomDirectory store = Ready(); Open(store);
        Act(store, "white", OnlineAction.OfferDraw);
        RoomSnapshot declined = Act(store, "black", OnlineAction.DeclineDraw).State;
        Assert.Null(declined.Draw.OfferedBy); Assert.Equal(Seat.White, declined.Turn);
        Act(store, "white", OnlineAction.OfferDraw);
        Move(store, "g1", "f3"); Assert.Equal(Seat.White, store.Snapshot("black").Draw.OfferedBy);
        Move(store, "g8", "f6"); Assert.Null(store.Snapshot("white").Draw.OfferedBy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentAndIntendedRepetitionClaimsEndWithoutPlayingAnExtraMove(bool intended)
    {
        RoomDirectory store = Ready();
        for (int cycle = 0; cycle < 2; cycle++)
        {
            Move(store, "g1", "f3"); Move(store, "g8", "f6"); Move(store, "f3", "g1");
            if (cycle == 1 && intended) break;
            Move(store, "f6", "g8");
        }
        RoomSnapshot before = store.Snapshot("white");
        if (intended) Assert.Contains(new MoveIntent("f6", "g8"), before.Draw.ClaimMoves);
        else Assert.True(before.Draw.CanClaim);
        CommandResult claimed = Act(store, intended ? "black" : "white", OnlineAction.ClaimDraw, intended ? new("f6", "g8") : null);
        Assert.Equal("ThreefoldRepetition", claimed.State.Result!.Reason);
        Assert.Equal(before.Fen, claimed.State.Fen); Assert.Equal(before.Moves, claimed.State.Moves);
    }

    [Fact]
    public void ResignationWorksOffTurnAndWhileTheOpponentIsDisconnected()
    {
        RoomDirectory store = Ready(); store.Disconnect("white");
        RoomSnapshot ended = store.Submit("black", new(Guid.NewGuid(), store.Snapshot("black").Revision, OnlineAction.Resign)).Result.State;
        Assert.Equal(RoomStatus.Finished, ended.Status); Assert.Equal(Seat.White, ended.Result!.Winner);
        Assert.Equal(Seat.Black, ended.Result.ResignedBy); Assert.Equal("Resignation", ended.Result.Reason);
        Assert.Empty(ended.Moves);
    }

    [Fact]
    public void ExactRetriesReturnReceiptsWithoutAnotherMutationOrBroadcast()
    {
        RoomDirectory store = Ready();
        RoomCommand command = Command(store, OnlineAction.Move, new("e2", "e4"));
        CommandExecution first = store.Submit("white", command);
        CommandExecution retry = store.Submit("white", command);
        Assert.False(first.Result.WasDuplicate); Assert.True(retry.Result.WasDuplicate);
        Assert.Null(retry.Update); Assert.Equal(first.Result.AppliedRevision, retry.Result.AppliedRevision);
        Assert.Single(retry.Result.State.Moves);
        Assert.Equal(first.Result.State.Revision, retry.Result.State.Revision);
        Assert.Equal("command_conflict", Assert.Throws<RoomException>(() => store.Submit("white", command with { Move = new("d2", "d4") })).Code);
    }

    [Fact]
    public async Task ConcurrentRetriesCommitExactlyOnce()
    {
        RoomDirectory store = Ready();
        RoomCommand command = Command(store, OnlineAction.Move, new("e2", "e4"));
        CommandExecution[] results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() => store.Submit("white", command), TestContext.Current.CancellationToken)));
        Assert.Single(results, result => !result.Result.WasDuplicate);
        Assert.Single(results, result => result.Update != null);
        Assert.Single(store.Snapshot("white").Moves);
    }

    [Fact]
    public void RetrySurvivesSeatResumeAndEvenACompletedGame()
    {
        RoomDirectory resumeStore = new(clock, Options.Create(new RoomLimits()));
        resumeStore.Connect("a"); resumeStore.Connect("b"); resumeStore.Connect("replacement");
        SeatTicket ticket = resumeStore.Create("a", Seat.White).Ticket;
        resumeStore.Join("b", ticket.Code);
        RoomCommand action = new(Guid.NewGuid(), resumeStore.Snapshot("a").Revision, OnlineAction.Resign);
        resumeStore.Submit("a", action); resumeStore.Disconnect("a");
        SeatTicket resumed = resumeStore.Resume("replacement", ticket.Code, ticket.Credential).Ticket;
        CommandExecution replay = resumeStore.Submit("replacement", action);
        Assert.True(replay.Result.WasDuplicate); Assert.Equal(resumed.State.Revision, replay.Result.State.Revision);
        Assert.Null(replay.Update);
    }

    [Fact]
    public void MalformedAndRejectedCommandsDoNotConsumeTheIdOrChangeState()
    {
        RoomDirectory store = Ready();
        RoomCommand command = Command(store, OnlineAction.ClaimDraw);
        string before = JsonSerializer.Serialize(store.Snapshot("white"));
        Assert.Throws<RoomException>(() => store.Submit("white", command));
        Assert.Throws<RoomException>(() => store.Submit("white", command with { Action = OnlineAction.Resign, Move = new("e2", "e4") }));
        Assert.Throws<RoomException>(() => store.Submit("white", command with { Id = Guid.Empty }));
        Assert.Equal(before, JsonSerializer.Serialize(store.Snapshot("white")));
        Assert.False(store.Submit("white", command with { Action = OnlineAction.Move, Move = new("e2", "e4") }).Result.WasDuplicate);
    }

    [Fact]
    public void AcceptedCommandLimitClosesWithoutInventingADraw()
    {
        RoomDirectory store = Ready(new() { MaxCommands = 3 }); Open(store);
        RoomSnapshot closed = Act(store, "white", OnlineAction.OfferDraw).State;
        Assert.Equal(RoomStatus.Closed, closed.Status); Assert.Equal("command_limit", closed.ClosedReason); Assert.Null(closed.Result);
    }

    [Fact]
    public void EvictedReceiptsStillCannotReplayAnOldRevision()
    {
        RoomDirectory store = Ready();
        RoomCommand first = Command(store, OnlineAction.Move, new("e2", "e4"));
        store.Submit("white", first); Move(store, "e7", "e5");
        for (int index = 0; index < 129; index++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            Act(store, "white", OnlineAction.OfferDraw); Act(store, "black", OnlineAction.DeclineDraw);
        }
        Assert.Equal("stale_revision", Assert.Throws<RoomException>(() => store.Submit("white", first)).Code);
        Assert.Equal(2, store.Snapshot("white").Moves.Length);
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
