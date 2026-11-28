using System.Text.Json;
using Chess.Client;
using Chess.Contracts;

if (args.Length != 2) throw new ArgumentException("Expected server URL and preferred seat.");
await using OnlineSession session = new(new Uri(args[0]));
object outputGate = new();
void Write(object value) { lock (outputGate) Console.WriteLine(JsonSerializer.Serialize(value)); }
session.Changed += () =>
{
    if (session.State is RoomSnapshot state)
        Write(new { kind = "state", moves = state.Moves, fen = state.Fen, result = state.Result?.Reason, revision = state.Revision });
};
await session.CreateAsync(Enum.Parse<Seat>(args[1], ignoreCase: true));
Write(new { kind = "ready", code = session.Ticket!.Code, seat = session.Ticket.Seat.ToString(), invitation = session.Invitation });
while (await Console.In.ReadLineAsync() is string line)
{
    using JsonDocument command = JsonDocument.Parse(line);
    JsonElement root = command.RootElement;
    string action = root.GetProperty("action").GetString()!;
    try
    {
        if (action == "resume") await session.ResumeAsync();
        else
        {
            MoveIntent? move = root.TryGetProperty("from", out JsonElement from)
                ? new(from.GetString()!, root.GetProperty("to").GetString()!, root.TryGetProperty("promotion", out JsonElement promotion) ? Enum.Parse<Promotion>(promotion.GetString()!, true) : null) : null;
            await session.SendAsync(Enum.Parse<OnlineAction>(action, true), move);
        }
        Write(new { kind = "done", action });
    }
    catch { Write(new { kind = "error", message = session.Error }); }
}
