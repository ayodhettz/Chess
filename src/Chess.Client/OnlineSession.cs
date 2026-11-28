using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using Chess.Contracts;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Chess.Client;

public sealed class OnlineSession : IAsyncDisposable
{
    private readonly HubConnection hub;
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly object snapshots = new();
    private bool disposed;
    public Uri Server { get; }
    public SeatTicket? Ticket { get; private set; }
    public RoomSnapshot? State { get; private set; }
    public RoomCommand? Pending { get; private set; }
    public bool Ready { get; private set; }
    public bool Busy { get; private set; }
    public bool Searching { get; private set; }
    public string? Error { get; private set; }
    public event Action? Changed;

    public OnlineSession(Uri server, Action<HttpConnectionOptions>? configure = null)
    {
        Server = ValidateServer(server);
        hub = new HubConnectionBuilder().WithUrl(new Uri(Server, OnlineProtocol.HubPath), options =>
        {
            // Long polling also works through the public Vercel/Cloudflare proxy.
            options.Transports = HttpTransportType.LongPolling;
            configure?.Invoke(options);
        }).WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)])
            .AddJsonProtocol(options => OnlineProtocol.ConfigureJson(options.PayloadSerializerOptions)).Build();
        hub.On<RoomSnapshot>("RoomChanged", state => Accept(state));
        hub.Reconnecting += _ => { Ready = false; Notify(); return Task.CompletedTask; };
        hub.Reconnected += async _ => { try { await ResumeAsync(); } catch { /* Error is exposed to the UI. */ } };
        hub.Closed += _ => { Ready = false; Notify(); return Task.CompletedTask; };
    }

    public static Uri ValidateServer(Uri server)
    {
        if (!server.IsAbsoluteUri || server.Scheme != "https" && !(server.Scheme == "http" && server.IsLoopback) ||
            server.AbsolutePath != "/" || server.Query.Length != 0 || server.Fragment.Length != 0 || server.UserInfo.Length != 0)
            throw new ArgumentException("Use an HTTPS server address, or HTTP localhost for development.", nameof(server));
        return server;
    }

    public static string RoomCode(string text)
    {
        if (Uri.TryCreate(text.Trim(), UriKind.Absolute, out Uri? invitation))
        {
            ValidateServer(new Uri(invitation.GetLeftPart(UriPartial.Authority)));
            string fragment = invitation.Fragment.TrimStart('#');
            text = fragment.StartsWith("room=", StringComparison.Ordinal) ? Uri.UnescapeDataString(fragment[5..]) : "";
        }
        string code = text.Trim().ToUpperInvariant();
        return Regex.IsMatch(code, "^[A-HJ-NP-Z2-9]{8}$") ? code : throw new ArgumentException("Enter an eight-character room code or invitation link.");
    }

    public string Invitation => Ticket == null ? "" : new Uri(Server, $"/#room={Ticket.Code}").AbsoluteUri;

    public Task CreateAsync(Seat seat) => Execute(async () =>
    {
        await Start();
        if (Ticket != null) throw new InvalidOperationException("This window already owns a seat.");
        await Enter(await hub.InvokeAsync<SeatTicket>("CreateRoom", seat));
    });

    public Task JoinAsync(string code) => Execute(async () =>
    {
        string normalized = RoomCode(code);
        await Start();
        if (Ticket != null) throw new InvalidOperationException("This window already owns a seat.");
        await Enter(await hub.InvokeAsync<SeatTicket>("JoinRoom", normalized));
    });

    public Task FindMatchAsync() => Execute(async () =>
    {
        if (Ticket != null) throw new InvalidOperationException("This window already owns a seat.");
        await Start();
        Searching = true; Notify();
        try
        {
            SeatTicket? ticket = null;
            while (Searching && ticket == null)
            {
                ticket = await hub.InvokeAsync<SeatTicket?>("FindMatch");
                if (ticket == null) await Task.Delay(1000);
            }
            ticket ??= await hub.InvokeAsync<SeatTicket?>("CancelMatch");
            if (ticket != null) await Enter(ticket);
        }
        finally { Searching = false; }
    });

    public void CancelSearch() { Searching = false; Notify(); }

    private async Task Enter(SeatTicket ticket)
    {
        Ticket = ticket;
        Accept(ticket.State);
        Accept(await hub.InvokeAsync<RoomSnapshot>("GetState"));
        Ready = true;
    }

    public Task ResumeAsync() => Execute(async () =>
    {
        if (Ticket == null) throw new InvalidOperationException("Create or join a room first.");
        Ready = false;
        await Start();
        try { Accept(await hub.InvokeAsync<RoomSnapshot>("GetState")); }
        catch (HubException error) when (Code(error) == "not_seated")
        {
            SeatTicket resumed = await hub.InvokeAsync<SeatTicket>("ResumeRoom", Ticket.Code, Ticket.Credential);
            Accept(resumed.State);
        }
        Accept(await hub.InvokeAsync<RoomSnapshot>("GetState"));
        if (Pending != null) await Deliver();
        Ready = true;
    });

    public Task SendAsync(OnlineAction action, MoveIntent? move = null) => Execute(async () =>
    {
        if (!Ready || State == null || Pending != null) throw new InvalidOperationException("Reconnect or recover the pending action first.");
        Pending = new(Guid.NewGuid(), State.Revision, action, move);
        Notify();
        await Deliver();
    });

    private async Task Deliver()
    {
        try
        {
            CommandResult result = await hub.InvokeAsync<CommandResult>("SubmitCommand", Pending);
            Accept(result.State);
            Pending = null;
        }
        catch (Exception error)
        {
            if (Code(error) is string code)
            {
                Pending = null;
                if (code is "not_seated" or "not_connected" or "room_expired" or "room_unavailable") Ready = false;
                else Accept(await hub.InvokeAsync<RoomSnapshot>("GetState"));
            }
            else Ready = false;
            throw;
        }
    }

    private async Task Execute(Func<Task> action)
    {
        await operations.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Busy = true; Error = null; Notify();
            await action();
        }
        catch (Exception error)
        {
            if (hub.State != HubConnectionState.Connected) Ready = false;
            Error = Message(error);
            throw;
        }
        finally { Busy = false; operations.Release(); Notify(); }
    }

    private async Task Start()
    {
        if (hub.State == HubConnectionState.Disconnected) await hub.StartAsync();
        if (hub.State != HubConnectionState.Connected) throw new InvalidOperationException("Connection is not ready. Reconnect shortly.");
    }

    private void Accept(RoomSnapshot state)
    {
        lock (snapshots)
        {
            if (Ticket == null || state.Code != Ticket.Code) return;
            if (state.ProtocolVersion != OnlineProtocol.Version)
            {
                Ready = false;
                throw new InvalidOperationException("Update this app to match the server protocol.");
            }
            if (State != null && state.Revision < State.Revision) return;
            State = state;
        }
        Notify();
    }

    private void Notify() { if (!disposed) Changed?.Invoke(); }
    private static string? Code(Exception error) => Regex.Match(error.Message,
        @"\b(stale_revision|not_seated|not_connected|room_expired|room_unavailable|room_full|invalid_credential|command_conflict|capacity|rate_limit|wrong_turn|invalid_move|game_finished|opponent_unavailable|invalid_action|game_not_started|invalid_command)\b") is { Success: true } match ? match.Value : null;
    private static string Message(Exception error) => Code(error) switch
    {
        "stale_revision" => "The game changed. The board is refreshed; choose your action again.",
        "room_full" => "Both seats are reserved in that room.",
        "room_unavailable" or "room_expired" => "The room expired or the server restarted. Start a new game.",
        "not_seated" => "This connection no longer owns the seat. Reconnect to resume it.",
        "opponent_unavailable" => "Wait for your opponent to reconnect.",
        "wrong_turn" => "It is your opponent's turn.",
        "rate_limit" => "Too many requests. Wait a few seconds before retrying.",
        _ => error is ArgumentException or InvalidOperationException ? error.Message : "The request failed. Reconnect or recover the pending action."
    };

    public async ValueTask DisposeAsync()
    {
        disposed = true; Ready = false;
        await hub.DisposeAsync();
    }
}
