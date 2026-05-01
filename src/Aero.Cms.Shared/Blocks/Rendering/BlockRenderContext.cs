using System.Globalization;
using Aero.Cms.Abstractions.Http.Clients;

namespace Aero.Cms.Shared.Blocks.Rendering;

/// <summary>
/// Cross-cutting rendering facts passed to block render adapters.
/// </summary>
public sealed record BlockRenderContext(
    NavigationDetail? Navigation = null,
    bool IsPreview = false,
    bool IsHtmxRequest = false,
    string? HtmxTarget = null,
    CultureInfo? Culture = null);
