namespace Aero.Cms.Abstractions.Blocks.Embed;

/// <summary>
/// Site/operator-level allow-list of permitted embed hosts.
/// Registered as a DI singleton. The host application or site configuration
/// populates the allowed hosts at startup.
/// </summary>
public class EmbedAllowList
{
    private readonly HashSet<string> _allowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "www.youtube-nocookie.com",
        "player.vimeo.com",
        "www.google.com",
        "calendly.com",
        "typeform.com",
        "www.loom.com"
    };

    /// <summary>
    /// Returns the read-only set of currently allowed hosts.
    /// </summary>
    public IReadOnlySet<string> AllowedHosts => _allowedHosts;

    /// <summary>
    /// Adds a host to the allow-list. Call during app startup.
    /// </summary>
    public void Allow(string host) => _allowedHosts.Add(host);

    /// <summary>
    /// Checks whether the given URI's host is on the allow-list.
    /// </summary>
    public bool IsAllowed(Uri uri) => _allowedHosts.Contains(uri.Host);
}
