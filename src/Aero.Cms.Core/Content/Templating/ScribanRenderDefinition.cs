using System.Text.Json;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Pure render-time Scriban input shared by content types and the transitional
/// dynamic-block renderer. It is not a persisted block or document model.
/// </summary>
public sealed record ScribanRenderDefinition(
    long Identity,
    int Version,
    string Template,
    JsonDocument? DataSchema);
