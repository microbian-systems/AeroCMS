using System.Text.Json;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Pure render-time Scriban input shared by content types and the transitional
/// dynamic-block renderer. It is not a persisted block or document model.
/// </summary>
/// <param name="Identity">The stable identity included in the parsed-template cache key.</param>
/// <param name="Version">The version included in the parsed-template cache key.</param>
/// <param name="Template">The Scriban template text.</param>
/// <param name="DataSchema">
/// An optional caller-owned schema used for data validation. Renderers do not dispose it.
/// </param>
public sealed record ScribanRenderDefinition(
    long Identity,
    int Version,
    string Template,
    JsonDocument? DataSchema);
