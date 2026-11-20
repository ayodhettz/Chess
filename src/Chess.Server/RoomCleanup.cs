using Microsoft.AspNetCore.SignalR;

namespace Chess.Server;

public sealed class RoomCleanup(RoomDirectory rooms, IHubContext<GameHub, IGameClient> hub, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(30), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            foreach (RoomUpdate update in rooms.ExpireRooms())
                await hub.Clients.Clients(update.Recipients).RoomChanged(update.State);
    }
}
