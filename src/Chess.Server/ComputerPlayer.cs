using ChessLogic;
using Microsoft.AspNetCore.SignalR;

namespace Chess.Server;

public sealed class ComputerPlayer(RoomDirectory rooms, IHubContext<GameHub, IGameClient> hub,
    ILogger<ComputerPlayer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // One bounded search at a time keeps computer rooms from saturating the host.
            foreach (ComputerWork work in rooms.TakeComputerTurns())
            {
                BotDecision? decision = null;
                try { decision = await new ChessBot().ChooseMoveAsync(work.Position, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogError(error, "Computer search failed."); }
                RoomUpdate? update = rooms.CompleteComputerTurn(work, decision);
                if (update != null) await hub.Clients.Clients(update.Recipients).RoomChanged(update.State);
            }
        }
    }
}
