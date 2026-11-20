using System.Net;
using System.Threading.Channels;
using Chess.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Chess.Server.Tests;

public class HubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public HubTests(WebApplicationFactory<Program> factory) => this.factory = factory;
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DrawAgreementAndRetriesAreDeliveredToBothPlayers()
    {
        await using Client white = await Connect(); await using Client black = await Connect();
        SeatTicket host = await white.Invoke<SeatTicket>("CreateRoom", Seat.White);
        RoomSnapshot state = (await black.Invoke<SeatTicket>("JoinRoom", host.Code)).State;
        RoomCommand opening = new(Guid.NewGuid(), state.Revision, OnlineAction.Move, new("e2", "e4"));
        CommandResult first = await white.Invoke<CommandResult>("SubmitCommand", opening);
        Assert.True((await white.Invoke<CommandResult>("SubmitCommand", opening)).WasDuplicate);
        state = (await black.Invoke<CommandResult>("SubmitCommand", new RoomCommand(Guid.NewGuid(), first.State.Revision, OnlineAction.Move, new("e7", "e5")))).State;
        state = (await white.Invoke<CommandResult>("SubmitCommand", new RoomCommand(Guid.NewGuid(), state.Revision, OnlineAction.OfferDraw))).State;
        Assert.Equal(Seat.White, state.Draw.OfferedBy);
        RoomCommand accept = new(Guid.NewGuid(), state.Revision, OnlineAction.AcceptDraw);
        CommandResult ended = await black.Invoke<CommandResult>("SubmitCommand", accept);
        CommandResult retry = await black.Invoke<CommandResult>("SubmitCommand", accept);
        Assert.True(retry.WasDuplicate); Assert.Equal(ended.AppliedRevision, retry.AppliedRevision);
        Assert.Equal(ended.State.Revision, retry.State.Revision);
        Assert.Equal("DrawAgreement", (await white.Read(ended.State.Revision)).Result!.Reason);
        Assert.Equal("DrawAgreement", (await black.Read(ended.State.Revision)).Result!.Reason);
        Assert.Equal(2, retry.State.Moves.Length);
    }

    [Fact]
    public async Task IntendedClaimsUseServerHistoryAndDoNotPlayTheClaimedMove()
    {
        await using Client white = await Connect(); await using Client black = await Connect();
        SeatTicket host = await white.Invoke<SeatTicket>("CreateRoom", Seat.White);
        RoomSnapshot state = (await black.Invoke<SeatTicket>("JoinRoom", host.Code)).State;
        foreach (var (from, to) in new[] { ("g1", "f3"), ("g8", "f6"), ("f3", "g1"), ("f6", "g8"), ("g1", "f3"), ("g8", "f6"), ("f3", "g1") })
            state = await (state.Turn == Seat.White ? white : black).Invoke<RoomSnapshot>("SubmitMove", new MoveRequest(state.Revision, new(from, to)));
        Assert.Contains(new MoveIntent("f6", "g8"), state.Draw.ClaimMoves);
        CommandResult ended = await black.Invoke<CommandResult>("SubmitCommand", new RoomCommand(Guid.NewGuid(), state.Revision, OnlineAction.ClaimDraw, new("f6", "g8")));
        Assert.Equal("ThreefoldRepetition", ended.State.Result!.Reason);
        Assert.Equal(state.Fen, ended.State.Fen); Assert.Equal(7, ended.State.Moves.Length);
    }

    [Fact]
    public async Task OffTurnResignationReportsTheAuthenticatedActor()
    {
        await using Client white = await Connect(); await using Client black = await Connect();
        SeatTicket host = await white.Invoke<SeatTicket>("CreateRoom", Seat.White);
        RoomSnapshot state = (await black.Invoke<SeatTicket>("JoinRoom", host.Code)).State;
        CommandResult result = await black.Invoke<CommandResult>("SubmitCommand", new RoomCommand(Guid.NewGuid(), state.Revision, OnlineAction.Resign));
        Assert.Equal(Seat.White, result.State.Result!.Winner);
        Assert.Equal(Seat.Black, result.State.Result.ResignedBy);
        Assert.Empty(result.State.Moves);
    }

    [Fact]
    public async Task TwoClientsReceiveTheSameAuthoritativeMove()
    {
        await using Client white = await Connect(); await using Client black = await Connect();
        SeatTicket host = await white.Invoke<SeatTicket>("CreateRoom", Seat.White);
        SeatTicket guest = await black.Invoke<SeatTicket>("JoinRoom", host.Code);
        RoomSnapshot accepted = await white.Invoke<RoomSnapshot>("SubmitMove", new MoveRequest(guest.State.Revision, new("e2", "e4")));
        Assert.Equal(new[] { "e4" }, accepted.Moves); Assert.Equal(Seat.Black, accepted.Turn);
        Assert.Equal(accepted.Fen, (await white.Read(accepted.Revision)).Fen);
        Assert.Equal(accepted.Fen, (await black.Read(accepted.Revision)).Fen);
        Assert.NotEmpty(accepted.LegalMoves);
        HubException stale = await Assert.ThrowsAsync<HubException>(() => white.Invoke<RoomSnapshot>("SubmitMove", new MoveRequest(guest.State.Revision, new("e2", "e4"))));
        Assert.Contains("stale_revision", stale.Message);
        Assert.Single((await black.Invoke<RoomSnapshot>("GetState")).Moves);
    }

    [Fact]
    public async Task RoomsAreIsolatedAndUnseatedClientsCannotReadOrMove()
    {
        await using Client a = await Connect(); await using Client b = await Connect(); await using Client outsider = await Connect();
        SeatTicket first = await a.Invoke<SeatTicket>("CreateRoom", Seat.White);
        SeatTicket second = await b.Invoke<SeatTicket>("CreateRoom", Seat.White);
        Assert.NotEqual(first.Code, second.Code);
        Assert.Contains("not_seated", (await Assert.ThrowsAsync<HubException>(() => outsider.Invoke<RoomSnapshot>("GetState"))).Message);
        await Assert.ThrowsAsync<HubException>(() => outsider.Invoke<RoomSnapshot>("SubmitMove", new MoveRequest(first.State.Revision, new("e2", "e4"))));
        Assert.Equal(second.Code, (await b.Read(second.State.Revision)).Code);
        Assert.Empty((await b.Invoke<RoomSnapshot>("GetState")).Moves);
    }

    [Fact]
    public async Task ReconnectingUsesTheCredentialAndRevokesTheOldConnection()
    {
        await using Client old = await Connect(); await using Client replacement = await Connect(); await using Client opponent = await Connect();
        SeatTicket host = await old.Invoke<SeatTicket>("CreateRoom", Seat.Black);
        await opponent.Invoke<SeatTicket>("JoinRoom", host.Code);
        await Assert.ThrowsAsync<HubException>(() => replacement.Invoke<SeatTicket>("ResumeRoom", host.Code, new string('0', 64)));
        SeatTicket resumed = await replacement.Invoke<SeatTicket>("ResumeRoom", host.Code, host.Credential);
        Assert.Equal(Seat.Black, resumed.Seat);
        await Assert.ThrowsAsync<HubException>(() => old.Invoke<RoomSnapshot>("GetState"));
        Assert.True((await opponent.Read(resumed.State.Revision)).BlackConnected);
    }

    [Fact]
    public async Task DisconnectPresenceIsPublishedAndTheSeatCanResume()
    {
        await using Client old = await Connect(); await using Client opponent = await Connect(); await using Client replacement = await Connect();
        SeatTicket host = await old.Invoke<SeatTicket>("CreateRoom", Seat.White);
        SeatTicket guest = await opponent.Invoke<SeatTicket>("JoinRoom", host.Code);
        await old.Stop();
        RoomSnapshot disconnected = await opponent.Read(guest.State.Revision + 1);
        Assert.False(disconnected.WhiteConnected);
        SeatTicket resumed = await replacement.Invoke<SeatTicket>("ResumeRoom", host.Code, host.Credential);
        Assert.True(resumed.State.WhiteConnected);
        Assert.Equal(disconnected.Fen, resumed.State.Fen);
    }

    [Fact]
    public async Task CheckmateComesFromSharedRulesAndFurtherMovesAreRejected()
    {
        await using Client white = await Connect(); await using Client black = await Connect();
        SeatTicket host = await white.Invoke<SeatTicket>("CreateRoom", Seat.White);
        RoomSnapshot state = (await black.Invoke<SeatTicket>("JoinRoom", host.Code)).State;
        foreach (var (from, to) in new[] { ("f2", "f3"), ("e7", "e5"), ("g2", "g4"), ("d8", "h4") })
            state = await (state.Turn == Seat.White ? white : black).Invoke<RoomSnapshot>("SubmitMove", new MoveRequest(state.Revision, new(from, to)));
        Assert.Equal(RoomStatus.Finished, state.Status); Assert.Equal(Seat.Black, state.Result!.Winner);
        Assert.Equal("Checkmate", state.Result.Reason); Assert.Equal("Qh4#", state.Moves[^1]); Assert.Empty(state.LegalMoves);
        await Assert.ThrowsAsync<HubException>(() => white.Invoke<RoomSnapshot>("SubmitMove", new MoveRequest(state.Revision, new("e2", "e4"))));
    }

    [Fact]
    public async Task HealthIsAvailableAndForeignBrowserOriginsAreRejected()
    {
        using HttpClient http = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health", Token)).StatusCode);
        using HttpRequestMessage request = new(HttpMethod.Post, "/hubs/game/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "https://untrusted.example");
        using HttpResponseMessage response = await http.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using HttpRequestMessage ownOrigin = new(HttpMethod.Post, "/hubs/game/negotiate?negotiateVersion=1");
        ownOrigin.Headers.Add("Origin", factory.Server.BaseAddress.GetLeftPart(UriPartial.Authority));
        using HttpResponseMessage accepted = await http.SendAsync(ownOrigin, Token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    private async Task<Client> Connect()
    {
        HubConnection connection = new HubConnectionBuilder().WithUrl(new Uri(factory.Server.BaseAddress, OnlineProtocol.HubPath), options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        }).AddJsonProtocol(options => OnlineProtocol.ConfigureJson(options.PayloadSerializerOptions)).Build();
        Client client = new(connection);
        await connection.StartAsync(Token);
        return client;
    }
    private sealed class Client : IAsyncDisposable
    {
        private readonly HubConnection connection;
        private readonly Channel<RoomSnapshot> states = Channel.CreateUnbounded<RoomSnapshot>();
        public Client(HubConnection connection)
        {
            this.connection = connection;
            connection.On<RoomSnapshot>("RoomChanged", state => { states.Writer.TryWrite(state); });
        }
        public Task<T> Invoke<T>(string method, params object?[] args) => connection.InvokeCoreAsync<T>(method, args, Token);
        public Task Stop() => connection.StopAsync(Token);
        public async Task<RoomSnapshot> Read(long revision)
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            while (await states.Reader.WaitToReadAsync(timeout.Token))
                while (states.Reader.TryRead(out RoomSnapshot? state))
                    if (state.Revision >= revision) return state;
            throw new TimeoutException("Expected room update was not delivered.");
        }
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
