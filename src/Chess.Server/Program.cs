using Chess.Contracts;
using Chess.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<RoomLimits>().BindConfiguration("Rooms")
    .Validate(limits => limits.MaxRooms is > 0 and <= 10000 && limits.MaxConnections is > 0 and <= 10000 &&
        limits.MaxPlies is > 0 and <= 512 && limits.MaxCommands is > 0 and <= 1024 &&
        limits.Lifetime > TimeSpan.Zero && limits.WaitingLifetime > TimeSpan.Zero &&
        limits.WaitingLifetime <= limits.Lifetime,
        "Room capacity, game length, and lifetime must be within their supported limits.").ValidateOnStart();
builder.Services.AddSingleton<RoomDirectory>();
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 4096;
    options.MaximumParallelInvocationsPerClient = 1;
    options.EnableDetailedErrors = false;
}).AddJsonProtocol(options => OnlineProtocol.ConfigureJson(options.PayloadSerializerOptions));
builder.Services.AddHostedService<RoomCleanup>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments(OnlineProtocol.HubPath) && context.Request.Headers.TryGetValue("Origin", out var origin) &&
        (origin.Count != 1 || !string.Equals(origin[0], $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    await next(context);
});
app.MapGet("/health", () => Results.Ok(new { status = "ok", protocolVersion = OnlineProtocol.Version }));
app.MapHub<GameHub>(OnlineProtocol.HubPath);
app.Run();

public partial class Program;
