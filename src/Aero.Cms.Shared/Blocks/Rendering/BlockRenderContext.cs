using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Cross-cutting rendering facts passed to block render adapters.
/// </summary>
/// <param name="Navigation">Runtime navigation info (site, page, etc.).</param>
/// <param name="IsPreview">True when rendering in preview/draft mode.</param>
/// <param name="IsHtmxRequest">True when the request comes from HTMX.</param>
/// <param name="HtmxTarget">HTMX target element ID, if applicable.</param>
/// <param name="Culture">Current request culture.</param>
/// <param name="NestingDepth">Current nesting depth for recursive composition rendering. Starts at 0.</param>
/// <param name="MaxNestingDepth">Maximum allowed nesting depth before truncation. Default 5.</param>
public sealed record BlockRenderContext(
    NavigationDetail? Navigation = null,
    bool IsPreview = false,
    bool IsHtmxRequest = false,
    string? HtmxTarget = null,
    CultureInfo? Culture = null,
    int NestingDepth = 0,
    int MaxNestingDepth = 5)
{
    public CultureInfo Culture { get; init; } = Culture ?? CultureInfo.CurrentCulture;
}
