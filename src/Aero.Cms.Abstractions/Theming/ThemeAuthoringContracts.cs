using System.Text.Json.Serialization;

namespace Aero.Cms.Abstractions.Theming;

/// <summary>Closed, browser-safe Daisy token document. Arbitrary CSS is never accepted.</summary>
public sealed class ThemeTokenSet
{
    [JsonRequired]
    public ThemeColorTokens Light { get; set; } = new();

    [JsonRequired]
    public ThemeColorTokens Dark { get; set; } = new();

    [JsonRequired]
    public ThemeShapeTokens Shape { get; set; } = new();

    [JsonRequired]
    public ThemeDefaultMode DefaultMode { get; set; } = ThemeDefaultMode.Light;
}

public sealed class ThemeColorTokens
{
    [JsonRequired] public string Base100 { get; set; } = "#ffffff";
    [JsonRequired] public string Base200 { get; set; } = "#eeeeee";
    [JsonRequired] public string Base300 { get; set; } = "#dddddd";
    [JsonRequired] public string BaseContent { get; set; } = "#202020";
    [JsonRequired] public string Primary { get; set; } = "#2563eb";
    [JsonRequired] public string PrimaryContent { get; set; } = "#ffffff";
    [JsonRequired] public string Secondary { get; set; } = "#475569";
    [JsonRequired] public string SecondaryContent { get; set; } = "#ffffff";
    [JsonRequired] public string Accent { get; set; } = "#0f766e";
    [JsonRequired] public string AccentContent { get; set; } = "#ffffff";
    [JsonRequired] public string Neutral { get; set; } = "#111827";
    [JsonRequired] public string NeutralContent { get; set; } = "#ffffff";
    [JsonRequired] public string Info { get; set; } = "#0369a1";
    [JsonRequired] public string InfoContent { get; set; } = "#ffffff";
    [JsonRequired] public string Success { get; set; } = "#15803d";
    [JsonRequired] public string SuccessContent { get; set; } = "#ffffff";
    [JsonRequired] public string Warning { get; set; } = "#ca8a04";
    [JsonRequired] public string WarningContent { get; set; } = "#000000";
    [JsonRequired] public string Error { get; set; } = "#dc2626";
    [JsonRequired] public string ErrorContent { get; set; } = "#ffffff";
}

public sealed class ThemeShapeTokens
{
    [JsonRequired] public decimal RadiusSelectorRem { get; set; } = .25m;
    [JsonRequired] public decimal RadiusFieldRem { get; set; } = .25m;
    [JsonRequired] public decimal RadiusBoxRem { get; set; } = .25m;
    [JsonRequired] public decimal SizeSelectorRem { get; set; } = .25m;
    [JsonRequired] public decimal SizeFieldRem { get; set; } = .25m;
    [JsonRequired] public decimal BorderRem { get; set; } = .0625m;
    [JsonRequired] public int Depth { get; set; }
    [JsonRequired] public int Noise { get; set; }
}
public enum ThemeDefaultMode { Light, Dark }

public sealed record CreateThemeCommand(string Name, string Slug, string? Description, ThemeTokenSet? Tokens = null);
public sealed record SaveThemeDraftCommand(long ExpectedRevision, string Name, string Slug, string? Description, ThemeTokenSet Tokens);
public sealed record AssignThemeCommand(string ThemeId, string Version, long ExpectedRevision);
public sealed record ThemeValidationWarning(string Code, string Message);
public sealed record ThemeDefinitionView(long Id, string Name, string Slug, string? Description, ThemeTokenSet Tokens, long Revision, bool Archived, IReadOnlyList<ThemeValidationWarning> ValidationWarnings);
public sealed record ThemeVersionView(string ThemeId, string Version, string DataThemeName, string Sha256, DateTimeOffset PublishedOn);
public sealed record SiteThemePublicationView(string ThemeId, string Version, long Revision, DateTimeOffset PublishedOn, string? PreviousThemeId, string? PreviousVersion);
public sealed record ThemePreviewView(string Token, DateTimeOffset ExpiresOn);
public sealed record ThemeImportEnvelope(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] ThemeImportPayload Theme);

public sealed record ThemeImportPayload(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Slug,
    string? Description,
    [property: JsonRequired] ThemeTokenSet Tokens);
