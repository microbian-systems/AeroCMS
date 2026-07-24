namespace Aero.Cms.Abstractions.Http.Clients;

using System.Net.Http.Json;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Html;

using Aero.Core.Railway;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for pages HTTP client.
/// </summary>
public interface IPagesHttpClient
{
    /// <summary>
    /// Gets all pages with pagination and optional search.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of page summaries or an error.</returns>
    Task<Result<PagedResult<PageSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 10, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a page detail by its identifier.
    /// </summary>
    /// <param name="id">The page identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>Gets the exact editable source through the manager-only authoring endpoint.</summary>
    Task<Result<PageSourceViewModel, AeroError>> GetSourceAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Gets a page detail by its slug.
    /// </summary>
    /// <param name="slug">The page slug.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Gets the explicitly registered application-fragment catalog.</summary>
    Task<Result<IReadOnlyList<PageRegisteredFragmentDescriptor>, AeroError>> GetRegisteredFragmentsAsync(
        CancellationToken ct = default);

    /// <summary>Gets the explicitly registered full-page renderer catalog.</summary>
    Task<Result<IReadOnlyList<PageRendererDescriptor>, AeroError>> GetRenderersAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Gets published pages with pagination.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A paged result of page summaries or an error.</returns>
    Task<Result<PagedResult<PageSummary>, AeroError>> GetPublishedAsync(int skip = 0, int take = 10, CancellationToken ct = default);

    /// <summary>
    /// Creates a new page.
    /// </summary>
    /// <param name="request">The create page request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing page.
    /// </summary>
    /// <param name="id">The page identifier to update.</param>
    /// <param name="request">The update page request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken ct = default);

    /// <summary>Computes the published-route impact of changing a page slug or parent.</summary>
    Task<Result<PageRouteChangeImpact, AeroError>> GetRouteChangeImpactAsync(
        long id,
        PageRouteChangeRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a page.
    /// </summary>
    /// <param name="id">The page identifier to delete.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if deletion was successful or an error.</returns>
    Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Deletes a page and all its descendants.
    /// </summary>
    Task<Result<bool, AeroError>> DeleteCascadeAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Deletes multiple pages by ID, optionally including descendants.
    /// </summary>
    Task<Result<int, AeroError>> DeleteMultipleAsync(IReadOnlyList<long> ids, bool deleteDescendants = false, CancellationToken ct = default);

    /// <summary>
    /// Deletes every localized variant in a translation group.
    /// </summary>
    Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken ct = default);

        /// <summary>
    /// PublishTranslationGroupAsync method.
    /// </summary>
Task<Result<PublicationBulkResult, AeroError>> PublishTranslationGroupAsync(long translationGroupId, CancellationToken ct = default);

        /// <summary>
    /// UnpublishTranslationGroupAsync method.
    /// </summary>
Task<Result<PublicationBulkResult, AeroError>> UnpublishTranslationGroupAsync(long translationGroupId, CancellationToken ct = default);

        /// <summary>
    /// TranslateWithAiAsync method.
    /// </summary>
Task<Result<AiTranslatePageResult, AeroError>> TranslateWithAiAsync(long id, AiTranslatePageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Publishes a page.
    /// </summary>
    /// <param name="id">The page identifier to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated page detail or an error.</returns>
    Task<Result<PageDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Unpublishes a page.
    /// </summary>
    Task<Result<PageDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Lists culture variants for a page.
    /// </summary>
    Task<Result<IReadOnlyList<PageDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Creates a draft culture variant for a page.
    /// </summary>
    Task<Result<PageDetail, AeroError>> ForkToCultureAsync(long id, ForkPageCultureRequest request, CancellationToken ct = default);

    // ── Tree / Hierarchy methods ──────────────────────────────────────

    /// <summary>
    /// Gets all pages as a flat, depth-ordered tree list for the current site.
    /// </summary>
    Task<Result<IReadOnlyList<PageTreeItem>, AeroError>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets immediate children of a parent page (or root-level pages).
    /// </summary>
    Task<Result<IReadOnlyList<PageTreeItem>, AeroError>> GetChildrenAsync(long? parentId, CancellationToken ct = default);

    /// <summary>
    /// Gets immediate translation-group children for the page manager tree.
    /// </summary>
    Task<Result<IReadOnlyList<PageTranslationGroupTreeItem>, AeroError>> GetTranslationGroupChildrenAsync(long? parentTranslationGroupId, string? culture = null, string? search = null, CancellationToken ct = default);

    /// <summary>
    /// Gets breadcrumb trail for a page.
    /// </summary>
    Task<Result<IReadOnlyList<TreeBreadcrumbItem>, AeroError>> GetBreadcrumbAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Computes the full path for a slug under a given parent. Validates uniqueness.
    /// Pass <paramref name="excludePageId"/> when editing so the page doesn't conflict with itself.
    /// </summary>
    Task<Result<ComputedPathResult, AeroError>> ComputePathAsync(long? parentId, string slug, long? excludePageId = null, CancellationToken ct = default);

}

/// <summary>
/// Typed client for pages endpoints.
/// </summary>
public class PagesHttpClient(HttpClient httpClient, ILogger<PagesHttpClient> logger) : AeroCmsClientBase(httpClient, logger), IPagesHttpClient
{
    /// <inheritdoc />
    public override string Path => "admin/pages";

    /// <inheritdoc />
    public Task<Result<PagedResult<PageSummary>, AeroError>> GetAllAsync(int skip = 0, int take = 20, string? search = null, CancellationToken ct = default)
    {
        var url = $"?skip={skip}&take={take}";
        if (!string.IsNullOrEmpty(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return GetAsync<PagedResult<PageSummary>>(url, ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<PageDetail>(id.ToString(), ct);
    }

    /// <inheritdoc />
    public Task<Result<PageSourceViewModel, AeroError>> GetSourceAsync(
        long id,
        CancellationToken ct = default)
        => GetAsync<PageSourceViewModel>($"{id}/source", ct);

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        return GetAsync<PageDetail>($"slug/{Uri.EscapeDataString(slug)}", ct);
    }

    /// <inheritdoc />
    public Task<Result<PagedResult<PageSummary>, AeroError>> GetPublishedAsync(int skip = 0, int take = 20, CancellationToken ct = default)
    {
        return GetAsync<PagedResult<PageSummary>>($"published?skip={skip}&take={take}", ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> CreateAsync(CreatePageRequest request, CancellationToken ct = default)
    {
        return PostAsync<CreatePageRequest, PageDetail>(string.Empty, request, ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> UpdateAsync(long id, UpdatePageRequest request, CancellationToken ct = default)
    {
        return PutAsync<UpdatePageRequest, PageDetail>(id.ToString(), request, ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<PageRegisteredFragmentDescriptor>, AeroError>> GetRegisteredFragmentsAsync(
        CancellationToken ct = default)
        => GetAsync<IReadOnlyList<PageRegisteredFragmentDescriptor>>("registered-fragments", ct);

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<PageRendererDescriptor>, AeroError>> GetRenderersAsync(
        CancellationToken ct = default)
        => GetAsync<IReadOnlyList<PageRendererDescriptor>>("renderers", ct);

    /// <inheritdoc />
    public Task<Result<PageRouteChangeImpact, AeroError>> GetRouteChangeImpactAsync(
        long id,
        PageRouteChangeRequest request,
        CancellationToken ct = default)
        => PostAsync<PageRouteChangeRequest, PageRouteChangeImpact>($"{id}/route-impact", request, ct);

    /// <inheritdoc />
    public Task<Result<bool, AeroError>> DeleteAsync(long id, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync(id.ToString(), ct));
    }

        /// <summary>
    /// DeleteCascadeAsync method.
    /// </summary>
public Task<Result<bool, AeroError>> DeleteCascadeAsync(long id, CancellationToken ct = default)
    {
        return MapBoolResult(base.DeleteAsync($"{id}/cascade", ct));
    }

        /// <summary>
    /// DeleteMultipleAsync method.
    /// </summary>
public async Task<Result<int, AeroError>> DeleteMultipleAsync(IReadOnlyList<long> ids, bool deleteDescendants = false, CancellationToken ct = default)
    {
        var result = await base.PostAsync<DeleteMultiplePagesRequest, DeleteMultipleResult>(
            "delete-multiple",
            new DeleteMultiplePagesRequest(ids, deleteDescendants),
            ct);

        return result switch
        {
            Result<DeleteMultipleResult, AeroError>.Ok ok => new Result<int, AeroError>.Ok(ok.Value.Deleted),
            Result<DeleteMultipleResult, AeroError>.Failure f => new Result<int, AeroError>.Failure(f.Error),
            _ => new Result<int, AeroError>.Failure(AeroError.CreateError("Bulk delete failed"))
        };
    }

        /// <summary>
    /// DeleteTranslationGroupAsync method.
    /// </summary>
public async Task<Result<int, AeroError>> DeleteTranslationGroupAsync(long translationGroupId, CancellationToken ct = default)
    {
        var result = await base.DeleteAsync($"translation-groups/{translationGroupId}", ct);
        return result switch
        {
            Result<HttpResponseMessage, AeroError>.Ok ok =>
                await ReadDeleteTranslationGroupResultAsync(ok.Value, ct),
            Result<HttpResponseMessage, AeroError>.Failure f => new Result<int, AeroError>.Failure(f.Error),
            _ => new Result<int, AeroError>.Failure(AeroError.CreateError("Translation group delete failed"))
        };
    }

    private static async Task<Result<int, AeroError>> ReadDeleteTranslationGroupResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<DeleteMultipleResult>(cancellationToken: ct);
            return new Result<int, AeroError>.Ok(body?.Deleted ?? 0);
        }
        catch
        {
            return new Result<int, AeroError>.Ok(0);
        }
    }

        /// <summary>
    /// PublishTranslationGroupAsync method.
    /// </summary>
public Task<Result<PublicationBulkResult, AeroError>> PublishTranslationGroupAsync(long translationGroupId, CancellationToken ct = default)
    {
        return PutAsync<object, PublicationBulkResult>($"translation-groups/{translationGroupId}/publish", new object(), ct);
    }

        /// <summary>
    /// UnpublishTranslationGroupAsync method.
    /// </summary>
public Task<Result<PublicationBulkResult, AeroError>> UnpublishTranslationGroupAsync(long translationGroupId, CancellationToken ct = default)
    {
        return PutAsync<object, PublicationBulkResult>($"translation-groups/{translationGroupId}/unpublish", new object(), ct);
    }

        /// <summary>
    /// TranslateWithAiAsync method.
    /// </summary>
public Task<Result<AiTranslatePageResult, AeroError>> TranslateWithAiAsync(long id, AiTranslatePageRequest request, CancellationToken ct = default)
    {
        return PostAsync<AiTranslatePageRequest, AiTranslatePageResult>($"{id}/ai-translate", request, ct);
    }

    private static async Task<Result<bool, AeroError>> MapBoolResult(Task<Result<HttpResponseMessage, AeroError>> task)
    {
        var response = await task;
        return response switch
        {
            Result<HttpResponseMessage, AeroError>.Ok => true,
            Result<HttpResponseMessage, AeroError>.Failure(var error) => error,
            _ => AeroError.CreateError("Unexpected result from HTTP operation")
        };
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> PublishAsync(long id, CancellationToken ct = default)
    {
        return PutAsync<object, PageDetail>($"{id}/publish", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> UnpublishAsync(long id, CancellationToken ct = default)
    {
        return PutAsync<object, PageDetail>($"{id}/unpublish", new object(), ct);
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<PageDetail>, AeroError>> ListCultureVariantsAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<PageDetail>>($"{id}/translations", ct);
    }

    /// <inheritdoc />
    public Task<Result<PageDetail, AeroError>> ForkToCultureAsync(long id, ForkPageCultureRequest request, CancellationToken ct = default)
    {
        return PostAsync<ForkPageCultureRequest, PageDetail>($"{id}/translations", request, ct);
    }

    // ── Tree / Hierarchy implementations ──────────────────────────────

        /// <summary>
    /// GetTreeAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<PageTreeItem>, AeroError>> GetTreeAsync(CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<PageTreeItem>>("tree", ct);
    }

        /// <summary>
    /// GetChildrenAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<PageTreeItem>, AeroError>> GetChildrenAsync(long? parentId, CancellationToken ct = default)
    {
        var url = "tree/children";
        if (parentId.HasValue) url += $"?parentId={parentId}";
        return GetAsync<IReadOnlyList<PageTreeItem>>(url, ct);
    }

        /// <summary>
    /// GetTranslationGroupChildrenAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<PageTranslationGroupTreeItem>, AeroError>> GetTranslationGroupChildrenAsync(
        long? parentTranslationGroupId,
        string? culture = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var parameters = new List<string>();
        if (parentTranslationGroupId.HasValue)
        {
            parameters.Add($"parentTranslationGroupId={parentTranslationGroupId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(culture))
        {
            parameters.Add($"culture={Uri.EscapeDataString(culture)}");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            parameters.Add($"search={Uri.EscapeDataString(search)}");
        }

        var url = "tree/translation-groups/children";
        if (parameters.Count > 0)
        {
            url += "?" + string.Join("&", parameters);
        }

        return GetAsync<IReadOnlyList<PageTranslationGroupTreeItem>>(url, ct);
    }

        /// <summary>
    /// GetBreadcrumbAsync method.
    /// </summary>
public Task<Result<IReadOnlyList<TreeBreadcrumbItem>, AeroError>> GetBreadcrumbAsync(long id, CancellationToken ct = default)
    {
        return GetAsync<IReadOnlyList<TreeBreadcrumbItem>>($"tree/breadcrumb/{id}", ct);
    }

        /// <summary>
    /// ComputePathAsync method.
    /// </summary>
public Task<Result<ComputedPathResult, AeroError>> ComputePathAsync(long? parentId, string slug, long? excludePageId = null, CancellationToken ct = default)
    {
        var url = $"tree/compute-path?slug={Uri.EscapeDataString(slug)}";
        if (parentId.HasValue) url += $"&parentId={parentId}";
        if (excludePageId.HasValue) url += $"&excludePageId={excludePageId}";
        return PostAsync<object, ComputedPathResult>(url, new {}, ct);
    }

}

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Summary information for a page.
/// </summary>
public record PageSummary(long Id, string Title, string Slug, DateTime CreatedAt, DateTime? PublishedAt, string? Excerpt);

/// <summary>
/// Detailed information for a page.
/// </summary>
public record PageDetail(
    long Id, 
    long SiteId,
    string Title, 
    string Slug, 
    string? Excerpt, 
    string? SeoTitle, 
    string? SeoDescription,
    DateTime CreatedAt, 
    DateTime UpdatedAt, 
    DateTime? PublishedAt, 
    ContentPublicationState PublicationState,
    int BlockCount,
    bool ShowInNavMenu,
    bool ShowHeaderNavigation,
    bool HideFooter,
    bool ShowChatAgent,
    long? ParentId = null,
    string Path = "",
    int Depth = 0,
    string Culture = "en-US",
    long? TranslationGroupId = null,
    HtmlPageContent? DraftContent = null,
    HtmlPageContent? PublishedContent = null,
    PageCompositionDocument? DraftComposition = null,
    PageCompositionDocument? PublishedComposition = null,
    string RendererId = PageRendererIds.AeroComposition,
    bool HasUnpublishedChanges = false);

/// <summary>
/// Request to create a new page.
/// </summary>
public record CreatePageRequest(
    string Title, 
    string Slug, 
    string? Summary, 
    string? SeoTitle, 
    string? SeoDescription, 
    ContentPublicationState PublicationState,
    long? ParentId = null,
    bool ShowInNavMenu = false, 
    bool ShowHeaderNavigation = true,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    HtmlPageContent? DraftContent = null,
    PageCompositionDocument? DraftComposition = null,
    string RendererId = PageRendererIds.AeroComposition,
    string? DraftSource = null);

/// <summary>
/// Request to update an existing page.
/// </summary>
public record UpdatePageRequest(
    string Title,
    string Slug,
    string? Summary,
    string? SeoTitle,
    string? SeoDescription,
    ContentPublicationState PublicationState,
    long? ParentId = null,
    bool ShowInNavMenu = false,
    bool ShowHeaderNavigation = true,
    bool HideFooter = false,
    bool ShowChatAgent = true,
    HtmlPageContent? DraftContent = null,
    PreviousPathBehavior? PreviousPathBehavior = null,
    PageCompositionDocument? DraftComposition = null,
    string RendererId = PageRendererIds.AeroComposition,
    string? DraftSource = null);

/// <summary>Proposed route inputs used to calculate redirect impact.</summary>
public sealed record PageRouteChangeRequest(string Slug, long? ParentId);

/// <summary>One historically published route affected by a proposed page route change.</summary>
[GenerateSerializer]
[Alias("PageRouteChangeItem")]
public sealed record PageRouteChangeItem(
    [property: Id(0)] long PageId,
    [property: Id(1)] string Title,
    [property: Id(2)] string Culture,
    [property: Id(3)] string OldPath,
    [property: Id(4)] string NewPath);

/// <summary>Server-computed impact of changing a page slug or parent.</summary>
[GenerateSerializer]
[Alias("PageRouteChangeImpact")]
public sealed record PageRouteChangeImpact(
    [property: Id(0)] long PageId,
    [property: Id(1)] string OldPath,
    [property: Id(2)] string NewPath,
    [property: Id(3)] IReadOnlyList<PageRouteChangeItem> PreviouslyPublishedRoutes,
    [property: Id(4)] string? ErrorMessage = null)
{
    /// <summary>Gets whether the proposed route differs from the current route.</summary>
    public bool RouteChanged => !string.Equals(OldPath, NewPath, StringComparison.Ordinal);

    /// <summary>Gets whether saving requires an explicit previous-path decision.</summary>
    public bool RequiresDecision => RouteChanged && PreviouslyPublishedRoutes.Count > 0;
}

/// <summary>
/// Request to create a draft culture variant for a page.
/// </summary>
public record ForkPageCultureRequest(
    string Culture,
    string Slug);

/// <summary>
/// Flat tree node model for page hierarchy display.
/// </summary>
public record PageTreeItem(
    long Id,
    string Title,
    string Slug,
    string Path,
    int Depth,
    int Order,
    long? ParentId,
    string PublicationState,
    bool IsHidden,
    bool HasChildren);

/// <summary>
/// Translation-group tree node for page hierarchy display.
/// </summary>
public sealed record PageTranslationGroupTreeItem(
    long TranslationGroupId,
    long DisplayPageId,
    string DisplayCulture,
    string DefaultCulture,
    string Title,
    string Slug,
    string Path,
    int Depth,
    int Order,
    long? ParentTranslationGroupId,
    string PublicationState,
    bool IsHidden,
    bool HasChildren,
    bool MissingDefaultCulture,
    bool MissingSelectedCulture,
    IReadOnlyList<PageTranslationVariantItem> Variants);

/// <summary>
/// Culture-specific variant under a translation-group page row.
/// </summary>
public sealed record PageTranslationVariantItem(
    long Id,
    string Culture,
    string Title,
    string Slug,
    string Path,
    string PublicationState,
    bool IsHidden,
    bool IsDefaultCulture);

/// <summary>
/// Single breadcrumb trail item.
/// </summary>
public record TreeBreadcrumbItem(
    long Id,
    string Title,
    string Slug,
    string Path);

/// <summary>
/// Result of slug + parent path validation.
/// </summary>
public record ComputedPathResult(
    string Path,
    int Depth,
    bool IsValid,
    string? ErrorMessage);

/// <summary>
/// Request body for bulk page deletion.
/// </summary>
public sealed record DeleteMultiplePagesRequest(
    IReadOnlyList<long> Ids,
    bool DeleteDescendants = false);

/// <summary>
/// Response from bulk page deletion.
/// </summary>
public sealed record DeleteMultipleResult(int Deleted);

/// <summary>
/// Represents a record for AiTranslatePageRequest.
/// </summary>
public sealed record AiTranslatePageRequest(
    IReadOnlyList<AiTranslatePageCultureRequest> Targets,
    string? ProviderId = null,
    bool OverwriteExisting = false);

/// <summary>
/// Represents a record for AiTranslatePageCultureRequest.
/// </summary>
public sealed record AiTranslatePageCultureRequest(
    string Culture,
    string? Slug = null);

/// <summary>
/// Represents a record for AiTranslatePageResult.
/// </summary>
public sealed record AiTranslatePageResult(
    IReadOnlyList<AiTranslatePageCultureResult> Results);

/// <summary>
/// Represents a record for AiTranslatePageCultureResult.
/// </summary>
public sealed record AiTranslatePageCultureResult(
    string Culture,
    bool Succeeded,
    PageDetail? Page,
    IReadOnlyList<string> Warnings,
    string? Error);
