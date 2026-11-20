using Chess.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Chess.Server.Tests;

public class BrowserOriginTests
{
    [Theory]
    [InlineData("PublicOrigin")]
    [InlineData("RENDER_EXTERNAL_URL")]
    public void HostedOriginWorksBehindHttpProxyWithoutTrustingForwardedHeaders(string setting)
    {
        BrowserOrigin policy = Policy(new() { [setting] = "https://chess.example" });
        DefaultHttpContext context = new();
        context.Request.Scheme = "http"; context.Request.Host = new("internal:8080");
        context.Request.Headers.Origin = "https://chess.example";
        Assert.True(policy.Allows(context.Request));
        context.Request.Headers.Origin = "http://chess.example";
        Assert.False(policy.Allows(context.Request));
        context.Request.Headers.Origin = "https://foreign.example";
        context.Request.Headers["X-Forwarded-Host"] = "foreign.example";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        Assert.False(policy.Allows(context.Request));
        context.Request.Headers.Remove("Origin");
        Assert.True(policy.Allows(context.Request));
    }

    [Fact]
    public void LocalDevelopmentRequiresExactRequestOriginAndRejectsMultipleOrigins()
    {
        BrowserOrigin policy = Policy(new());
        DefaultHttpContext context = new();
        context.Request.Scheme = "http"; context.Request.Host = new("localhost:5080");
        context.Request.Headers.Origin = "http://localhost:5080";
        Assert.True(policy.Allows(context.Request));
        context.Request.Headers.Origin = new Microsoft.Extensions.Primitives.StringValues(new[] { "http://localhost:5080", "https://foreign.example" });
        Assert.False(policy.Allows(context.Request));
    }

    [Fact]
    public void ExplicitOriginOverridesRenderDefault()
    {
        BrowserOrigin policy = Policy(new() { ["PublicOrigin"] = "https://custom.example", ["RENDER_EXTERNAL_URL"] = "https://default.onrender.com" });
        DefaultHttpContext context = new(); context.Request.Headers.Origin = "https://custom.example";
        Assert.True(policy.Allows(context.Request));
        context.Request.Headers.Origin = "https://default.onrender.com";
        Assert.False(policy.Allows(context.Request));
    }

    [Theory]
    [InlineData("http://chess.example")]
    [InlineData("https://chess.example/path")]
    [InlineData("https://user:pass@chess.example")]
    [InlineData("https://chess.example?query")]
    [InlineData("https://chess.example#fragment")]
    [InlineData("invalid")]
    public void InvalidHostedOriginsFailAtStartup(string origin) =>
        Assert.Throws<InvalidOperationException>(() => Policy(new() { ["PublicOrigin"] = origin }));

    private static BrowserOrigin Policy(Dictionary<string, string?> settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
