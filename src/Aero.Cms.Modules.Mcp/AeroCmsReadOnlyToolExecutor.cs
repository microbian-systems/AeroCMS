using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Modules.Sites;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Mcp;

/// <summary>Executes the explicitly registered, site-scoped read-only CMS tools.</summary>
public sealed class AeroCmsReadOnlyToolExecutor(
    IAeroPageActor pageActor,
    ISiteLookupService siteLookupService) : IAeroCmsReadOnlyToolExecutor
{
    public const string CurrentSiteTool = "aero.cms.current_site";
    public const string PagesListTool = "aero.cms.pages.list";
    public const string PageGetTool = "aero.cms.page.get";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<AeroCmsReadOnlyToolDescriptor> Tools { get; } =
    [
        new(CurrentSiteTool, "Returns the authenticated manager's currently selected site."),
        new(PagesListTool, "Lists at most 25 page summaries from the authenticated manager's selected site."),
        new(PageGetTool, "Gets one page by positive identifier from the authenticated manager's selected site.")
    ];

    public async Task<Result<AeroCmsReadOnlyToolResult>> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var contextFailure = ValidateContext(context);
        if (contextFailure is not null)
            return contextFailure;

        return toolName switch
        {
            CurrentSiteTool => await CurrentSiteAsync(context, cancellationToken),
            PagesListTool => await ListPagesAsync(arguments, context, cancellationToken),
            PageGetTool => await GetPageAsync(arguments, context, cancellationToken),
            _ => AeroError.NotFoundError("The requested read-only tool is not registered.")
        };
    }

    private async Task<Result<AeroCmsReadOnlyToolResult>> CurrentSiteAsync(
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sites = await siteLookupService.GetAllAsync(cancellationToken);
        var site = sites.SingleOrDefault(candidate =>
            candidate.Id == context.SiteId && candidate.TenantId == context.TenantId);
        if (site is null)
            return AeroError.ForbiddenError("The selected site is unavailable.");

        return Serialize(new
        {
            siteId = site.Id,
            tenantId = site.TenantId,
            site.Name,
            site.PrimaryHost,
            site.DefaultCulture,
            site.SupportedCultures
        });
    }

    private async Task<Result<AeroCmsReadOnlyToolResult>> ListPagesAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var take = ReadInt(arguments, "take", 10);
        var skip = ReadInt(arguments, "skip", 0);
        if (take is < 1 or > 25)
            return AeroError.ValidationError(["take must be between 1 and 25."]);
        if (skip is < 0 or > 100_000)
            return AeroError.ValidationError(["skip must be between 0 and 100000."]);

        var (items, totalCount) = await pageActor.GetAllPagesAsync(
            context.SiteId,
            skip,
            take,
            search: null,
            cancellationToken);
        if (items.Any(page => page.SiteId != context.SiteId))
            return AeroError.ForbiddenError("The page result did not match the selected site.");

        return Serialize(new
        {
            totalCount,
            skip,
            take,
            items = items.Select(PageSummary)
        });
    }

    private async Task<Result<AeroCmsReadOnlyToolResult>> GetPageAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var id = ReadLong(arguments, "id", 0);
        if (id <= 0)
            return AeroError.ValidationError(["id must be a positive page identifier."]);

        var response = await pageActor.GetByIdAsync(id, context.SiteId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(response.error.Message) ||
            response.data.Id <= 0 ||
            response.data.SiteId != context.SiteId)
        {
            return AeroError.NotFoundError("Page was not found in the selected site.");
        }

        var content = response.data.Content ?? string.Empty;
        var truncated = content.Length > 50_000;
        if (truncated)
            content = content[..50_000];

        return Serialize(new
        {
            page = PageSummary(response.data),
            response.data.Summary,
            response.data.SeoTitle,
            response.data.SeoDescription,
            content,
            contentTruncated = truncated
        });
    }

    private static object PageSummary(PageViewModel page) => new
    {
        page.Id,
        page.Title,
        page.Slug,
        page.Path,
        page.ParentId,
        page.Culture,
        page.PublicationState,
        page.IsPublished,
        page.PublishedOn,
        page.ModifiedOn
    };

    private static AeroError? ValidateContext(AeroCmsToolExecutionContext context)
    {
        if (context.Principal.Identity?.IsAuthenticated != true)
            return AeroError.UnauthorizedError("Authentication is required.");
        if (context.UserId <= 0 || context.SiteId <= 0 || context.TenantId <= 0)
            return AeroError.ForbiddenError("A valid user, site, and tenant context is required.");
        if (string.IsNullOrWhiteSpace(context.CorrelationId))
            return AeroError.InvalidRequestError("A correlation context is required.");
        return null;
    }

    private static int ReadInt(JsonElement arguments, string name, int fallback)
        => arguments.ValueKind == JsonValueKind.Object &&
           arguments.TryGetProperty(name, out var value) &&
           value.TryGetInt32(out var result)
            ? result
            : fallback;

    private static long ReadLong(JsonElement arguments, string name, long fallback)
        => arguments.ValueKind == JsonValueKind.Object &&
           arguments.TryGetProperty(name, out var value) &&
           value.TryGetInt64(out var result)
            ? result
            : fallback;

    private static AeroCmsReadOnlyToolResult Serialize(object value)
        => new(JsonSerializer.Serialize(value, JsonOptions));
}
