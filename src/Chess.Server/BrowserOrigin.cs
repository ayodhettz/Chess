namespace Chess.Server;

public sealed class BrowserOrigin
{
    private readonly string? publicOrigin;

    public BrowserOrigin(IConfiguration configuration)
    {
        string? configured = configuration["PublicOrigin"] ?? configuration["RENDER_EXTERNAL_URL"];
        if (string.IsNullOrWhiteSpace(configured)) return;
        if (!Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
            throw new InvalidOperationException("PublicOrigin must be an HTTPS origin without a path, query, fragment, or credentials.");
        publicOrigin = uri.GetLeftPart(UriPartial.Authority);
    }

    public bool Allows(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Origin", out var origin)) return true;
        // TLS terminates at the host; do not trust arbitrary forwarded headers.
        string expected = publicOrigin ?? $"{request.Scheme}://{request.Host}";
        return origin.Count == 1 && string.Equals(origin[0], expected, StringComparison.OrdinalIgnoreCase);
    }
}
