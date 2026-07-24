using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Aero.Cms.Abstractions.Interfaces;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Core.Content.Templating;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Renders an exact full-page Scriban source snapshot through Aero's bounded
/// template, HTML-import, style, and static-rendering pipeline.
/// </summary>
public sealed class ScribanPageRenderer(
    ISecureScribanRenderer scribanRenderer,
    IHtmlFragmentImporter htmlImporter,
    HtmlStaticRenderer htmlRenderer,
    IStyleCompiler styleCompiler,
    ISiteStyleProfileResolver styleProfileResolver) : IPageRenderer
{
    /// <inheritdoc />
    public PageRendererId Id { get; } = new(PageRendererIds.Scriban);

    /// <inheritdoc />
    public PageRendererDescriptor Descriptor { get; } = new(
        PageRendererIds.Scriban,
        "Scriban",
        PageEditorKinds.Source,
        SupportsFragments: true,
        IsExperimental: false,
        SourceLanguage: "liquid",
        InitialSource: """
            <main class="aero-page">
              <h1>{{ page.title }}</h1>
            </main>
            """);

    /// <inheritdoc />
    public async Task<Result<RenderedPage>> RenderAsync(
        PageRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceResult = ValidateSource(request);
        if (sourceResult is Result<PageRenderSource>.Failure sourceFailure)
        {
            return sourceFailure.Error;
        }

        var source = ((Result<PageRenderSource>.Ok)sourceResult).Value;
        var definition = new ScribanRenderDefinition(
            CreateCacheIdentity(source),
            1,
            source.Source,
            DataSchema: null);
        var globals = ScribanPageScopeMapper.CreateGlobals(
            request.Metadata,
            request.ContentQueries,
            request.IsPreview);
        var renderedTemplate = await scribanRenderer.RenderTrustedAsync(
            definition,
            globals,
            cancellationToken);
        if (renderedTemplate is Result<string>.Failure templateFailure)
        {
            return templateFailure.Error;
        }

        var imported = htmlImporter.Import(
            ((Result<string>.Ok)renderedTemplate).Value);
        if (imported is Result<HtmlPageContent>.Failure importFailure)
        {
            return importFailure.Error;
        }

        var profileResult = await styleProfileResolver.ResolveAsync(
            request.Metadata.SiteId,
            cancellationToken);
        if (profileResult is Result<IStyleProfile, AeroError>.Failure profileFailure)
        {
            return profileFailure.Error;
        }

        var content = ((Result<HtmlPageContent>.Ok)imported).Value;
        var profile = ((Result<IStyleProfile, AeroError>.Ok)profileResult).Value;
        var compiled = styleCompiler.Compile(content, profile);
        if (compiled is Result<CompiledPageStyles>.Failure styleFailure)
        {
            return styleFailure.Error;
        }

        var renderedPage = htmlRenderer.RenderPage(
            content,
            ((Result<CompiledPageStyles>.Ok)compiled).Value);
        if (renderedPage is Result<RenderedHtmlPage>.Failure htmlFailure)
        {
            return htmlFailure.Error;
        }

        var page = ((Result<RenderedHtmlPage>.Ok)renderedPage).Value;
        var aliases = request.ContentQueries.ContentTypeAliases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new RenderedPage(page.Markup, page.CssText, aliases);
    }

    private static Result<PageRenderSource> ValidateSource(PageRenderRequest request)
    {
        var selectedRendererId =
            PageRendererIds.NormalizeOrDefault(request.Metadata.RendererId);
        if (!string.Equals(
                selectedRendererId,
                PageRendererIds.Scriban,
                StringComparison.Ordinal))
        {
            return AeroError.ValidationError(
                ["The page metadata does not select the Scriban renderer."]);
        }

        if (request.Source is not { } source)
        {
            return AeroError.ValidationError(
                ["A Scriban page requires an exact source version."]);
        }

        var sourceRendererId = PageRendererIds.NormalizeOrDefault(source.RendererId);
        if (!string.Equals(
                sourceRendererId,
                selectedRendererId,
                StringComparison.Ordinal))
        {
            return AeroError.ValidationError(
                ["The page source version does not match the selected renderer."]);
        }

        if (source.VersionId < 0 || source.VersionId == 0 && !request.IsPreview)
        {
            return AeroError.ValidationError(
                ["Only preview rendering may use an unpersisted source version."]);
        }

        if (source.Source is null)
        {
            return AeroError.ValidationError(["Scriban page source cannot be null."]);
        }

        var expectedHash = ComputeHash(source.Source);
        if (!string.Equals(source.SourceHash, expectedHash, StringComparison.Ordinal))
        {
            return AeroError.ValidationError(
                ["The Scriban page source hash does not match its exact source."]);
        }

        return source;
    }

    private static long CreateCacheIdentity(PageRenderSource source)
    {
        if (source.VersionId > 0)
        {
            return source.VersionId;
        }

        var hash = Convert.FromHexString(source.SourceHash);
        var identity = BinaryPrimitives.ReadInt64BigEndian(hash);
        return identity == 0 ? 1 : identity;
    }

    private static string ComputeHash(string source)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();
}
