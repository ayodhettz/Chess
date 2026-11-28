using Chess.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace Chess.Server;

public interface IGameClient
{
    Task RoomChanged(RoomSnapshot state);
}

public sealed class GameHub(RoomDirectory rooms) : Hub<IGameClient>
{
    public override Task OnConnectedAsync()
    {
        try { rooms.Connect(Context.ConnectionId); }
        catch (RoomException) { Context.Abort(); }
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        RoomUpdate? update = rooms.Disconnect(Context.ConnectionId);
        if (update != null) await Publish(update);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<SeatTicket> CreateRoom(Seat preferredSeat)
    {
        RoomLease lease = Guard(() => rooms.Create(Context.ConnectionId, preferredSeat));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> CreateComputerRoom(Seat humanSeat)
    {
        RoomLease lease = Guard(() => rooms.Create(Context.ConnectionId, humanSeat, computer: true));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> CreateComputerGame(Seat humanSeat, string difficulty)
    {
        if (!Enum.TryParse<ChessLogic.BotDifficulty>(difficulty, true, out var level) || !Enum.IsDefined(level))
            throw new HubException("invalid_difficulty: Choose Easy, Medium, or Hard.");
        RoomLease lease = Guard(() => rooms.Create(Context.ConnectionId, humanSeat, computer: true, difficulty: level));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> JoinRoom(string code)
    {
        RoomLease lease = Guard(() => rooms.Join(Context.ConnectionId, code, supportsCrazyhouse: false));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> ResumeRoom(string code, string credential)
    {
        RoomLease lease = Guard(() => rooms.Resume(Context.ConnectionId, code, credential, supportsCrazyhouse: false));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> CreateVariantRoom(Seat seat, ChessVariant variant)
    {
        RoomLease lease = Guard(() => rooms.Create(Context.ConnectionId, seat, variant: variant));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> CreateVariantComputerGame(Seat seat, string difficulty, ChessVariant variant)
    {
        if (!Enum.TryParse<ChessLogic.BotDifficulty>(difficulty, true, out var level) || !Enum.IsDefined(level))
            throw new HubException("invalid_difficulty: Choose Easy, Medium, or Hard.");
        RoomLease lease = Guard(() => rooms.Create(Context.ConnectionId, seat, computer: true, difficulty: level, variant: variant));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> JoinGame(string code)
    {
        RoomLease lease = Guard(() => rooms.Join(Context.ConnectionId, code));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> ResumeGame(string code, string credential)
    {
        RoomLease lease = Guard(() => rooms.Resume(Context.ConnectionId, code, credential));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public Task<SeatTicket?> FindVariantMatch(ChessVariant variant) => Task.FromResult(Guard(() => rooms.FindMatch(Context.ConnectionId, variant)));

    public Task<SeatTicket?> FindMatch() => Task.FromResult(Guard(() => rooms.FindMatch(Context.ConnectionId)));
    public Task<SeatTicket?> CancelMatch() => Task.FromResult(Guard(() => rooms.CancelMatch(Context.ConnectionId)));

    public Task<RoomSnapshot> GetState() => Task.FromResult(Guard(() => rooms.Snapshot(Context.ConnectionId)));

    public async Task<RoomSnapshot> SubmitMove(MoveRequest request)
    {
        RoomUpdate update = Guard(() => rooms.Move(Context.ConnectionId, request));
        await Publish(update);
        return update.State;
    }

    public async Task<CommandResult> SubmitCommand(RoomCommand command)
    {
        CommandExecution execution = Guard(() => rooms.Submit(Context.ConnectionId, command));
        if (execution.Update != null) await Publish(execution.Update);
        return execution.Result;
    }

    private Task Publish(RoomUpdate update) => Clients.Clients(update.Recipients).RoomChanged(update.State);
    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (RoomException error) { throw new HubException($"{error.Code}: {error.Message}"); }
    }
}
