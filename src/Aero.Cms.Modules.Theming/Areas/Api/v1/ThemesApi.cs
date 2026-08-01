using Aero.Cms.Abstractions.Theming;
using AeroDB.Sable;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aero.Cms.Modules.Theming.Areas.Api.v1;

/// <summary>Maps deployment discovery, selected-site authoring, and immutable generated CSS endpoints.</summary>
public static class ThemesApi
{
    private const int MaximumImportCharacters = 262_144;

    public static void MapThemesApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/{HttpConstants.ApiPrefix}admin/themes")
            .WithTags("Admin - Themes")
            .RequireAuthorization("theme:design")
            .AddEndpointFilter(MapExpectedFailuresAsync);
        group.MapGet("/", GetAllThemes).WithName("GetAllThemes");
        group.MapGet("/details/{id}/{version}", GetTheme).WithName("GetThemeByIdentity");
        group.MapGet("/drafts", ListDrafts);
        group.MapPost("/drafts", CreateDraft);
        group.MapGet("/drafts/{id:long}", GetDraft);
        group.MapPut("/drafts/{id:long}", SaveDraft);
        group.MapPost("/drafts/{id:long}/publish", Publish);
        group.MapGet("/drafts/{id:long}/versions", ListVersions);
        group.MapGet("/drafts/{id:long}/export", Export);
        group.MapPost("/import", Import);
        group.MapPost("/drafts/{id:long}/preview", CreatePreview);
        group.MapGet("/preview/{token}.css", GetPreviewCss);
        group.MapGet("/publication-history", ListHistory);
        group.MapPost("/assign", Assign);

        // Published documents are public only by their immutable tenant/theme/version/hash identity.
        app.MapGet("/_cms/themes/{tenantId:long}/{themeId}/{version}/{sha256}.css", GetPublishedCss)
            .WithTags("Themes").AllowAnonymous();
    }

    private static IResult GetAllThemes([FromServices] IThemeCatalog catalog) => TypedResults.Ok<IReadOnlyList<ThemeSummary>>(catalog.GetAll().Select(ToSummary).ToList());
    private static IResult GetTheme(string id, string version, [FromServices] IThemeCatalog catalog) => catalog.Find(id, version) is { } theme ? TypedResults.Ok(ToDetail(theme)) : TypedResults.NotFound();
    private static async Task<IResult> ListDrafts(IThemeApplicationService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct));
    private static async Task<IResult> GetDraft(long id, IThemeApplicationService service, CancellationToken ct) => await service.GetAsync(id, ct) is { } theme ? Results.Ok(theme) : Results.NotFound();
    private static async Task<IResult> CreateDraft(CreateThemeCommand command, IThemeApplicationService service, CancellationToken ct)
    {
        var created = await service.CreateAsync(command, ct);
        return Results.Created($"drafts/{created.Id}", created);
    }
    private static async Task<IResult> SaveDraft(long id, SaveThemeDraftCommand command, IThemeApplicationService service, CancellationToken ct) => Results.Ok(await service.SaveDraftAsync(id, command, ct));
    private static async Task<IResult> Publish(long id, IThemeApplicationService service, CancellationToken ct) => Results.Ok(await service.PublishAsync(id, ct));
    private static async Task<IResult> ListVersions(long id, IThemeApplicationService service, CancellationToken ct) => Results.Ok(await service.ListVersionsAsync(id, ct));
    private static async Task<IResult> ListHistory(IThemeApplicationService service, CancellationToken ct) => Results.Ok(await service.ListPublicationHistoryAsync(ct));
    private static async Task<IResult> Assign(AssignThemeCommand command, IThemeApplicationService service, CancellationToken ct) => Results.Ok(await service.AssignAsync(command, ct));
    private static async Task<IResult> CreatePreview(long id, IThemeApplicationService service, CancellationToken ct) => Results.Ok(await service.CreatePreviewAsync(id, ct));
    private static async Task<IResult> Export(long id, IThemeApplicationService service, CancellationToken ct) => await service.GetAsync(id, ct) is { } theme ? Results.Ok(service.Import(service.Export(theme))) : Results.NotFound();
    private static async Task<IResult> Import(HttpRequest request, IThemeApplicationService service, CancellationToken ct)
    {
        if (request.ContentLength is > MaximumImportCharacters)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var reader = new StreamReader(request.Body);
        var json = await ReadBoundedAsync(reader, ct);
        var envelope = service.Import(json);
        if (envelope.SchemaVersion != 1) return Results.BadRequest("Unsupported theme import schema.");
        return Results.Created("drafts", await service.CreateAsync(new CreateThemeCommand(envelope.Theme.Name, envelope.Theme.Slug, envelope.Theme.Description, envelope.Theme.Tokens), ct));
    }

    private static async ValueTask<object?> MapExpectedFailuresAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ThemeRevisionConflictException exception)
        {
            return Results.Conflict(new
            {
                message = exception.Message,
                currentRevision = exception.CurrentRevision
            });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (ThemeConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or BadHttpRequestException)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[16_384];
        var builder = new StringBuilder();
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return builder.ToString();
            }

            if (builder.Length + read > MaximumImportCharacters)
            {
                throw new BadHttpRequestException(
                    "Theme import exceeds the 256 KB limit.",
                    StatusCodes.Status413PayloadTooLarge);
            }

            builder.Append(buffer, 0, read);
        }
    }
    private static async Task<IResult> GetPreviewCss(string token, HttpResponse response, IThemeApplicationService service, CancellationToken ct)
    {
        var css = await service.ResolvePreviewCssAsync(token, ct);
        if (css is null) return Results.NotFound();
        response.Headers.CacheControl = "no-store"; response.Headers["X-Content-Type-Options"] = "nosniff";
        return Results.Text(css, "text/css");
    }
    private static async Task<IResult> GetPublishedCss(long tenantId, string themeId, string version, string sha256, HttpRequest request, HttpResponse response, [FromServices] IQuerySession session, CancellationToken ct)
    {
        if (tenantId <= 0 || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit)) return Results.NotFound();
        var published = await session.Query<ThemeVersionDocument>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ThemeId == themeId && x.Version == version && x.CssSha256 == sha256.ToLowerInvariant(), ct);
        if (published is null) return Results.NotFound();
        var etag = $"\"{published.CssSha256}\"";
        response.Headers.ETag = etag; response.Headers.CacheControl = "public, max-age=31536000, immutable"; response.Headers["X-Content-Type-Options"] = "nosniff";
        if (request.Headers.IfNoneMatch.Any(x => string.Equals(x, etag, StringComparison.Ordinal))) return Results.StatusCode(StatusCodes.Status304NotModified);
        return Results.Text(published.Css, "text/css");
    }
    private static ThemeSummary ToSummary(InstalledThemeManifest manifest) => new(manifest.Id, manifest.Name, manifest.Version, manifest.Author, manifest.ThumbnailUrl, manifest.IsSafeDefault);
    private static ThemeDetail ToDetail(InstalledThemeManifest manifest) => new(manifest.Id, manifest.Name, manifest.Version, manifest.Author, manifest.Description, manifest.ThumbnailUrl, manifest.IsSafeDefault, manifest.Stylesheets.Select(static asset => new ThemeAsset(asset.Path, "stylesheet")).ToList());
}
