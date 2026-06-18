using System.Text.RegularExpressions;
using Aero.Cms.Abstractions.Blocks.Embed;

namespace Aero.Cms.Ui.Neo.Embed;

/// <summary>
/// Resolves Calendly scheduling links to the inline embed widget.
/// </summary>
public sealed partial class CalendlyEmbedResolver : IEmbedUrlResolver
{
    [GeneratedRegex(@"calendly\.com/(?<path>.+)", RegexOptions.Compiled)]
    private static partial Regex Pattern();

    public bool CanResolve(Uri uri) => uri.Host.Contains("calendly.com");

    public EmbedResolvedUrl Resolve(Uri uri) => new(
        EmbedSrc: uri.ToString(),
        DefaultRatio: AspectRatio.Standard,
        DefaultSandbox: SandboxFlags.Form);
}
