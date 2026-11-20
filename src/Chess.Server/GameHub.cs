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

    public async Task<SeatTicket> JoinRoom(string code)
    {
        RoomLease lease = Guard(() => rooms.Join(Context.ConnectionId, code));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public async Task<SeatTicket> ResumeRoom(string code, string credential)
    {
        RoomLease lease = Guard(() => rooms.Resume(Context.ConnectionId, code, credential));
        await Publish(lease.Update);
        return lease.Ticket;
    }

    public Task<RoomSnapshot> GetState() => Task.FromResult(Guard(() => rooms.Snapshot(Context.ConnectionId)));

    public async Task<RoomSnapshot> SubmitMove(MoveRequest request)
    {
        RoomUpdate update = Guard(() => rooms.Move(Context.ConnectionId, request));
        await Publish(update);
        return update.State;
    }

    private Task Publish(RoomUpdate update) => Clients.Clients(update.Recipients).RoomChanged(update.State);
    private static T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (RoomException error) { throw new HubException($"{error.Code}: {error.Message}"); }
    }
}
