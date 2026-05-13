namespace Aero.Cms.Abstractions.Blocks.Neo;

/// <summary>
/// Stable catalog IDs used by <see cref="NeoPageNode.CatalogId"/> for legacy block migration.
/// These IDs are the authority for block type identity — change them only in coordinated
/// schema migrations, never ad-hoc.
/// </summary>
public static class NeoCatalogIds
{
    // ── Typed Aero UI blocks ───────────────────────────────────

    /// <summary>BoringHeroBlock → basic hero section.</summary>
    public const string HeroBasic = "aero.hero.basic";

    /// <summary>HeroBlock → full hero section.</summary>
    public const string HeroFull = "aero.hero.01";

    // ── Layout / composition ───────────────────────────────────

    /// <summary>ColumnsBlock → columnar layout container.</summary>
    public const string LayoutColumns = "neo.layout.columns";

    /// <summary>Generic composition wrapper (fallback).</summary>
    public const string Composition = "neo.composition";

    // ── Content / media ────────────────────────────────────────

    /// <summary>DynamicTemplateBlock / Scriban → server-side template.</summary>
    public const string TemplateScriban = "neo.template.scriban";

    /// <summary>Raw HTML block → sanitized raw markup.</summary>
    public const string RawHtml = "ui.raw-html";

    /// <summary>Image block → single image.</summary>
    public const string MediaImage = "media.image";

    /// <summary>Video block → embedded video player.</summary>
    public const string MediaVideo = "media.video";

    /// <summary>Audio block → audio player.</summary>
    public const string MediaAudio = "media.audio";

    /// <summary>Gallery / Carousel → image gallery.</summary>
    public const string MediaGallery = "media.gallery";

    /// <summary>Separator / divider → visual spacer.</summary>
    public const string Separator = "ui.separator";
}
