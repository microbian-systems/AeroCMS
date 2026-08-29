namespace Aero.Cms.Hosting;

/// <summary>
/// Identifies a deterministic host-catalog validation failure.
/// </summary>
public sealed class AeroCmsCatalogException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new catalog exception.
    /// </summary>
    /// <param name="code">Stable machine-readable failure code.</param>
    /// <param name="message">Human-readable failure description.</param>
    public AeroCmsCatalogException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    /// <summary>Gets the stable machine-readable failure code.</summary>
    public string Code { get; }
}
