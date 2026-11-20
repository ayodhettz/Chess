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
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; connect-src 'self' ws: wss:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (context.Request.Path == "/" || context.Request.Path == "/index.html")
        context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ok", protocolVersion = OnlineProtocol.Version }));
app.MapHub<GameHub>(OnlineProtocol.HubPath);
app.Run();

public partial class Program;
