using Aero.Cms.Abstractions.Content;
using Aero.Cms.Core.Content.Search;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Web.Core.PublicApi;

/// <summary>Maps the public, read-only CMS query facade used by HTMX and JSON clients.</summary>
public static class PublicCmsQueryApi
{
    public static IServiceCollection AddPublicCmsQueryApi(this IServiceCollection services)
    {
        services.AddScoped<IPublicCmsQueryService, PublicCmsQueryService>();
        return services;
    }

    public static IEndpointRouteBuilder MapPublicCmsQueryApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/query")
            .WithTags("Public CMS Query")
            .AllowAnonymous();

        group.MapGet("/pages", QueryPagesAsync)
            .WithName("QueryPublishedPages")
            .Produces<PublicQueryPage<PublicPageQueryItem>>();

        group.MapGet("/posts", QueryPostsAsync)
            .WithName("QueryPublishedPosts")
            .Produces<PublicQueryPage<PublicPostQueryItem>>();

        group.MapGet("/docs", QueryDocsAsync)
            .WithName("QueryPublishedDocs")
            .Produces<PublicQueryPage<PublicDocsQueryItem>>();

        group.MapGet("/content/{contentTypeAlias}", QueryContentAsync)
            .WithName("QueryPublishedContentHierarchy")
            .Produces<ContentQueryResult>();

        group.MapGet("/content/{contentTypeAlias}/search", QueryContentSearchAsync)
            .WithName("SearchPublishedContent")
            .Produces<PublicContentSearchResult>();

        return endpoints;
    }

    private static async Task<IResult> QueryPagesAsync(
        HttpContext httpContext,
        IPublicCmsQueryService service,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
        => ToResponse(
            httpContext,
            await service.QueryPagesAsync(skip, take, cancellationToken),
            PublicCmsQueryHtmlWriter.Pages);

    private static async Task<IResult> QueryPostsAsync(
        HttpContext httpContext,
        IPublicCmsQueryService service,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
        => ToResponse(
            httpContext,
            await service.QueryPostsAsync(skip, take, cancellationToken),
            PublicCmsQueryHtmlWriter.Posts);

    private static async Task<IResult> QueryDocsAsync(
        HttpContext httpContext,
        IPublicCmsQueryService service,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
        => ToResponse(
            httpContext,
            await service.QueryDocsAsync(skip, take, cancellationToken),
            PublicCmsQueryHtmlWriter.Docs);

    private static async Task<IResult> QueryContentAsync(
        HttpContext httpContext,
        IPublicCmsQueryService service,
        string contentTypeAlias,
        ContentTraversal traversal = ContentTraversal.RootsWithDescendants,
        string? rootId = null,
        int maximumDepth = 4,
        int maximumItems = 50,
        string? fields = null,
        CancellationToken cancellationToken = default)
        => ToResponse(
            httpContext,
            await service.QueryContentAsync(
                contentTypeAlias,
                traversal,
                rootId,
                maximumDepth,
                maximumItems,
                SplitFields(fields),
                cancellationToken),
            PublicCmsQueryHtmlWriter.Content);

    private static async Task<IResult> QueryContentSearchAsync(
        HttpContext httpContext,
        IPublicCmsQueryService service,
        string contentTypeAlias,
        string q,
        string mode = "fulltext",
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ContentSearchMode>(mode, ignoreCase: true, out var parsedMode)
            || !Enum.IsDefined(parsedMode))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["mode"] = ["Mode must be 'fulltext' or 'semantic'."]
                });
        }

        return ToResponse(
            httpContext,
            await service.QueryContentSearchAsync(
                contentTypeAlias,
                q,
                parsedMode,
                skip,
                take,
                cancellationToken),
            PublicCmsQueryHtmlWriter.ContentSearch);
    }

    private static IReadOnlyList<string>? SplitFields(string? fields)
        => string.IsNullOrWhiteSpace(fields)
            ? null
            : fields.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IResult ToResponse<T>(
        HttpContext httpContext,
        Result<T> result,
        Func<T, string> htmlWriter)
    {
        httpContext.Response.Headers.Vary = "HX-Request, Accept";
        httpContext.Response.Headers.CacheControl = "private, no-store";
        return result switch
        {
            Result<T>.Ok success when WantsHtml(httpContext.Request) =>
                Results.Content(htmlWriter(success.Value), "text/html; charset=utf-8"),
            Result<T>.Ok success => Results.Json(success.Value),
            Result<T>.Failure failure => ToProblem(failure.Error),
            _ => Results.Problem(
                title: "Unexpected query result.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static bool WantsHtml(HttpRequest request)
        => string.Equals(
               request.Headers["HX-Request"].ToString(),
               "true",
               StringComparison.OrdinalIgnoreCase)
           || request.GetTypedHeaders().Accept?.Any(mediaType =>
               string.Equals(
                   mediaType.MediaType.Value,
                   "text/html",
                   StringComparison.OrdinalIgnoreCase)) == true;

    private static IResult ToProblem(AeroError error)
        => error switch
        {
            AeroError.Validation validation => Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["query"] = validation.Errors.ToArray()
                }),
            AeroError.NotFound notFound => Results.Problem(
                title: notFound.msg,
                statusCode: StatusCodes.Status404NotFound),
            AeroError.Cancelled => Results.StatusCode(499),
            _ => Results.Problem(
                title: "The read-only CMS query could not be completed.",
                statusCode: StatusCodes.Status500InternalServerError)
        };
}
