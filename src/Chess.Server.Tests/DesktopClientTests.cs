using System.Text.Json;
using Chess.Client;
using Chess.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Connections;

namespace Chess.Server.Tests;

public class DesktopClientTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public DesktopClientTests(WebApplicationFactory<Program> factory) => this.factory = factory;
    private OnlineSession Desktop() => new(factory.Server.BaseAddress, options => options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler());

    [Fact]
    public async Task DesktopAndWirePeerShareMovesDrawActionsAndResults()
    {
        await using OnlineSession desktop = Desktop();
        await desktop.CreateAsync(Seat.White);
        Assert.True(desktop.Ready);
        Assert.DoesNotContain(desktop.Ticket!.Credential, desktop.Invitation);
        await using HubConnection peer = Peer(); await peer.StartAsync(TestContext.Current.CancellationToken);
        await peer.InvokeAsync<SeatTicket>("JoinRoom", desktop.Ticket.Code, TestContext.Current.CancellationToken);
        await desktop.ResumeAsync();
        await desktop.SendAsync(OnlineAction.Move, new("e2", "e4"));
        RoomSnapshot state = await peer.InvokeAsync<RoomSnapshot>("GetState", TestContext.Current.CancellationToken);
        Assert.Equal("e4", Assert.Single(state.Moves));
        state = (await peer.InvokeAsync<CommandResult>("SubmitCommand", new RoomCommand(Guid.NewGuid(), state.Revision, OnlineAction.Move, new("e7", "e5")), TestContext.Current.CancellationToken)).State;
        await desktop.ResumeAsync(); await desktop.SendAsync(OnlineAction.OfferDraw);
        state = await peer.InvokeAsync<RoomSnapshot>("GetState", TestContext.Current.CancellationToken);
        await peer.InvokeAsync<CommandResult>("SubmitCommand", new RoomCommand(Guid.NewGuid(), state.Revision, OnlineAction.AcceptDraw), TestContext.Current.CancellationToken);
        await desktop.ResumeAsync();
        Assert.Equal("DrawAgreement", desktop.State!.Result!.Reason);
        Assert.Null(desktop.Pending);
    }

    [Fact]
    public async Task DesktopJoinsAnInvitationAndResignsOffTurn()
    {
        await using HubConnection peer = Peer(); await peer.StartAsync(TestContext.Current.CancellationToken);
        SeatTicket host = await peer.InvokeAsync<SeatTicket>("CreateRoom", Seat.White, TestContext.Current.CancellationToken);
        await using OnlineSession desktop = Desktop();
        await desktop.JoinAsync($"https://example.test/#room={host.Code}");
        Assert.Equal(Seat.Black, desktop.Ticket!.Seat);
        await desktop.SendAsync(OnlineAction.Resign);
        Assert.Equal(Seat.White, desktop.State!.Result!.Winner);
        Assert.Equal(Seat.Black, desktop.State.Result.ResignedBy);
    }

    [Theory]
    [InlineData("https://example.test/", true)]
    [InlineData("http://localhost:5080/", true)]
    [InlineData("http://example.test/", false)]
    [InlineData("https://user:secret@example.test/", false)]
    [InlineData("https://example.test/path", false)]
    public void NativeServerAddressesRequireTlsExceptForLocalDevelopment(string address, bool valid)
    {
        if (valid) Assert.Equal(new Uri(address), OnlineSession.ValidateServer(new Uri(address)));
        else Assert.Throws<ArgumentException>(() => OnlineSession.ValidateServer(new Uri(address)));
    }

    [Theory]
    [InlineData("abcdefgh", "ABCDEFGH")]
    [InlineData("https://example.test/#room=ABCDEFGH", "ABCDEFGH")]
    public void InvitationParsingUsesOnlyTheRoomCode(string input, string expected) => Assert.Equal(expected, OnlineSession.RoomCode(input));

    [Fact]
    public async Task LostCommandResponseReconnectsAndRetriesWithoutDuplicatingTheMove()
    {
        LostResponse response = new();
        await using OnlineSession desktop = new(factory.Server.BaseAddress, options =>
            options.HttpMessageHandlerFactory = _ => new LoseCommandResponse(factory.Server.CreateHandler(), response));
        await desktop.CreateAsync(Seat.White);
        await using HubConnection peer = Peer(); await peer.StartAsync(TestContext.Current.CancellationToken);
        await peer.InvokeAsync<SeatTicket>("JoinRoom", desktop.Ticket!.Code, TestContext.Current.CancellationToken);
        await desktop.ResumeAsync();
        try { await desktop.SendAsync(OnlineAction.Move, new("e2", "e4")); } catch (Exception) { }
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (!desktop.Ready || desktop.Pending != null)
            await Task.Delay(20, timeout.Token);
        Assert.True(response.Dropped);
        RoomSnapshot state = await peer.InvokeAsync<RoomSnapshot>("GetState", TestContext.Current.CancellationToken);
        Assert.Equal("e4", Assert.Single(state.Moves));
        Assert.Equal(Seat.Black, state.Turn);
    }

    private sealed class LostResponse { public string? Invocation; public bool Dropped; }
    private sealed class LoseCommandResponse(HttpMessageHandler inner, LostResponse lost) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method == HttpMethod.Post && request.Content != null && !lost.Dropped)
            {
                foreach (string message in (await request.Content.ReadAsStringAsync(token)).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
                {
                    using JsonDocument document = JsonDocument.Parse(message);
                    if (document.RootElement.TryGetProperty("target", out JsonElement target) && target.GetString() == "SubmitCommand")
                        lost.Invocation = document.RootElement.GetProperty("invocationId").GetString();
                }
            }
            HttpResponseMessage response = await base.SendAsync(request, token);
            if (request.Method == HttpMethod.Get && lost.Invocation != null && !lost.Dropped)
            {
                string body = await response.Content.ReadAsStringAsync(token);
                if (body.Contains($"\"invocationId\":\"{lost.Invocation}\"", StringComparison.Ordinal))
                {
                    lost.Dropped = true; response.Dispose();
                    throw new HttpRequestException("Simulated loss after the server accepted the command.");
                }
            }
            return response;
        }
    }

    private HubConnection Peer() => new HubConnectionBuilder().WithUrl(new Uri(factory.Server.BaseAddress, OnlineProtocol.HubPath), options =>
    {
        options.Transports = HttpTransportType.LongPolling;
        options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
    }).AddJsonProtocol(options => OnlineProtocol.ConfigureJson(options.PayloadSerializerOptions)).Build();
}
