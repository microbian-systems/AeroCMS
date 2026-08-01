using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Events;
using Aero.Cms.Abstractions.Theming;
using Aero.Cms.Core;
using Aero.Cms.Core.Entities;
using AeroDB.Sable;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Wolverine;

namespace Aero.Cms.Modules.Theming;

/// <summary>Manager-facing commands for closed-token theme authoring.</summary>

/// <summary>Source-generated strict serialization contract for portable theme documents.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ThemeImportEnvelope))]
public sealed partial class ThemeJsonContext : JsonSerializerContext;

public interface IThemeApplicationService
{
    Task<ThemeDefinitionView?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThemeDefinitionView>> ListAsync(CancellationToken cancellationToken = default);
    Task<ThemeDefinitionView> CreateAsync(CreateThemeCommand command, CancellationToken cancellationToken = default);
    Task<ThemeDefinitionView> SaveDraftAsync(long id, SaveThemeDraftCommand command, CancellationToken cancellationToken = default);
    Task<ThemeVersionView> PublishAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThemeVersionView>> ListVersionsAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SiteThemePublicationView>> ListPublicationHistoryAsync(CancellationToken cancellationToken = default);
    Task<SiteThemePublicationView> AssignAsync(AssignThemeCommand command, CancellationToken cancellationToken = default);
    Task<ThemePreviewView> CreatePreviewAsync(long id, CancellationToken cancellationToken = default);
    Task<string?> ResolvePreviewCssAsync(string token, CancellationToken cancellationToken = default);
    ThemeImportEnvelope Import(string json);
    string Export(ThemeDefinitionView theme);
}

/// <summary>Provides a tenant-safe selected-site context for theme design commands.</summary>
public sealed class ThemeDesignContextAccessor(IHttpContextAccessor httpContextAccessor, IQuerySession session)
{
    public async Task<ThemeDesignContext> GetAsync(CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext ?? throw new UnauthorizedAccessException("No HTTP request is active.");
        if (context.User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Authentication is required.");
        if (!long.TryParse(context.Request.Cookies["AeroCms.SiteId"], out var siteId))
            throw new UnauthorizedAccessException("A selected site is required.");

        var site = await session.LoadAsync<SitesModel>(siteId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The selected site no longer exists.");
        var isAdmin = context.User.IsInRole("Admin") || context.User.HasClaim("is_admin", "true");
        var userId = GetUserId(context.User);
        if (!isAdmin)
        {
            if (userId is null) throw new UnauthorizedAccessException("A numeric user identity is required.");
            var assignment = await session.Query<UserSiteAssignment>()
                .FirstOrDefaultAsync(x => x.UserId == userId.Value && x.SiteId == site.Id, cancellationToken).ConfigureAwait(false);
            if (assignment is null || !assignment.Permissions.Any(x => x.Equals("design", StringComparison.OrdinalIgnoreCase) || x.Equals("update", StringComparison.OrdinalIgnoreCase)))
                throw new UnauthorizedAccessException("Theme design permission is required for the selected site.");
        }
        return new ThemeDesignContext(site.Id, site.TenantId, userId?.ToString() ?? "admin");
    }

    private static long? GetUserId(ClaimsPrincipal user)
        => long.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value, out var id) ? id : null;
}

public sealed record ThemeDesignContext(long SiteId, long TenantId, string Actor);
internal sealed record ThemePreviewSession(long TenantId, long SiteId, string Actor, string Css, DateTimeOffset ExpiresOn);

/// <summary>Coordinates tenant-scoped drafts, immutable versions, and selected-site assignment.</summary>
public sealed class ThemeApplicationService(
    IDocumentStore store,
    IQuerySession querySession,
    IThemeLibrary library,
    IThemeCssCompiler compiler,
    ThemeDesignContextAccessor designContext,
    IMemoryCache cache,
    IMessageBus messageBus) : IThemeApplicationService
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromMinutes(10);

    public async Task<ThemeDefinitionView?> GetAsync(long id, CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        var theme = await querySession.Query<ThemeDefinitionDocument>().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == scope.TenantId, ct).ConfigureAwait(false);
        return theme is null ? null : ToView(theme);
    }

    public async Task<IReadOnlyList<ThemeDefinitionView>> ListAsync(CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        var themes = await querySession.Query<ThemeDefinitionDocument>().Where(x => x.TenantId == scope.TenantId && !x.Archived).ToListAsync(ct).ConfigureAwait(false);
        return themes.OrderBy(x => x.Name, StringComparer.Ordinal).Select(ToView).ToArray();
    }

    public async Task<ThemeDefinitionView> CreateAsync(CreateThemeCommand command, CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        Validate(command.Name, command.Slug, command.Tokens ?? new ThemeTokenSet());
        var duplicate = (await querySession.Query<ThemeDefinitionDocument>().Where(x => x.TenantId == scope.TenantId && x.Slug == command.Slug).ToListAsync(ct).ConfigureAwait(false)).Count != 0;
        if (duplicate) throw new ThemeConflictException("A theme with this slug already exists for the selected tenant.");
        var document = new ThemeDefinitionDocument { Id = Snowflake.NewId(), TenantId = scope.TenantId, Name = command.Name.Trim(), Slug = command.Slug.Trim(), Description = command.Description?.Trim(), DraftTokenSet = command.Tokens ?? new(), CreatedBy = scope.Actor, ModifiedBy = scope.Actor };
        await using var write = await store.LightweightSessionAsync(ct).ConfigureAwait(false);
        write.Store(document);
        await write.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToView(document);
    }

    public async Task<ThemeDefinitionView> SaveDraftAsync(long id, SaveThemeDraftCommand command, CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        Validate(command.Name, command.Slug, command.Tokens);
        await using var write = await store.LightweightSessionAsync(ct).ConfigureAwait(false);
        var theme = await write.LoadAsync<ThemeDefinitionDocument>(id, ct).ConfigureAwait(false);
        if (theme is null || theme.TenantId != scope.TenantId) throw new KeyNotFoundException("Theme not found.");
        if (theme.Revision != command.ExpectedRevision) throw new ThemeRevisionConflictException(theme.Revision);
        theme.Name = command.Name.Trim(); theme.Slug = command.Slug.Trim(); theme.Description = command.Description?.Trim(); theme.DraftTokenSet = command.Tokens; theme.Revision = checked(theme.Revision + 1); theme.ModifiedOn = DateTimeOffset.UtcNow; theme.ModifiedBy = scope.Actor;
        write.Store(theme);
        await write.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToView(theme);
    }

    public async Task<ThemeVersionView> PublishAsync(long id, CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        await using var write = await store.LightweightSessionAsync(ct).ConfigureAwait(false);
        var theme = await write.LoadAsync<ThemeDefinitionDocument>(id, ct).ConfigureAwait(false);
        if (theme is null || theme.TenantId != scope.TenantId) throw new KeyNotFoundException("Theme not found.");
        var version = $"{theme.Revision}.0";
        var exists = (await querySession.Query<ThemeVersionDocument>().Where(x => x.TenantId == scope.TenantId && x.ThemeDefinitionId == id && x.Version == version).ToListAsync(ct).ConfigureAwait(false)).Count != 0;
        if (exists) throw new ThemeConflictException("This draft revision has already been published.");
        var themeId = $"tenant-{scope.TenantId}-{theme.Slug}";
        var dataThemeBaseName = $"theme-{theme.Id}-{theme.Revision}";
        var dataThemeName = theme.DraftTokenSet.DefaultMode == ThemeDefaultMode.Dark
            ? dataThemeBaseName + "-dark"
            : dataThemeBaseName;
        var compiled = compiler.Compile(dataThemeBaseName, theme.DraftTokenSet);
        var published = new ThemeVersionDocument { Id = Snowflake.NewId(), TenantId = scope.TenantId, ThemeDefinitionId = theme.Id, ThemeId = themeId, Version = version, DataThemeName = dataThemeName, TokenSet = theme.DraftTokenSet, Css = compiled.Css, CssSha256 = compiled.Sha256, PublishedBy = scope.Actor };
        write.Store(published);
        await write.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToView(published);
    }

    public async Task<IReadOnlyList<ThemeVersionView>> ListVersionsAsync(long id, CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        var versions = await querySession.Query<ThemeVersionDocument>().Where(x => x.TenantId == scope.TenantId && x.ThemeDefinitionId == id).ToListAsync(ct).ConfigureAwait(false);
        return versions.OrderByDescending(x => x.PublishedOn).Select(ToView).ToArray();
    }

    public async Task<IReadOnlyList<SiteThemePublicationView>> ListPublicationHistoryAsync(CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        var entries = await querySession.Query<SiteThemePublicationDocument>().Where(x => x.TenantId == scope.TenantId && x.SiteId == scope.SiteId).ToListAsync(ct).ConfigureAwait(false);
        return entries.OrderByDescending(x => x.Revision).Select(x => new SiteThemePublicationView(x.ThemeId, x.Version, x.Revision, x.PublishedOn, x.PreviousThemeId, x.PreviousVersion)).ToArray();
    }

    public async Task<SiteThemePublicationView> AssignAsync(AssignThemeCommand command, CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        if (command.ExpectedRevision <= 0 || string.IsNullOrWhiteSpace(command.ThemeId) || string.IsNullOrWhiteSpace(command.Version)) throw new ArgumentException("An exact theme and positive expected revision are required.");
        if (await library.ResolveAsync(scope.TenantId, command.ThemeId, command.Version, ct).ConfigureAwait(false) is null) throw new KeyNotFoundException("Theme version not found for the selected tenant.");
        await using var write = await store.LightweightSessionAsync(ct).ConfigureAwait(false);
        var site = await write.LoadAsync<SitesModel>(scope.SiteId, ct).ConfigureAwait(false) ?? throw new KeyNotFoundException("Selected site not found.");
        if (site.TenantId != scope.TenantId) throw new UnauthorizedAccessException();
        if (site.ThemeRevision != command.ExpectedRevision) throw new ThemeRevisionConflictException(site.ThemeRevision);
        var previousId = site.ThemeId; var previousVersion = site.ThemeVersion;
        site.ThemeId = command.ThemeId; site.ThemeVersion = command.Version; site.ThemeRevision = checked(site.ThemeRevision + 1); site.ModifiedOn = DateTimeOffset.UtcNow; site.ModifiedBy = scope.Actor;
        var publication = new SiteThemePublicationDocument { Id = Snowflake.NewId(), TenantId = scope.TenantId, SiteId = site.Id, ThemeId = site.ThemeId, Version = site.ThemeVersion, Revision = site.ThemeRevision, PublishedBy = scope.Actor, PreviousThemeId = previousId, PreviousVersion = previousVersion };
        write.Store(site); write.Store(publication);
        await write.SaveChangesAsync(ct).ConfigureAwait(false);
        await messageBus.PublishAsync(new SiteThemeChangedEvent(site.Id, site.ThemeId, site.ThemeVersion, site.ThemeRevision, site.ModifiedOn.Value)).ConfigureAwait(false);
        return new SiteThemePublicationView(publication.ThemeId, publication.Version, publication.Revision, publication.PublishedOn, publication.PreviousThemeId, publication.PreviousVersion);
    }

    public async Task<ThemePreviewView> CreatePreviewAsync(long id, CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        var theme = await querySession.Query<ThemeDefinitionDocument>().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == scope.TenantId, ct).ConfigureAwait(false) ?? throw new KeyNotFoundException("Theme not found.");
        var css = compiler.CompilePreview($"preview-{theme.Id}", theme.DraftTokenSet).Css;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(); var expiry = DateTimeOffset.UtcNow.Add(PreviewLifetime);
        cache.Set(PreviewKey(token), new ThemePreviewSession(scope.TenantId, scope.SiteId, scope.Actor, css, expiry), expiry);
        return new ThemePreviewView(token, expiry);
    }

    public async Task<string?> ResolvePreviewCssAsync(string token, CancellationToken ct = default)
    {
        var scope = await designContext.GetAsync(ct).ConfigureAwait(false);
        return cache.TryGetValue(PreviewKey(token), out ThemePreviewSession? preview) && preview is not null && preview.ExpiresOn > DateTimeOffset.UtcNow && preview.TenantId == scope.TenantId && preview.SiteId == scope.SiteId && preview.Actor == scope.Actor ? preview.Css : null;
    }

    public ThemeImportEnvelope Import(string json)
    {
        var envelope = JsonSerializer.Deserialize(json, ThemeJsonContext.Default.ThemeImportEnvelope)
            ?? throw new JsonException("Theme import is empty.");
        if (envelope.Theme is null || envelope.Theme.Tokens is null)
        {
            throw new JsonException("Theme import must include a theme and its complete token document.");
        }

        ThemeTokenValidator.ThrowIfInvalid(envelope.Theme.Tokens);
        return envelope;
    }
    public string Export(ThemeDefinitionView theme) => JsonSerializer.Serialize(new ThemeImportEnvelope(1, new ThemeImportPayload(theme.Name, theme.Slug, theme.Description, theme.Tokens)), ThemeJsonContext.Default.ThemeImportEnvelope);
    private static string PreviewKey(string token) => $"aero-theme-preview:{token}";
    private static ThemeDefinitionView ToView(ThemeDefinitionDocument x) => new(x.Id, x.Name, x.Slug, x.Description, x.DraftTokenSet, x.Revision, x.Archived, ThemeTokenValidator.GetContrastWarnings(x.DraftTokenSet));
    private static ThemeVersionView ToView(ThemeVersionDocument x) => new(x.ThemeId, x.Version, x.DataThemeName, x.CssSha256, x.PublishedOn);
    private static void Validate(string name, string slug, ThemeTokenSet tokens)
    { if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug) || !slug.All(x => x is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) throw new ArgumentException("Name and lowercase slug are required."); ThemeTokenValidator.ThrowIfInvalid(tokens); }
}

public sealed class ThemeRevisionConflictException(long currentRevision) : InvalidOperationException("The theme changed while it was being saved.") { public long CurrentRevision { get; } = currentRevision; }
public sealed class ThemeConflictException(string message) : InvalidOperationException(message);
