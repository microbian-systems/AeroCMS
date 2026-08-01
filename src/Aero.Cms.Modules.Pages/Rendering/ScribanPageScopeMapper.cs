using System.Globalization;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Core.Content.Templating;
using Scriban.Runtime;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Builds the closed global scope exposed to pure Scriban pages.</summary>
public static class ScribanPageScopeMapper
{
    /// <summary>
    /// Creates read-only <c>page</c>, <c>site</c>, <c>content</c>, and
    /// <c>is_preview</c> globals.
    /// </summary>
    public static ScriptObject CreateGlobals(
        PageRenderMetadata metadata,
        PageContentQueryResolution contentQueries,
        bool isPreview,
        int maximumJsonDepth = 10)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(contentQueries);

        var page = new ScriptObject
        {
            ["id"] = metadata.Id is { } pageId
                ? pageId.ToString(CultureInfo.InvariantCulture)
                : null,
            ["title"] = metadata.Title,
            ["slug"] = metadata.Slug,
            ["path"] = metadata.Path,
            ["culture"] = metadata.Culture
        };
        SetReadOnly(page, "id", "title", "slug", "path", "culture");

        var site = new ScriptObject
        {
            ["id"] = metadata.SiteId.ToString(CultureInfo.InvariantCulture),
            ["current_culture"] = metadata.Culture
        };
        SetReadOnly(site, "id", "current_culture");

        var globals = new ScriptObject
        {
            ["page"] = page,
            ["site"] = site,
            ["content"] = ContentQueryToScribanMapper.CreateContentScope(
                contentQueries,
                maximumJsonDepth),
            ["is_preview"] = isPreview
        };
        SetReadOnly(globals, "page", "site", "content", "is_preview");
        return globals;
    }

    private static void SetReadOnly(ScriptObject value, params string[] names)
    {
        foreach (var name in names)
        {
            value.SetReadOnly(name, readOnly: true);
        }
    }
}
