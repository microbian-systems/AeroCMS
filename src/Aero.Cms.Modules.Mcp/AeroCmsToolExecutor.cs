using System.Globalization;
using System.Net;
using System.Text.Json;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Ai.Assistant;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Serialization;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Models;
using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Cms.Abstractions.Requests;
using Aero.Cms.Abstractions.Security;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.RateLimiting;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Authorization;

namespace Aero.Cms.Modules.Mcp;

/// <summary>
/// Executes the explicitly registered, site-scoped AeroCMS tools used by MCP and the manager AI.
/// </summary>
public sealed class AeroCmsToolExecutor(
    IAeroPageActor pageActor,
    IAeroPostActor postActor,
    IAeroDocsActor docsActor,
    IAeroContentTypeActor contentTypeActor,
    IAeroContentItemActor contentItemActor,
    IContentHierarchyQueryService hierarchyQueryService,
    ISiteLookupService siteLookupService,
    IAuthorizationService authorizationService,
    IAeroApplicationRateLimiter rateLimiter) : IAeroCmsToolExecutor
{
    public const string CurrentSiteTool = "aero.cms.current_site";
    public const string PagesListTool = "aero.cms.pages.list";
    public const string PageGetTool = "aero.cms.page.get";
    public const string PageCreateTool = "aero.cms.page.create";
    public const string PostsListTool = "aero.cms.posts.list";
    public const string PostGetTool = "aero.cms.post.get";
    public const string PostCreateTool = "aero.cms.post.create";
    public const string DocsListTool = "aero.cms.docs.list";
    public const string DocGetTool = "aero.cms.doc.get";
    public const string DocCreateTool = "aero.cms.doc.create";
    public const string ContentTypesListTool = "aero.cms.content_types.list";
    public const string ContentTypeGetTool = "aero.cms.content_type.get";
    public const string ContentTypeCreateTool = "aero.cms.content_type.create";
    public const string ContentItemsListTool = "aero.cms.content_items.list";
    public const string ContentItemGetTool = "aero.cms.content_item.get";
    public const string ContentItemCreateTool = "aero.cms.content_item.create";
    public const string ContentHierarchyGetTool = "aero.cms.content_hierarchy.get";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<AeroCmsToolDescriptor> Tools { get; } =
    [
        Read(CurrentSiteTool, "Returns the authenticated manager's currently selected site.", AeroApiKeyPermissionDomains.Sites),
        Read(PagesListTool, "Lists a bounded page of AeroCMS page summaries.", AeroApiKeyPermissionDomains.Pages),
        Read(PageGetTool, "Gets one page from the selected site.", AeroApiKeyPermissionDomains.Pages),
        Create(PageCreateTool, "Creates one draft page in the selected site.", AeroApiKeyPermissionDomains.Pages),
        Read(PostsListTool, "Lists a bounded page of blog post summaries.", AeroApiKeyPermissionDomains.Posts),
        Read(PostGetTool, "Gets one blog post from the selected site.", AeroApiKeyPermissionDomains.Posts),
        Create(PostCreateTool, "Creates one draft blog post in the selected site.", AeroApiKeyPermissionDomains.Posts),
        Read(DocsListTool, "Lists a bounded page of documentation entries.", AeroApiKeyPermissionDomains.Docs),
        Read(DocGetTool, "Gets one documentation entry from the selected site.", AeroApiKeyPermissionDomains.Docs),
        Create(DocCreateTool, "Creates one draft documentation entry in the selected site.", AeroApiKeyPermissionDomains.Docs),
        Read(ContentTypesListTool, "Lists a bounded page of content-type definitions.", AeroApiKeyPermissionDomains.ContentTypes),
        Read(ContentTypeGetTool, "Gets one content-type definition by alias.", AeroApiKeyPermissionDomains.ContentTypes),
        Create(ContentTypeCreateTool, "Creates one content-type definition, including hierarchy settings.", AeroApiKeyPermissionDomains.ContentTypes),
        Read(ContentItemsListTool, "Lists a bounded page of items for one content type.", AeroApiKeyPermissionDomains.ContentItems),
        Read(ContentItemGetTool, "Gets one item from one content type.", AeroApiKeyPermissionDomains.ContentItems),
        Create(ContentItemCreateTool, "Creates one draft item for one content type.", AeroApiKeyPermissionDomains.ContentItems),
        Read(ContentHierarchyGetTool, "Returns an immutable, bounded hierarchy projection.", AeroApiKeyPermissionDomains.ContentItems)
    ];

    public async Task<Result<IReadOnlyList<AeroCmsToolDescriptor>>> GetAuthorizedToolsAsync(
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var contextFailure = ValidateContext(context);
        if (contextFailure is not null)
            return contextFailure;

        var authorized = new List<AeroCmsToolDescriptor>(Tools.Count);
        foreach (var descriptor in Tools)
        {
            if (await IsAuthorizedAsync(descriptor, context, cancellationToken))
                authorized.Add(descriptor);
        }

        return authorized;
    }

    public async Task<Result<AeroCmsToolResult>> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var contextFailure = ValidateContext(context);
        if (contextFailure is not null)
            return contextFailure;

        var descriptor = Tools.SingleOrDefault(tool =>
            string.Equals(tool.Name, toolName, StringComparison.Ordinal));
        if (descriptor is null)
            return AeroError.NotFoundError("The requested CMS tool is not registered.");

        if (!await IsAuthorizedAsync(descriptor, context, cancellationToken))
            return AeroError.ForbiddenError("The current credential is not authorized for this CMS operation.");

        var rateLimitPolicy = descriptor.Destructive
            ? AeroRateLimitPolicyNames.McpDestructive
            : descriptor.ReadOnly
                ? AeroRateLimitPolicyNames.McpRead
                : AeroRateLimitPolicyNames.McpWrite;
        var apiKeyId = FirstClaim(
            context.Principal,
            AeroApiKeyClaimTypes.KeyId,
            "api_key_id",
            "api-key-id",
            "key_id");
        var admission = await rateLimiter.AcquireAsync(
            rateLimitPolicy,
            new AeroRateLimitSubject(
                context.TenantId,
                context.SiteId,
                "cms-tools",
                apiKeyId is null ? "principal" : "api-key",
                apiKeyId ?? context.UserId.ToString(CultureInfo.InvariantCulture)),
            cancellationToken);
        if (!admission.IsAcquired)
        {
            var retry = admission.RetryAfter is { } retryAfter
                ? $" Retry after {Math.Clamp((int)Math.Ceiling(retryAfter.TotalSeconds), 1, 86_400)} seconds."
                : string.Empty;
            return AeroError.HttpRequestError(
                HttpStatusCode.TooManyRequests,
                $"CMS tool request limit exceeded.{retry}");
        }

        return toolName switch
        {
            CurrentSiteTool => await CurrentSiteAsync(context, cancellationToken),
            PagesListTool => await ListPagesAsync(arguments, context, cancellationToken),
            PageGetTool => await GetPageAsync(arguments, context, cancellationToken),
            PageCreateTool => await CreatePageAsync(arguments, context, cancellationToken),
            PostsListTool => await ListPostsAsync(arguments, context, cancellationToken),
            PostGetTool => await GetPostAsync(arguments, context, cancellationToken),
            PostCreateTool => await CreatePostAsync(arguments, context, cancellationToken),
            DocsListTool => await ListDocsAsync(arguments, context, cancellationToken),
            DocGetTool => await GetDocAsync(arguments, context, cancellationToken),
            DocCreateTool => await CreateDocAsync(arguments, context, cancellationToken),
            ContentTypesListTool => await ListContentTypesAsync(arguments, context, cancellationToken),
            ContentTypeGetTool => await GetContentTypeAsync(arguments, context, cancellationToken),
            ContentTypeCreateTool => await CreateContentTypeAsync(arguments, context, cancellationToken),
            ContentItemsListTool => await ListContentItemsAsync(arguments, context, cancellationToken),
            ContentItemGetTool => await GetContentItemAsync(arguments, context, cancellationToken),
            ContentItemCreateTool => await CreateContentItemAsync(arguments, context, cancellationToken),
            ContentHierarchyGetTool => await GetContentHierarchyAsync(arguments, context, cancellationToken),
            _ => AeroError.NotFoundError("The requested CMS tool is not registered.")
        };
    }

    private async Task<bool> IsAuthorizedAsync(
        AeroCmsToolDescriptor descriptor,
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsApiKeyPrincipal(context.Principal))
            return HasApiKeyCapability(context, descriptor);

        var authorization = await authorizationService.AuthorizeAsync(
            context.Principal,
            resource: null,
            descriptor.RequiredPolicy);
        return authorization.Succeeded;
    }

    private static string? FirstClaim(
        System.Security.Claims.ClaimsPrincipal principal,
        params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool IsApiKeyPrincipal(System.Security.Claims.ClaimsPrincipal principal) =>
        principal.HasClaim(claim =>
            string.Equals(claim.Type, AeroApiKeyClaimTypes.KeyId, StringComparison.Ordinal));

    private static bool HasApiKeyCapability(
        AeroCmsToolExecutionContext context,
        AeroCmsToolDescriptor descriptor)
    {
        var principal = context.Principal;
        if (!principal.HasClaim(AeroApiKeyClaimTypes.McpServer, "true") ||
            !principal.HasClaim(AeroApiKeyClaimTypes.TenantId, context.TenantId.ToString(CultureInfo.InvariantCulture)) ||
            !principal.HasClaim(AeroApiKeyClaimTypes.SiteId, context.SiteId.ToString(CultureInfo.InvariantCulture)))
        {
            return false;
        }

        if (principal.HasClaim(AeroApiKeyClaimTypes.Administrator, "true"))
            return true;

        return principal.FindAll(AeroApiKeyClaimTypes.Permission)
            .Any(claim => PermissionAllows(
                claim.Value,
                descriptor.PermissionDomain,
                descriptor.PermissionOperation));
    }

    private static bool PermissionAllows(string value, string domain, char operation)
    {
        var separator = value.IndexOf(':');
        return separator > 0 &&
               string.Equals(value[..separator], domain, StringComparison.Ordinal) &&
               value.AsSpan(separator + 1).Contains(operation);
    }

    private async Task<Result<AeroCmsToolResult>> CurrentSiteAsync(
        AeroCmsToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sites = await siteLookupService.GetAllAsync(cancellationToken);
        var site = sites.SingleOrDefault(candidate =>
            candidate.Id == context.SiteId && candidate.TenantId == context.TenantId);
        return site is null
            ? AeroError.ForbiddenError("The selected site is unavailable.")
            : Serialize(new
            {
                siteId = Id(site.Id),
                tenantId = Id(site.TenantId),
                site.Name,
                site.PrimaryHost,
                site.DefaultCulture,
                site.SupportedCultures
            });
    }

    private async Task<Result<AeroCmsToolResult>> ListPagesAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var bounds = ReadBounds(arguments);
        if (bounds is Result<(int Skip, int Take), AeroError>.Failure failure)
            return failure.Error;
        var (skip, take) = ((Result<(int Skip, int Take), AeroError>.Ok)bounds).Value;
        var search = ReadOptionalString(arguments, "search");
        var (items, totalCount) = await pageActor.GetAllPagesAsync(
            context.SiteId,
            skip,
            take,
            search,
            ct);
        if (items.Any(page => page.SiteId != context.SiteId))
            return AeroError.ForbiddenError("The page result did not match the selected site.");
        return SerializePage(items.Select(PageSummary), totalCount, skip, take);
    }

    private async Task<Result<AeroCmsToolResult>> GetPageAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var id = ReadId(arguments, "id");
        if (id <= 0)
            return PositiveId("id", "page");
        var response = await pageActor.GetByIdAsync(id, context.SiteId, ct);
        if (HasError(response.error.Message)
            || response.data.Id != id
            || response.data.SiteId != context.SiteId)
            return AeroError.NotFoundError("Page was not found in the selected site.");
        return Serialize(new
        {
            page = PageSummary(response.data),
            response.data.Summary,
            response.data.SeoTitle,
            response.data.SeoDescription,
            content = BoundText(response.data.Content, out var truncated),
            contentTruncated = truncated,
            response.data.RendererId
        });
    }

    private async Task<Result<AeroCmsToolResult>> CreatePageAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var title = ReadRequiredString(arguments, "title");
        var slug = ReadRequiredString(arguments, "slug");
        if (title is null || slug is null)
            return Required("title and slug are required.");
        var rendererId = PageRendererIds.NormalizeOrDefault(
            ReadOptionalString(arguments, "rendererId"));
        var request = new CreatePageRequest(
            title,
            slug,
            ReadOptionalString(arguments, "summary"),
            ReadOptionalString(arguments, "seoTitle"),
            ReadOptionalString(arguments, "seoDescription"),
            ParentId: ReadNullableId(arguments, "parentId"),
            ShowInNavMenu: ReadBool(arguments, "showInNavigation"),
            SiteId: context.SiteId,
            RendererId: rendererId,
            DraftSource: ReadOptionalRawString(arguments, "source"));
        var result = await pageActor.CreateAsync(request, ct);
        return HasError(result.error.Message) || result.data.SiteId != context.SiteId
            ? AeroError.ValidationError([SafeActorError(result.error.Message, "Page could not be created.")])
            : Serialize(new { page = PageSummary(result.data) });
    }

    private async Task<Result<AeroCmsToolResult>> ListPostsAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var bounds = ReadBounds(arguments);
        if (bounds is Result<(int Skip, int Take), AeroError>.Failure failure)
            return failure.Error;
        var (skip, take) = ((Result<(int Skip, int Take), AeroError>.Ok)bounds).Value;
        var (items, totalCount) = await postActor.GetAllPostsAsync(
            context.SiteId,
            skip,
            take,
            ReadOptionalString(arguments, "search"),
            ct);
        if (items.Any(post => post.SiteId != context.SiteId))
            return AeroError.ForbiddenError("The post result did not match the selected site.");
        return SerializePage(items.Select(PostSummary), totalCount, skip, take);
    }

    private async Task<Result<AeroCmsToolResult>> GetPostAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var id = ReadId(arguments, "id");
        if (id <= 0)
            return PositiveId("id", "post");
        var response = await postActor.GetByIdAsync(id, context.SiteId, ct);
        if (HasError(response.error.Message)
            || response.data.Id != id
            || response.data.SiteId != context.SiteId)
            return AeroError.NotFoundError("Post was not found in the selected site.");
        return Serialize(new
        {
            post = PostSummary(response.data),
            response.data.Excerpt,
            response.data.SeoTitle,
            response.data.SeoDescription,
            markdown = BoundText(response.data.MarkdownContent, out var truncated),
            markdownTruncated = truncated,
            response.data.ImageUrl
        });
    }

    private async Task<Result<AeroCmsToolResult>> CreatePostAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var title = ReadRequiredString(arguments, "title");
        var slug = ReadRequiredString(arguments, "slug");
        if (title is null || slug is null)
            return Required("title and slug are required.");
        var vm = new PostViewModel
        {
            Id = Snowflake.NewId(),
            SiteId = context.SiteId,
            Title = title,
            Slug = slug,
            Excerpt = ReadOptionalString(arguments, "excerpt"),
            MarkdownContent = ReadOptionalRawString(arguments, "markdown") ?? string.Empty,
            Culture = NormalizeCulture(ReadOptionalString(arguments, "culture")),
            CreatedOn = DateTimeOffset.UtcNow,
            CreatedBy = $"assistant:{context.UserId}",
            ModifiedBy = $"assistant:{context.UserId}"
        };
        var result = await postActor.SavePostAsync(vm, context.SiteId, ct);
        return HasError(result.error.Message) || result.data.SiteId != context.SiteId
            ? AeroError.ValidationError([SafeActorError(result.error.Message, "Post could not be created.")])
            : Serialize(new { post = PostSummary(result.data) });
    }

    private async Task<Result<AeroCmsToolResult>> ListDocsAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var bounds = ReadBounds(arguments);
        if (bounds is Result<(int Skip, int Take), AeroError>.Failure failure)
            return failure.Error;
        var (skip, take) = ((Result<(int Skip, int Take), AeroError>.Ok)bounds).Value;
        var search = ReadOptionalString(arguments, "search");
        var docs = await docsActor.GetAllBySiteAsync(context.SiteId, ct);
        if (docs.Any(doc => doc.SiteId != context.SiteId))
            return AeroError.ForbiddenError("The docs result did not match the selected site.");
        var filtered = string.IsNullOrWhiteSpace(search)
            ? docs
            : docs.Where(doc =>
                (doc.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (doc.Slug?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        return SerializePage(
            filtered.Skip(skip).Take(take).Select(DocSummary),
            filtered.Count,
            skip,
            take);
    }

    private async Task<Result<AeroCmsToolResult>> GetDocAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var id = ReadId(arguments, "id");
        if (id <= 0)
            return PositiveId("id", "doc");
        var response = await docsActor.GetByIdAsync(id, context.SiteId, ct);
        if (HasError(response.error.Message)
            || response.data.Id != id
            || response.data.SiteId != context.SiteId)
            return AeroError.NotFoundError("Doc was not found in the selected site.");
        return Serialize(new
        {
            doc = DocSummary(response.data),
            response.data.Summary,
            response.data.SeoTitle,
            response.data.SeoDescription,
            markdown = BoundText(response.data.MarkdownContent, out var truncated),
            markdownTruncated = truncated
        });
    }

    private async Task<Result<AeroCmsToolResult>> CreateDocAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var title = ReadRequiredString(arguments, "title");
        var slug = ReadRequiredString(arguments, "slug");
        if (title is null || slug is null)
            return Required("title and slug are required.");
        var vm = new DocViewModel
        {
            SiteId = context.SiteId,
            Title = title,
            Slug = slug,
            Summary = ReadOptionalString(arguments, "summary"),
            MarkdownContent = ReadOptionalRawString(arguments, "markdown"),
            Culture = NormalizeCulture(ReadOptionalString(arguments, "culture")),
            ParentId = ReadNullableId(arguments, "parentId"),
            CreatedOn = DateTimeOffset.UtcNow,
            CreatedBy = $"assistant:{context.UserId}",
            ModifiedBy = $"assistant:{context.UserId}"
        };
        var result = await docsActor.SaveAsync(vm, context.SiteId, ct);
        return HasError(result.error.Message) || result.data.SiteId != context.SiteId
            ? AeroError.ValidationError([SafeActorError(result.error.Message, "Doc could not be created.")])
            : Serialize(new { doc = DocSummary(result.data) });
    }

    private async Task<Result<AeroCmsToolResult>> ListContentTypesAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var bounds = ReadBounds(arguments);
        if (bounds is Result<(int Skip, int Take), AeroError>.Failure failure)
            return failure.Error;
        var (skip, take) = ((Result<(int Skip, int Take), AeroError>.Ok)bounds).Value;
        var types = await contentTypeActor.GetAllAsync(context.SiteId, ct);
        if (types.Any(type => type.SiteId != context.SiteId))
            return AeroError.ForbiddenError("The content-type result did not match the selected site.");
        return SerializePage(
            types.Skip(skip).Take(take).Select(ContentTypeSummary),
            types.Count,
            skip,
            take);
    }

    private async Task<Result<AeroCmsToolResult>> GetContentTypeAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var alias = ReadRequiredString(arguments, "alias");
        if (alias is null)
            return Required("alias is required.");
        var type = await contentTypeActor.GetByAliasAsync(context.SiteId, alias, ct);
        return type is null || type.SiteId != context.SiteId
            ? AeroError.NotFoundError("Content type was not found in the selected site.")
            : Serialize(new { contentType = ContentTypeDetail(type) });
    }

    private async Task<Result<AeroCmsToolResult>> CreateContentTypeAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var alias = ReadRequiredString(arguments, "alias");
        var name = ReadRequiredString(arguments, "name");
        var fieldsJson = ReadOptionalRawString(arguments, "fieldsJson") ?? "[]";
        if (alias is null || name is null)
            return Required("alias and name are required.");
        if (!TryParseJson(fieldsJson, JsonValueKind.Array, out _))
            return Required("fieldsJson must be a JSON array of content-field definitions.");

        var structure = ReadEnum(arguments, "structure", ContentStructure.Flat);
        var maximumDepth = Math.Clamp(ReadInt(arguments, "maximumDepth", 8), 1, 32);
        var vm = new ContentTypeViewModel
        {
            SiteId = context.SiteId,
            Alias = alias,
            Name = name,
            Description = ReadOptionalString(arguments, "description"),
            Category = ReadOptionalString(arguments, "category"),
            FieldsJson = fieldsJson,
            Cardinality = ContentCardinality.Collection,
            Structure = structure,
            HierarchyRules = new ContentHierarchyRules
            {
                MaximumDepth = maximumDepth,
                AllowRootItems = true,
                RequireSameTypeParent = true
            }
        };
        var result = await contentTypeActor.CreateAsync(vm, context.SiteId, ct);
        return HasError(result.error.Message) || result.data.SiteId != context.SiteId
            ? AeroError.ValidationError([SafeActorError(result.error.Message, "Content type could not be created.")])
            : Serialize(new { contentType = ContentTypeDetail(result.data) });
    }

    private async Task<Result<AeroCmsToolResult>> ListContentItemsAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var alias = ReadRequiredString(arguments, "alias");
        if (alias is null)
            return Required("alias is required.");
        var bounds = ReadBounds(arguments);
        if (bounds is Result<(int Skip, int Take), AeroError>.Failure failure)
            return failure.Error;
        var (skip, take) = ((Result<(int Skip, int Take), AeroError>.Ok)bounds).Value;
        var (items, totalCount) = await contentItemActor.GetByTypeAsync(
            context.SiteId,
            alias,
            skip,
            take,
            ct);
        if (items.Any(item =>
            item.SiteId != context.SiteId
            || !string.Equals(item.ContentTypeAlias, alias, StringComparison.Ordinal)))
            return AeroError.ForbiddenError("The content-item result did not match the selected site and type.");
        return SerializePage(items.Select(ContentItemSummary), totalCount, skip, take);
    }

    private async Task<Result<AeroCmsToolResult>> GetContentItemAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var alias = ReadRequiredString(arguments, "alias");
        var id = ReadId(arguments, "id");
        if (alias is null || id <= 0)
            return Required("alias and a positive id are required.");
        var response = await contentItemActor.GetByIdAsync(id, context.SiteId, ct);
        if (HasError(response.error.Message)
            || response.data.Id != id
            || response.data.SiteId != context.SiteId
            || !string.Equals(response.data.ContentTypeAlias, alias, StringComparison.Ordinal))
            return AeroError.NotFoundError("Content item was not found in the selected site and type.");
        return Serialize(new { contentItem = ContentItemDetail(response.data) });
    }

    private async Task<Result<AeroCmsToolResult>> CreateContentItemAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var alias = ReadRequiredString(arguments, "alias");
        var title = ReadRequiredString(arguments, "title");
        var slug = ReadRequiredString(arguments, "slug");
        var fieldsJson = ReadOptionalRawString(arguments, "fieldsJson") ?? "{}";
        if (alias is null || title is null || slug is null)
            return Required("alias, title, and slug are required.");
        if (!TryParseJson(fieldsJson, JsonValueKind.Object, out _))
            return Required("fieldsJson must be a JSON object keyed by content-field name.");
        var vm = new ContentItemViewModel
        {
            SiteId = context.SiteId,
            ContentTypeAlias = alias,
            Title = title,
            Slug = slug,
            FieldsJson = fieldsJson,
            Culture = NormalizeCulture(ReadOptionalString(arguments, "culture")),
            ParentId = ReadNullableId(arguments, "parentId"),
            SortOrder = ReadInt(arguments, "sortOrder", 0)
        };
        var result = await contentItemActor.SaveDraftAsync(vm, context.SiteId, ct);
        return HasError(result.error.Message)
               || result.data.SiteId != context.SiteId
               || !string.Equals(result.data.ContentTypeAlias, alias, StringComparison.Ordinal)
            ? AeroError.ValidationError([SafeActorError(result.error.Message, "Content item could not be created.")])
            : Serialize(new { contentItem = ContentItemDetail(result.data) });
    }

    private async Task<Result<AeroCmsToolResult>> GetContentHierarchyAsync(
        JsonElement arguments,
        AeroCmsToolExecutionContext context,
        CancellationToken ct)
    {
        var alias = ReadRequiredString(arguments, "alias");
        if (alias is null)
            return Required("alias is required.");
        var type = await contentTypeActor.GetByAliasAsync(context.SiteId, alias, ct);
        if (type is null || type.SiteId != context.SiteId)
            return AeroError.NotFoundError("Content type was not found in the selected site.");
        var traversal = ReadEnum(
            arguments,
            "traversal",
            ContentTraversal.RootsWithDescendants);
        var request = new ContentQueryRequest(
            "hierarchy",
            context.SiteId,
            type.Id,
            alias,
            NormalizeCulture(ReadOptionalString(arguments, "culture")),
            traversal,
            ReadNullableId(arguments, "rootId"),
            Math.Clamp(ReadInt(arguments, "maximumDepth", 6), 1, 16),
            Math.Clamp(ReadInt(arguments, "maximumItems", 100), 1, 500),
            IncludeDrafts: ReadBool(arguments, "includeDrafts"));
        var result = await hierarchyQueryService.QueryAsync(request, ct);
        return result switch
        {
            Result<ContentQueryResult>.Ok ok => Serialize(ok.Value),
            Result<ContentQueryResult>.Failure failure => failure.Error,
            _ => AeroError.CreateError("Content hierarchy query failed.")
        };
    }

    private static AeroCmsToolDescriptor Read(
        string name,
        string description,
        string domain) =>
        new(name, description, "site:read", domain, 'R', true, false, true);

    private static AeroCmsToolDescriptor Create(
        string name,
        string description,
        string domain) =>
        new(name, description, "site:create", domain, 'C', false, false, false);

    private static object PageSummary(PageViewModel page) => new
    {
        id = Id(page.Id),
        page.Title,
        page.Slug,
        page.Path,
        parentId = NullableId(page.ParentId),
        page.Culture,
        publicationState = page.PublicationState.ToString(),
        page.IsPublished,
        page.PublishedOn,
        page.ModifiedOn
    };

    private static object PostSummary(PostViewModel post) => new
    {
        id = Id(post.Id),
        post.Title,
        post.Slug,
        post.Culture,
        publicationState = post.PublicationState.ToString(),
        post.PublishedOn,
        post.ModifiedOn
    };

    private static object DocSummary(DocViewModel doc) => new
    {
        id = Id(doc.Id),
        doc.Title,
        doc.Slug,
        parentId = NullableId(doc.ParentId),
        doc.Order,
        doc.Culture,
        publicationState = doc.PublicationState.ToString(),
        doc.PublishedOn,
        doc.ModifiedOn
    };

    private static object ContentTypeSummary(ContentTypeViewModel type) => new
    {
        id = Id(type.Id),
        type.Alias,
        type.Name,
        type.Description,
        type.Category,
        cardinality = type.Cardinality.ToString(),
        structure = type.Structure.ToString()
    };

    private static object ContentTypeDetail(ContentTypeViewModel type) => new
    {
        summary = ContentTypeSummary(type),
        type.FieldsJson,
        type.AllowPublicUrl,
        type.IncludeInSearch,
        type.IncludeInPublicAi,
        type.HierarchyRules,
        type.ScribanTemplate
    };

    private static object ContentItemSummary(ContentItemViewModel item) => new
    {
        id = Id(item.Id),
        item.Title,
        item.Slug,
        item.ContentTypeAlias,
        item.Culture,
        parentId = NullableId(item.ParentId),
        item.SortOrder,
        publicationState = item.PublicationState.ToString(),
        item.PublishedOn,
        item.VersionNumber
    };

    private static object ContentItemDetail(ContentItemViewModel item) => new
    {
        summary = ContentItemSummary(item),
        item.FieldsJson,
        translationGroupId = NullableId(item.TranslationGroupId),
        sourceItemId = NullableId(item.SourceItemId)
    };

    private static Result<AeroCmsToolResult> SerializePage(
        IEnumerable<object> items,
        long totalCount,
        int skip,
        int take) =>
        Serialize(new { totalCount, skip, take, items });

    private static Result<AeroCmsToolResult> Serialize(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return json.Length > 1_000_000
            ? AeroError.ValidationError(["The tool result exceeded the one-megabyte output bound."])
            : new AeroCmsToolResult(json);
    }

    private static Result<(int Skip, int Take), AeroError> ReadBounds(JsonElement arguments)
    {
        var take = ReadInt(arguments, "take", 10);
        var skip = ReadInt(arguments, "skip", 0);
        if (take is < 1 or > 25)
            return AeroError.ValidationError(["take must be between 1 and 25."]);
        if (skip is < 0 or > 100_000)
            return AeroError.ValidationError(["skip must be between 0 and 100000."]);
        return (skip, take);
    }

    private static int ReadInt(JsonElement arguments, string name, int fallback) =>
        TryProperty(arguments, name, out var value) && value.TryGetInt32(out var result)
            ? result
            : fallback;

    private static long ReadId(JsonElement arguments, string name)
    {
        if (!TryProperty(arguments, name, out var value))
            return 0;
        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var stringId))
            return stringId;
        return value.TryGetInt64(out var numericId) ? numericId : 0;
    }

    private static long? ReadNullableId(JsonElement arguments, string name)
    {
        if (!TryProperty(arguments, name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        var id = ReadId(arguments, name);
        return id > 0 ? id : null;
    }

    private static string? ReadRequiredString(JsonElement arguments, string name)
    {
        var value = ReadOptionalString(arguments, name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadOptionalString(JsonElement arguments, string name) =>
        TryProperty(arguments, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;

    private static string? ReadOptionalRawString(JsonElement arguments, string name) =>
        TryProperty(arguments, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBool(JsonElement arguments, string name) =>
        TryProperty(arguments, name, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static TEnum ReadEnum<TEnum>(
        JsonElement arguments,
        string name,
        TEnum fallback)
        where TEnum : struct, Enum
    {
        var raw = ReadOptionalString(arguments, name);
        return Enum.TryParse<TEnum>(raw, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
                ? parsed
                : fallback;
    }

    private static bool TryProperty(
        JsonElement arguments,
        string name,
        out JsonElement value)
    {
        value = default;
        return arguments.ValueKind == JsonValueKind.Object
               && arguments.TryGetProperty(name, out value);
    }

    private static bool TryParseJson(
        string json,
        JsonValueKind expectedKind,
        out JsonElement value)
    {
        try
        {
            value = JsonSerializer.Deserialize<JsonElement>(json, ContentJsonContext.Default.Options);
            return value.ValueKind == expectedKind;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }

    private static string NormalizeCulture(string? culture) =>
        string.IsNullOrWhiteSpace(culture)
            ? "en-US"
            : CultureInfo.GetCultureInfo(culture).Name;

    private static string? BoundText(string? value, out bool truncated)
    {
        value ??= string.Empty;
        truncated = value.Length > 50_000;
        return truncated ? value[..50_000] : value;
    }

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);
    private static string? NullableId(long? id) => id?.ToString(CultureInfo.InvariantCulture);
    private static bool HasError(string? message) => !string.IsNullOrWhiteSpace(message);
    private static string SafeActorError(string? message, string fallback) =>
        string.IsNullOrWhiteSpace(message) ? fallback : message.Length <= 500 ? message : fallback;
    private static AeroError PositiveId(string name, string entity) =>
        AeroError.ValidationError([$"{name} must be a positive {entity} identifier string."]);
    private static AeroError Required(string message) => AeroError.ValidationError([message]);

    private static AeroError? ValidateContext(AeroCmsToolExecutionContext context)
    {
        if (context.Principal.Identity?.IsAuthenticated != true)
            return AeroError.UnauthorizedError("Authentication is required.");
        if (context.UserId <= 0 || context.SiteId <= 0 || context.TenantId <= 0)
            return AeroError.ForbiddenError("A valid user, site, and tenant context is required.");
        return string.IsNullOrWhiteSpace(context.CorrelationId)
            ? AeroError.InvalidRequestError("A correlation context is required.")
            : null;
    }
}
