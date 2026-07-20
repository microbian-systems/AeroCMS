using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Abstractions.Services;
using Aero.Cms.Modules.Posts;
using Aero.Cms.Modules.Posts.Models;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Tenant;
using Aero.Cms.Core.Entities;
using Aero.Cms.Core.Infrastructure;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Modular;
using AeroDB.Sable;
using Aero.Cms.Core.Models;
using Aero.Cms.Modules.Media;
using Aero.Cms.Modules.Modules.Services;
using Aero.Cms.Modules.Commerce.Data;
using Aero.Cms.Modules.Footer.Domain;
using Aero.Cms.Modules.Footer.Events;
using Aero.Cms.Modules.Navigation.Domain;
using Aero.Cms.Modules.Navigation.Events;
using Aero.Cms.Modules.Setup.Bootstrap;
using Microsoft.AspNetCore.Hosting;
using Serilog;
using System.Globalization;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Contains the infrastructure selections and initial content needed to seed a new installation.
/// </summary>
/// <remarks>
/// Passwords, connection strings, database credentials, and Infisical credentials are
/// sensitive workflow inputs. They must not be logged or stored in the durable setup-state document.
/// </remarks>
public sealed record SeedDatabaseRequest(
    string DatabaseMode,
    string CacheMode,
    string SecretProvider,
    string AuthenticationMode,
    string? ConnectionString,
    string? CacheConnectionString,
    string? InfisicalMachineId,
    string? InfisicalClientSecret,
    string AdminUserName,
    string AdminEmail,
    string Password,
    string SiteName,
    string HomepageTitle,
    string BlogName,
    string Hostname,
    string DefaultCulture,
    IReadOnlyList<string> SupportedCultures)
{
    /// <summary>Gets whether the configured server accepts unauthenticated connections.</summary>
    public bool DatabaseUnauthenticated { get; init; }

    /// <summary>Gets the configured SurrealDB server username.</summary>
    public string? DatabaseUsername { get; init; }

    /// <summary>Gets the configured SurrealDB server password.</summary>
    public string? DatabasePassword { get; init; }
}

/// <summary>
/// Describes installation artifacts created by a setup completion attempt.
/// </summary>
public sealed class SeedDatabaseResult
{
    /// <summary>
    /// Gets whether the result contains no reported errors.
    /// </summary>
public bool Succeeded => Errors.Count == 0;
    /// <summary>
    /// Gets whether the database already contained a completed setup marker.
    /// </summary>
public bool AlreadyComplete { get; init; }
    /// <summary>
    /// Gets whether this attempt created the initial administrator account.
    /// </summary>
public bool CreatedAdmin { get; init; }
    /// <summary>
    /// Gets whether this attempt created any CMS role or administrator role assignment.
    /// </summary>
public bool CreatedRoles { get; init; }
    /// <summary>
    /// Gets whether the result reports a successfully provisioned tenant.
    /// </summary>
public bool CreatedTenant { get; init; }
    /// <summary>
    /// Gets whether the result reports a successfully provisioned site.
    /// </summary>
public bool CreatedSite { get; init; }
    /// <summary>
    /// Gets the provisioned or reused tenant identifier.
    /// </summary>
public long? TenantId { get; init; }
    /// <summary>
    /// Gets the provisioned or reused site identifier.
    /// </summary>
public long? SiteId { get; init; }
    /// <summary>
    /// Gets failure messages returned by expected setup operations.
    /// </summary>
public List<string> Errors { get; } = [];

    /// <summary>
    /// Creates a failed result from zero or more error messages.
    /// </summary>
    /// <param name="errors">The messages to include after blank values are removed.</param>
    /// <returns>A result containing the supplied non-blank errors.</returns>
public static SeedDatabaseResult Failure(params string[] errors)
        => Failure(errors.AsEnumerable());

    /// <summary>
    /// Creates a failed result from an error sequence.
    /// </summary>
    /// <param name="errors">The messages to include after blank values are removed.</param>
    /// <returns>A result containing the supplied non-blank errors.</returns>
public static SeedDatabaseResult Failure(IEnumerable<string> errors)
    {
        var result = new SeedDatabaseResult();
        result.Errors.AddRange(errors.Where(error => !string.IsNullOrWhiteSpace(error)));
        return result;
    }
}

/// <summary>
/// Completes durable initialization of a newly configured AeroCMS installation.
/// </summary>
public interface ISeedDatabaseService
{
    /// <summary>
    /// Provisions identity, tenant, site, starter content, module state, and completion markers.
    /// </summary>
    /// <param name="request">The infrastructure and initial-content selections.</param>
    /// <param name="cancellationToken">Cancels database, identity, media, module, or file operations.</param>
    /// <returns>
    /// A result describing created artifacts or expected failures. Infrastructure exceptions
    /// outside the starter-content stage may propagate.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
Task<SeedDatabaseResult> CompleteAsync(SeedDatabaseRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alias for backwards compatibility. ISeedDatabaseService was previously named ISetupCompletionService.
/// </summary>
public interface ISetupCompletionService : ISeedDatabaseService { }

// todo - each module should have its own seeding service via SRP and Verticle Slices 

/// <summary>
/// Coordinates initial installation seeding across Identity, AeroDB, content modules, and bootstrap persistence.
/// </summary>
/// <remarks>
/// The durable setup-state document is authoritative for idempotency. If it is already
/// complete, this service repairs the file-based completion flags and skips all seeding.
/// The workflow spans multiple stores and is not transactional as a whole: Identity,
/// tenant/site, content, module, and settings changes may commit at different points.
/// Individual page-save failures are logged and do not fail the overall starter-content stage.
/// </remarks>
public sealed class SeedDatabaseService(
    IDocumentSession session,
    IWebHostEnvironment env,
    ISetupIdentityBootstrapper identityBootstrapper,
    IPageContentService pageContentService,
    IPagePublishingWorkflowService pagePublishingWorkflowService,
    IPostContentService blogPostContentService,
    IMediaService mediaService,
    ICommerceSeedService commerceSeedService,
    IModuleInitializationService moduleInitializationService,
    IBootstrapCompletionWriter bootstrapCompletionWriter,
    ITenantService tenantService,
    ISiteService siteService,
    IApiKeyService apiKeyService,
    IReadOnlyList<ModuleDescriptor> moduleDescriptors) : ISeedDatabaseService, ISetupCompletionService
{
    /// <inheritdoc />
public async Task<SeedDatabaseResult> CompleteAsync(SeedDatabaseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingState = await session.LoadAsync<SetupStateDocument>(SetupStateDocument.FixedId, ct);
        if (existingState?.IsComplete == true)
        {
            // The database is authoritative. If the prior process committed the
            // setup transaction but stopped before updating appsettings, repair
            // the bootstrap flags on the next run.
            await bootstrapCompletionWriter.MarkCompleteAsync(ct);
            return new SeedDatabaseResult
            {
                AlreadyComplete = true
            };
        }

        var identityResult = await identityBootstrapper.BootstrapAsync(
            new SetupIdentityBootstrapRequest(
                request.AdminUserName,
                request.AdminEmail,
                request.Password),
            ct);

        if (!identityResult.Succeeded)
        {
            return SeedDatabaseResult.Failure(identityResult.Errors.Select(error => error.Description));
        }

        // Create default admin API key
        // TODO: Remove this pre-defined key later once stable
        var apiKey = await apiKeyService.CreateKeyAsync(identityResult.AdminUser!.Id, request.AdminEmail, cancellationToken: ct);

        // Create tenant and site for multi-tenant foundation
        var (tenantResult, siteResult) = await CreateTenantAndSiteAsync(request, ct);
        if (tenantResult.IsFailure || siteResult.IsFailure)
        {
            var errors = new List<string>();
            if (tenantResult is Result<TenantModel, AeroError>.Failure tenantFail)
                errors.Add(tenantFail.Error is AeroError.Error te ? te.msg : "Failed to create tenant");
            if (siteResult is Result<SitesModel, AeroError>.Failure siteFail)
                errors.Add(siteFail.Error is AeroError.Error se ? se.msg : "Failed to create site");
            return SeedDatabaseResult.Failure(errors);
        }

        var tenant = tenantResult is Result<TenantModel, AeroError>.Ok tenantOk ? tenantOk.Value : null;
        var site = siteResult is Result<SitesModel, AeroError>.Ok siteOk ? siteOk.Value : null;
        
        if (tenant == null || site == null)
        {
            return SeedDatabaseResult.Failure("Failed to create tenant or site");
        }

        var cultureSettings = NormalizeCultureSettings(request.DefaultCulture, request.SupportedCultures);

        try
        {
            await SeedStarterContentAsync(request, site.Id, cultureSettings.DefaultCulture, cultureSettings.SupportedCultures, ct);
        }
        catch (Exception ex)
        {
            return SeedDatabaseResult.Failure(ex.Message);
        }

        var completedAtUtc = existingState?.CompletedAtUtc ?? DateTimeOffset.UtcNow;
        session.Store(new SetupStateDocument
        {
            Id = SetupStateDocument.FixedId,
            IsComplete = true,
            CompletedAtUtc = completedAtUtc,
            DatabaseMode = request.DatabaseMode,
            CacheMode = request.CacheMode,
            SecretProvider = request.SecretProvider,
            AdminEmail = request.AdminEmail,
            SiteName = request.SiteName,
            HomepageTitle = request.HomepageTitle,
            BlogName = request.BlogName,
            CreatedTenantId = tenant.Id,
            CreatedSiteId = site.Id,
            Hostname = request.Hostname,
            DefaultCulture = cultureSettings.DefaultCulture,
            SupportedCultures = cultureSettings.SupportedCultures
        });
        await session.SaveChangesAsync(ct);

        // Discover and save all available modules
        await SaveModuleStateAsync(ct);
        await bootstrapCompletionWriter.MarkCompleteAsync(ct);

        return new SeedDatabaseResult
        {
            CreatedAdmin = identityResult.CreatedAdmin,
            CreatedRoles = identityResult.CreatedRoles,
            CreatedTenant = true,
            CreatedSite = true,
            TenantId = tenant.Id,
            SiteId = site.Id
        };
    }

    /// <summary>
    /// Reuses a matching tenant/site pair or creates both and assigns the requested primary host.
    /// </summary>
    /// <remarks>Creation is sequential and a site or host failure does not roll back an already-created tenant.</remarks>
    private async Task<(Result<TenantModel, AeroError> Tenant, Result<SitesModel, AeroError> Site)> CreateTenantAndSiteAsync(
        SeedDatabaseRequest request, 
        CancellationToken cancellationToken)
    {
        var existing = await FindExistingTenantAndSiteAsync(request, cancellationToken);
        if (existing is { Tenant: not null, Site: not null })
        {
            var hostResult = await siteService.AddHostAsync(
                existing.Site.Id,
                request.Hostname,
                isPrimary: true,
                cancellationToken);

            if (hostResult.IsFailure)
            {
                return (
                    new Result<TenantModel, AeroError>.Ok(existing.Tenant),
                    new Result<SitesModel, AeroError>.Failure(
                        AeroError.CreateError(GetHostFailureMessage(hostResult))));
            }

            return (
                new Result<TenantModel, AeroError>.Ok(existing.Tenant),
                new Result<SitesModel, AeroError>.Ok(existing.Site));
        }

        // Create tenant with SiteName as the tenant name
        var tenant = new TenantModel
        {
            Id = Snowflake.NewId(),
            Name = request.SiteName,
            Hostname = request.Hostname,
            Notes = $"Default tenant created during setup on {DateTimeOffset.UtcNow:yyyy-MM-dd}"
        };

        var tenantResult = await tenantService.CreateTenantAsync(tenant, cancellationToken);
        
        if (tenantResult.IsFailure)
        {
            var tenantError = tenantResult is Result<TenantModel, AeroError>.Failure tf 
                ? (tf.Error is AeroError.Error te ? te.msg : "Failed to create tenant")
                : "Failed to create tenant";
            return (tenantResult, new Result<SitesModel, AeroError>.Failure(AeroError.CreateError(tenantError)));
        }

        // Get the created tenant's ID
        var createdTenantId = tenantResult is Result<TenantModel, AeroError>.Ok to 
            ? to.Value.Id 
            : tenant.Id;

        var cultureSettings = NormalizeCultureSettings(request.DefaultCulture, request.SupportedCultures);

        // Create site linked to the tenant
        var site = new SitesModel
        {
            Id = Snowflake.NewId(),
            TenantId = createdTenantId,
            Name = request.SiteName,
            IsEnabled = true,
            DefaultCulture = cultureSettings.DefaultCulture,
            SupportedCultures = cultureSettings.SupportedCultures
        };

        var siteResult = await siteService.CreateSiteAsync(site, cancellationToken);

        if (siteResult.IsSuccess)
        {
            // Create a SiteHost entry for the primary host/domain
            var siteId = siteResult is Result<SitesModel, AeroError>.Ok ok ? ok.Value.Id : site.Id;
            var hostResult = await siteService.AddHostAsync(
                siteId,
                request.Hostname!,
                isPrimary: true,
                cancellationToken);

            if (hostResult.IsFailure)
            {
                return (
                    tenantResult,
                    new Result<SitesModel, AeroError>.Failure(
                        AeroError.CreateError(GetHostFailureMessage(hostResult))));
            }
        }
        
        return (tenantResult, siteResult);
    }

    /// <summary>
    /// Finds an existing site through its normalized host, then falls back to tenant and site names.
    /// </summary>
    private async Task<(TenantModel? Tenant, SitesModel? Site)> FindExistingTenantAndSiteAsync(
        SeedDatabaseRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedHost = HostNormalizer.Normalize(request.Hostname);

        var existingHost = await session.Query<SiteHost>()
            .Where(siteHost => siteHost.Host == normalizedHost)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingHost is not null)
        {
            var existingSite = await session.LoadAsync<SitesModel>(existingHost.SiteId, cancellationToken);
            if (existingSite is not null)
            {
                var existingTenant = await session.LoadAsync<TenantModel>(existingSite.TenantId, cancellationToken);
                if (existingTenant is not null)
                {
                    return (existingTenant, existingSite);
                }
            }
        }

        var tenant = await session.Query<TenantModel>()
            .Where(candidate =>
                candidate.Hostname == normalizedHost &&
                candidate.Name == request.SiteName)
            .OrderBy(candidate => candidate.CreatedOn)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return (null, null);
        }

        var site = await session.Query<SitesModel>()
            .Where(candidate =>
                candidate.TenantId == tenant.Id &&
                candidate.Name == request.SiteName)
            .OrderBy(candidate => candidate.CreatedOn)
            .FirstOrDefaultAsync(cancellationToken);

        return (tenant, site);
    }

    /// <summary>
    /// Extracts a user-facing site-host failure message from a railway result.
    /// </summary>
    private static string GetHostFailureMessage(Result<SiteHost, AeroError> hostResult)
        => hostResult is Result<SiteHost, AeroError>.Failure failure
            ? failure.Error is AeroError.Error error
                ? error.msg
                : "Failed to create site host"
            : "Failed to create site host";

    /// <summary>
    /// Seeds initial pages, navigation, footer, media, posts, error aliases, products, and global settings.
    /// </summary>
    /// <remarks>
    /// Spanish (Mexico) variants are added only when <c>es-MX</c> is supported but is not
    /// the default culture. Several collaborators persist independently during this method.
    /// </remarks>
    private async Task SeedStarterContentAsync(
        SeedDatabaseRequest request,
        long siteId,
        string defaultCulture,
        IReadOnlyList<string> supportedCultures,
        CancellationToken cancellationToken)
    {
        // Build pages first to get their IDs for navigation items
        var homepage = BuildHomepage(request);
        homepage.DraftContent = LivingStandardSeedPageFactory.Create(
            Normalize(request.HomepageTitle),
            homepage.Summary!,
            [
                ("Performance First", "Built on .NET 10 with a document-oriented content model designed for fast, predictable publishing."),
                ("Editor Friendly", "Compose semantic HTML sections, layouts, text, media, lists, and calls to action without framework lock-in.")
            ],
            backgroundImageUrl: "/media/data-center.png");
        var blogListing = BuildBlogListingPage(request);
        blogListing.DraftContent = LivingStandardSeedPageFactory.Create(
            blogListing.Title,
            blogListing.Summary!,
            [("Latest Articles", "Ten example posts are already published so the site is useful immediately.")]);
        var aboutPage = BuildAboutPage();
        aboutPage.DraftContent = LivingStandardSeedPageFactory.Create(
            aboutPage.Title,
            aboutPage.Summary!,
            [
                ("Our Mission", "We build tools that help teams publish clear, accessible content without unnecessary technical friction."),
                ("Our Values", "Integrity, innovation, and inclusivity guide how we design and ship Aero CMS.")
            ]);
        var contactPage = BuildContactPage();
        contactPage.DraftContent = LivingStandardSeedPageFactory.Create(
            contactPage.Title,
            contactPage.Summary!,
            [
                ("Get In Touch", "Our team typically responds within one business day."),
                ("Visit Us", "123 Main Street, Suite 100, Anytown, USA")
            ],
            callToAction: ("Send Us a Message", "mailto:hello@example.com"));
        var privacyPage = BuildPrivacyPage();
        privacyPage.DraftContent = LivingStandardSeedPageFactory.Create(
            privacyPage.Title,
            privacyPage.Summary!,
            [
                ("Information We Collect", "We collect only the information needed to provide and improve the site."),
                ("Your Rights", "You may request access, correction, or deletion of your personal information.")
            ]);
        var termsPage = BuildTermsPage();
        termsPage.DraftContent = LivingStandardSeedPageFactory.Create(
            termsPage.Title,
            termsPage.Summary!,
            [
                ("Use of the Site", "Use this site only for lawful purposes and do not interfere with its operation."),
                ("Intellectual Property", "Site content remains the property of Aero CMS or its respective licensors.")
            ]);
        var cookiesPage = BuildCookiesPage();
        cookiesPage.DraftContent = LivingStandardSeedPageFactory.Create(
            cookiesPage.Title,
            cookiesPage.Summary!,
            [
                ("What Are Cookies?", "Cookies are small files that help sites remember preferences and improve performance."),
                ("Managing Cookies", "You can control or remove cookies through your browser settings.")
            ]);
        var docs = BuildStarterDocsContent();
        var rootDoc = docs.First(d => d.Slug == "docs");

        StampPageCulture(homepage, defaultCulture);
        StampPageCulture(blogListing, defaultCulture);
        StampPageCulture(aboutPage, defaultCulture);
        StampPageCulture(contactPage, defaultCulture);
        StampPageCulture(privacyPage, defaultCulture);
        StampPageCulture(termsPage, defaultCulture);
        StampPageCulture(cookiesPage, defaultCulture);
        // Store Living Standard pages.
        homepage.SiteId = siteId;
        var homeR = await SavePublishedPageAsync(homepage, cancellationToken);
        if (homeR.IsFailure) Log.Warning("Failed to seed homepage: {Error}", ErrMsg(homeR));

        blogListing.SiteId = siteId;
        var blogR = await SavePublishedPageAsync(blogListing, cancellationToken);
        if (blogR.IsFailure) Log.Warning("Failed to seed blog listing page: {Error}", ErrMsg(blogR));

        aboutPage.SiteId = siteId;
        var aboutR = await SavePublishedPageAsync(aboutPage, cancellationToken);
        if (aboutR.IsFailure) Log.Warning("Failed to seed about page: {Error}", ErrMsg(aboutR));

        contactPage.SiteId = siteId;
        var contactR = await SavePublishedPageAsync(contactPage, cancellationToken);
        if (contactR.IsFailure) Log.Warning("Failed to seed contact page: {Error}", ErrMsg(contactR));

        privacyPage.SiteId = siteId;
        var privacyR = await SavePublishedPageAsync(privacyPage, cancellationToken);
        if (privacyR.IsFailure) Log.Warning("Failed to seed privacy page: {Error}", ErrMsg(privacyR));

        termsPage.SiteId = siteId;
        var termsR = await SavePublishedPageAsync(termsPage, cancellationToken);
        if (termsR.IsFailure) Log.Warning("Failed to seed terms page: {Error}", ErrMsg(termsR));

        cookiesPage.SiteId = siteId;
        var cookiesR = await SavePublishedPageAsync(cookiesPage, cancellationToken);
        if (cookiesR.IsFailure) Log.Warning("Failed to seed cookies page: {Error}", ErrMsg(cookiesR));
        
        foreach (var doc in docs)
        {
            doc.SiteId = siteId;
            session.Store(doc);
        }

        var navMenuTranslationGroupId = await SeedDefaultNavMenuAsync(siteId, defaultCulture, homepage.Id, aboutPage.Id, contactPage.Id, blogListing.Id, cancellationToken);
        var footerTranslationGroupId = await SeedDefaultFooterAsync(siteId, defaultCulture, aboutPage.Id, contactPage.Id, blogListing.Id, cancellationToken);

        if (ShouldSeedSpanishMexico(defaultCulture, supportedCultures))
        {
            await SeedSpanishMexicoStarterContentAsync(
                request,
                siteId,
                homepage.Id,
                blogListing.Id,
                aboutPage.Id,
                contactPage.Id,
                navMenuTranslationGroupId,
                footerTranslationGroupId,
                cancellationToken);
        }

        // Seed starter media assets from wwwroot/media
        await SeedStarterMediaAsync(cancellationToken);

        // Build starter blog content (posts and tags)
        var (posts, tags) = BuildStarterBlogContent(request);

        // Store tags first
        foreach (var tag in tags)
        {
            tag.SiteId = siteId;
            session.Store(tag);
        }

        if (ShouldSeedSpanishMexico(defaultCulture, supportedCultures))
        {
            foreach (var translation in BuildSpanishMexicoTagTranslations(tags))
            {
                session.Store(translation);
            }
        }

        // Save blog posts (blocks are stored inline in Content)
        foreach (var post in posts)
        {
            post.SiteId = siteId;
            post.Culture = defaultCulture;
            post.TranslationGroupId ??= post.Id;
            await blogPostContentService.SaveAsync(post, cancellationToken);
        }

        // Seed /oops 404 page with alias
        await SeedOopsPageAsync(siteId, defaultCulture, cancellationToken);

        // Seed commerce products
        await commerceSeedService.SeedAsync(siteId, cancellationToken);

        // Seed default global settings
        SeedDefaultSettings(defaultCulture);
    }

    /// <summary>
    /// Reuses or event-sources the default navigation menu and ensures it is selected for the site.
    /// </summary>
    /// <returns>The menu translation-group identifier used by localized variants.</returns>
    private async Task<long> SeedDefaultNavMenuAsync(
        long siteId,
        string culture,
        long homepageId,
        long aboutPageId,
        long contactPageId,
        long blogListingPageId,
        CancellationToken cancellationToken)
    {
        const string navMenuName = "Header Menu";
        const string navMenuKey = "header-menu";

        var existingMenu = await session.Query<NavMenuDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Culture == culture && x.Key == navMenuKey, cancellationToken);
        var settings = await session.Query<SiteNavigationSettingsDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId, cancellationToken);

        if (existingMenu is not null)
        {
            if (settings?.DefaultNavMenuId is null)
            {
                var changed = new SiteDefaultNavMenuChanged(siteId, existingMenu.Id, UserId: null, DateTimeOffset.UtcNow);
                if (settings is null)
                    session.Events.StartStream(NavMenuStreams.SiteSettings(siteId), new object[] { changed });
                else
                    session.Events.Append(NavMenuStreams.SiteSettings(siteId), new object[] { changed });
            }

            return existingMenu.TranslationGroupId ?? existingMenu.Id;
        }

        var now = DateTimeOffset.UtcNow;
        var navMenuId = Snowflake.NewId();
        var snapshot = BuildSeedNavMenuSnapshot(
            [
                new NavLink { Key = "home", Label = "Home", Href = "/", PageId = homepageId, AltText = "Home Page", Alignment = NavAlignment.Left },
                new NavLink { Key = "about", Label = "About", Href = "/about", PageId = aboutPageId, AltText = "About Us", Alignment = NavAlignment.Left },
                new NavLink { Key = "contact", Label = "Contact", Href = "/contact", PageId = contactPageId, AltText = "Contact Us", Alignment = NavAlignment.Left },
                new NavLink { Key = "docs", Label = "Docs", Href = "/docs", AltText = "Documentation", Alignment = NavAlignment.Left },
                new NavLink { Key = "blog", Label = "Blog", Href = "/blog", PageId = blogListingPageId, AltText = "Blog and Field Notes", Alignment = NavAlignment.Left }
            ]);
        snapshot.Validate();

        session.Events.StartStream(
            NavMenuStreams.Menu(navMenuId),
            new object[]
            {
                new NavMenuCreated(siteId, navMenuName, navMenuKey, UserId: null, now, Culture: culture, TranslationGroupId: navMenuId),
                new NavMenuDraftSaved(siteId, navMenuName, navMenuKey, snapshot, UserId: null, now, "Seeded starter navigation"),
                new NavMenuPublished(siteId, snapshot, UserId: null, now, "Seeded starter navigation")
            });

        if (settings?.DefaultNavMenuId is null)
        {
            var defaultChanged = new SiteDefaultNavMenuChanged(siteId, navMenuId, UserId: null, now);
            if (settings is null)
                session.Events.StartStream(NavMenuStreams.SiteSettings(siteId), new object[] { defaultChanged });
            else
                session.Events.Append(NavMenuStreams.SiteSettings(siteId), new object[] { defaultChanged });
        }

        return navMenuId;
    }

    /// <summary>
    /// Reuses or event-sources the default footer and ensures it is selected for the site.
    /// </summary>
    /// <returns>The footer translation-group identifier used by localized variants.</returns>
    private async Task<long> SeedDefaultFooterAsync(
        long siteId,
        string culture,
        long aboutPageId,
        long contactPageId,
        long blogListingPageId,
        CancellationToken cancellationToken)
    {
        const string footerName = "Site Footer";
        const string footerKey = "site-footer";

        var existingFooter = await session.Query<FooterDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Culture == culture && x.Key == footerKey, cancellationToken);
        var settings = await session.Query<SiteFooterSettingsDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId, cancellationToken);

        if (existingFooter is not null)
        {
            if (settings?.DefaultFooterId is null)
            {
                var changed = new SiteDefaultFooterChanged(siteId, existingFooter.Id, UserId: null, DateTimeOffset.UtcNow);
                if (settings is null)
                    session.Events.StartStream(FooterStreams.SiteSettings(siteId), new object[] { changed });
                else
                    session.Events.Append(FooterStreams.SiteSettings(siteId), new object[] { changed });
            }

            return existingFooter.TranslationGroupId ?? existingFooter.Id;
        }

        var now = DateTimeOffset.UtcNow;
        var footerId = Snowflake.NewId();
        var snapshot = new FooterSnapshot
        {
            Brand = new FooterBrandSettings
            {
                CompanyName = "Aero CMS",
                Tagline = "A fast, modular CMS for modern .NET sites.",
                LogoAltText = "Aero CMS logo"
            },
            Legal = FooterLegalSettings.Default with
            {
                CopyrightText = "Aero CMS. All rights reserved.",
                LegalLinks =
                [
                    new FooterLink("Privacy", "/privacy"),
                    new FooterLink("Terms", "/terms"),
                    new FooterLink("Cookies", "/cookies")
                ]
            },
            Rows = BuildSeedFooterRows(
                [
                    new FooterLinkGroup
                    {
                        Key = "company",
                        Title = "Company",
                        Order = 0,
                        Links =
                        [
                            new FooterLink("About", "/about"),
                            new FooterLink("Contact", "/contact")
                        ]
                    },
                    new FooterLinkGroup
                    {
                        Key = "content",
                        Title = "Content",
                        Order = 1,
                        Links =
                        [
                            new FooterLink("Blog", "/blog"),
                            new FooterLink("Docs", "/docs")
                        ]
                    },
                    new FooterLinkGroup
                    {
                        Key = "site",
                        Title = "Site",
                        Order = 2,
                        Links =
                        [
                            new FooterLink("Home", "/"),
                            new FooterLink("Sitemap", "/sitemap.xml")
                        ]
                    }
                ])
        };
        snapshot.Validate();

        session.Events.StartStream(
            FooterStreams.Footer(footerId),
            new object[]
            {
                new FooterCreated(siteId, footerName, footerKey, "Default seeded site footer", UserId: null, now, Culture: culture, TranslationGroupId: footerId),
                new FooterDraftSaved(siteId, footerName, footerKey, "Default seeded site footer", snapshot, UserId: null, now, "Seeded starter footer"),
                new FooterPublished(siteId, snapshot, UserId: null, now, "Seeded starter footer")
            });

        if (settings?.DefaultFooterId is null)
        {
            var defaultChanged = new SiteDefaultFooterChanged(siteId, footerId, UserId: null, now);
            if (settings is null)
                session.Events.StartStream(FooterStreams.SiteSettings(siteId), new object[] { defaultChanged });
            else
                session.Events.Append(FooterStreams.SiteSettings(siteId), new object[] { defaultChanged });
        }

        return footerId;
    }

    /// <summary>
    /// Seeds the supported Spanish (Mexico) page, navigation, and footer variants.
    /// </summary>
    private async Task SeedSpanishMexicoStarterContentAsync(
        SeedDatabaseRequest request,
        long siteId,
        long homepageTranslationGroupId,
        long blogTranslationGroupId,
        long aboutTranslationGroupId,
        long contactTranslationGroupId,
        long navMenuTranslationGroupId,
        long footerTranslationGroupId,
        CancellationToken cancellationToken)
    {
        var homepage = BuildSpanishHomepage(request, homepageTranslationGroupId);
        homepage.DraftContent = LivingStandardSeedPageFactory.Create(
            homepage.Title,
            homepage.Summary!,
            [
                ("Bienvenidos", "Crea sitios claros y atractivos con un CMS moderno y rapido."),
                ("Contenido Flexible", "Organiza y publica paginas semanticas con un flujo sencillo para todo el equipo.")
            ],
            backgroundImageUrl: "/media/data-center.png");
        var blogListing = BuildSpanishBlogListingPage(request, blogTranslationGroupId);
        blogListing.DraftContent = LivingStandardSeedPageFactory.Create(
            blogListing.Title,
            blogListing.Summary!,
            [("Articulos Recientes", "Notas, novedades y articulos publicados por el equipo.")]);
        var aboutPage = BuildSpanishAboutPage(aboutTranslationGroupId);
        aboutPage.DraftContent = LivingStandardSeedPageFactory.Create(
            aboutPage.Title,
            aboutPage.Summary!,
            [("Nuestra Mision", "Creamos herramientas que ayudan a los equipos a publicar con claridad y confianza.")]);
        var contactPage = BuildSpanishContactPage(contactTranslationGroupId);
        contactPage.DraftContent = LivingStandardSeedPageFactory.Create(
            contactPage.Title,
            contactPage.Summary!,
            [("Comunicate", "Envianos un mensaje y te responderemos a la brevedad.")],
            callToAction: ("Enviar un mensaje", "mailto:hello@example.com"));

        homepage.SiteId = siteId;
        var homeR = await SavePublishedPageAsync(homepage, cancellationToken);
        if (homeR.IsFailure) Log.Warning("Failed to seed es-MX homepage: {Error}", ErrMsg(homeR));

        blogListing.SiteId = siteId;
        var blogR = await SavePublishedPageAsync(blogListing, cancellationToken);
        if (blogR.IsFailure) Log.Warning("Failed to seed es-MX blog listing page: {Error}", ErrMsg(blogR));

        aboutPage.SiteId = siteId;
        var aboutR = await SavePublishedPageAsync(aboutPage, cancellationToken);
        if (aboutR.IsFailure) Log.Warning("Failed to seed es-MX about page: {Error}", ErrMsg(aboutR));

        contactPage.SiteId = siteId;
        var contactR = await SavePublishedPageAsync(contactPage, cancellationToken);
        if (contactR.IsFailure) Log.Warning("Failed to seed es-MX contact page: {Error}", ErrMsg(contactR));

        await SeedSpanishMexicoNavMenuAsync(
            siteId,
            homepage.Id,
            aboutPage.Id,
            contactPage.Id,
            blogListing.Id,
            navMenuTranslationGroupId,
            cancellationToken);

        await SeedSpanishMexicoFooterAsync(
            siteId,
            aboutPage.Id,
            contactPage.Id,
            blogListing.Id,
            footerTranslationGroupId,
            cancellationToken);
    }

    /// <summary>
    /// Creates the Spanish (Mexico) navigation stream unless that site, culture, and key already exist.
    /// </summary>
    private async Task SeedSpanishMexicoNavMenuAsync(
        long siteId,
        long homepageId,
        long aboutPageId,
        long contactPageId,
        long blogListingPageId,
        long TranslationGroupId,
        CancellationToken cancellationToken)
    {
        const string culture = "es-MX";
        const string navMenuName = "Menu Principal";
        const string navMenuKey = "header-menu";

        var existingMenu = await session.Query<NavMenuDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Culture == culture && x.Key == navMenuKey, cancellationToken);

        if (existingMenu is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var navMenuId = Snowflake.NewId();
        var snapshot = BuildSeedNavMenuSnapshot(
            [
                new NavLink { Key = "home", Label = "Inicio", Href = "/es-mx/", PageId = homepageId, AltText = "Pagina de inicio", Alignment = NavAlignment.Left },
                new NavLink { Key = "about", Label = "Acerca de", Href = "/es-mx/acerca-de", PageId = aboutPageId, AltText = "Acerca de nosotros", Alignment = NavAlignment.Left },
                new NavLink { Key = "contact", Label = "Contacto", Href = "/es-mx/contacto", PageId = contactPageId, AltText = "Contactanos", Alignment = NavAlignment.Left },
                new NavLink { Key = "blog", Label = "Blog", Href = "/es-mx/blog", PageId = blogListingPageId, AltText = "Blog y notas", Alignment = NavAlignment.Left }
            ]);
        snapshot.Validate();

        session.Events.StartStream(
            NavMenuStreams.Menu(navMenuId),
            new object[]
            {
                new NavMenuCreated(siteId, navMenuName, navMenuKey, UserId: null, now, Culture: culture, TranslationGroupId: TranslationGroupId),
                new NavMenuDraftSaved(siteId, navMenuName, navMenuKey, snapshot, UserId: null, now, "Seeded es-MX starter navigation"),
                new NavMenuPublished(siteId, snapshot, UserId: null, now, "Seeded es-MX starter navigation")
            });
    }

    /// <summary>
    /// Creates the Spanish (Mexico) footer stream unless that site, culture, and key already exist.
    /// </summary>
    private async Task SeedSpanishMexicoFooterAsync(
        long siteId,
        long aboutPageId,
        long contactPageId,
        long blogListingPageId,
        long TranslationGroupId,
        CancellationToken cancellationToken)
    {
        const string culture = "es-MX";
        const string footerName = "Pie del sitio";
        const string footerKey = "site-footer";

        var existingFooter = await session.Query<FooterDocument>()
            .FirstOrDefaultAsync(x => x.SiteId == siteId && x.Culture == culture && x.Key == footerKey, cancellationToken);

        if (existingFooter is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var footerId = Snowflake.NewId();
        var snapshot = new FooterSnapshot
        {
            Brand = new FooterBrandSettings
            {
                CompanyName = "Aero CMS",
                Tagline = "Un CMS modular y rapido para sitios modernos en .NET.",
                LogoAltText = "Logo de Aero CMS"
            },
            Legal = FooterLegalSettings.Default with
            {
                CopyrightText = "Aero CMS. Todos los derechos reservados.",
                LegalLinks =
                [
                    new FooterLink("Privacidad", "/es-mx/privacidad"),
                    new FooterLink("Terminos", "/es-mx/terminos"),
                    new FooterLink("Cookies", "/es-mx/cookies")
                ]
            },
            Rows = BuildSeedFooterRows(
                [
                    new FooterLinkGroup
                    {
                        Key = "company",
                        Title = "Compania",
                        Order = 0,
                        Links =
                        [
                            new FooterLink("Acerca de", "/es-mx/acerca-de"),
                            new FooterLink("Contacto", "/es-mx/contacto")
                        ]
                    },
                    new FooterLinkGroup
                    {
                        Key = "content",
                        Title = "Contenido",
                        Order = 1,
                        Links =
                        [
                            new FooterLink("Blog", "/es-mx/blog"),
                            new FooterLink("Mapa del sitio", "/sitemap-es-mx.xml")
                        ]
                    }
                ])
        };
        snapshot.Validate();

        session.Events.StartStream(
            FooterStreams.Footer(footerId),
            new object[]
            {
                new FooterCreated(siteId, footerName, footerKey, "Pie del sitio inicial en es-MX", UserId: null, now, Culture: culture, TranslationGroupId: TranslationGroupId),
                new FooterDraftSaved(siteId, footerName, footerKey, "Pie del sitio inicial en es-MX", snapshot, UserId: null, now, "Seeded es-MX starter footer"),
                new FooterPublished(siteId, snapshot, UserId: null, now, "Seeded es-MX starter footer")
            });
    }

    /// <summary>
    /// Places ordered navigation components into a single responsive full-width row.
    /// </summary>
    private static NavMenuSnapshot BuildSeedNavMenuSnapshot(IReadOnlyList<INavMenuComponent> components)
        => new()
        {
            Layout = NavMenuLayout.Default,
            Responsive = NavMenuResponsiveSettings.Default,
            Style = NavMenuStyleSettings.Default,
            Rows =
            [
                new NavCanvasRow
                {
                    Key = "header-row",
                    Order = 0,
                    Label = "Header",
                    DesktopDisplay = "Flex",
                    TabletDisplay = "Flex",
                    MobileDisplay = "Stack",
                    Columns =
                    [
                        new NavCanvasColumn
                        {
                            Key = "primary-nav",
                            Order = 0,
                            DesktopSpan = 12,
                            TabletSpan = 12,
                            MobileSpan = 12,
                            Blocks = components
                                .Select((component, index) => new NavCanvasBlock
                                {
                                    Key = component.Key,
                                    Order = index,
                                    Component = component
                                })
                                .ToList()
                        }
                    ]
                }
            ]
        };

    /// <summary>
    /// Distributes ordered footer components across responsive columns.
    /// </summary>
    private static List<FooterCanvasRow> BuildSeedFooterRows(IReadOnlyList<IFooterComponent> components)
    {
        var orderedComponents = components.OrderBy(component => component.Order).ToList();
        var columnSpan = orderedComponents.Count switch
        {
            <= 1 => 12,
            2 => 6,
            3 => 4,
            _ => 3
        };

        return
        [
            new FooterCanvasRow
            {
                Key = "footer-main",
                Order = 0,
                Label = "Main footer",
                DesktopDisplay = "Grid",
                TabletDisplay = "Grid",
                MobileDisplay = "Stack",
                Columns = orderedComponents.Select((component, index) => new FooterCanvasColumn
                {
                    Key = $"footer-column-{index + 1}",
                    Order = index,
                    DesktopSpan = columnSpan,
                    TabletSpan = orderedComponents.Count <= 2 ? 6 : 12,
                    MobileSpan = 12,
                    Blocks =
                    [
                        new FooterCanvasBlock
                        {
                            Key = component.Key,
                            Order = 0,
                            Component = component
                        }
                    ]
                }).ToList()
            }
        ];
    }

    /// <summary>
    /// Stages the initial security, general, SEO, and API settings in the current session.
    /// </summary>
    private void SeedDefaultSettings(string defaultCulture)
    {
        var defaults = new List<Setting>
        {
            // Security
            new() { Key = "Manager.DisablePublicAccess", Category = "Security", Value = "false", Type = "bool", Description = "Restrict manager access to internal networks only." },
            new() { Key = "Manager.RequireWasmInstall", Category = "Security", Value = "false", Type = "bool", Description = "Force WASM PWA install for manager access." },
            new() { Key = "Security.MaintenanceMode", Category = "Security", Value = "false", Type = "bool", Description = "Enable maintenance mode for public site." },
            new() { Key = "Security.MaintenanceMessage", Category = "Security", Value = "", Type = "string", Description = "Message shown during maintenance." },

            // General
            new() { Key = "General.DefaultLocale", Category = "General", Value = defaultCulture, Type = "string", Description = "Default culture code." },
            new() { Key = "General.DefaultTimezone", Category = "General", Value = "UTC", Type = "string", Description = "Default timezone." },
            new() { Key = "General.AdminPagination", Category = "General", Value = "20", Type = "int", Description = "Items per page in admin lists." },
            new() { Key = "General.MaxUploadSizeMB", Category = "General", Value = "50", Type = "int", Description = "Max file upload size in MB." },

            // SEO
            new() { Key = "SEO.RobotsTxt", Category = "SEO", Value = "", Type = "text", Description = "Custom robots.txt content." },
            new() { Key = "SEO.DefaultMetaDescription", Category = "SEO", Value = "", Type = "string", Description = "Fallback meta description." },
            new() { Key = "SEO.DefaultOgImage", Category = "SEO", Value = "", Type = "string", Description = "Fallback OG image URL." },

            // API
            new() { Key = "API.CorsOrigins", Category = "API", Value = "", Type = "string", Description = "Comma-separated allowed CORS origins." },
            new() { Key = "API.RateLimitPerMinute", Category = "API", Value = "60", Type = "int", Description = "API rate limit per minute per IP." },
            new() { Key = "API.EnablePublicApi", Category = "API", Value = "true", Type = "bool", Description = "Allow unauthenticated public API access." }
        };

        foreach (var setting in defaults)
        {
            session.Store(setting);
        }
    }

    /// <summary>
    /// Publishes the fallback page and stores aliases for 404, 500, and the retired setup route.
    /// </summary>
    private async Task SeedOopsPageAsync(long siteId, string defaultCulture, CancellationToken ct)
    {
        var oopsPage = new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Standard,
            Slug = "oops",
            Path = "/oops",
            Depth = 0,
            Order = 0,
            Title = "Page Not Found",
            Summary = "The page you're looking for doesn't exist or has been moved.",
            SeoTitle = "Page Not Found",
            DraftContent = LivingStandardSeedPageFactory.Create(
                "Page Not Found",
                "The page you're looking for doesn't exist or has been moved.",
                [("Let's get you back on track", "Check the address or return to the homepage and continue browsing.")],
                callToAction: ("Return Home", "/")),
            PublicationState = ContentPublicationState.Draft,
            Culture = defaultCulture,
            CreatedBy = "seed",
            ModifiedBy = "seed"
        };

        // Stamp siteId on the oopsPage before storing
        oopsPage.SiteId = siteId;
        oopsPage.TranslationGroupId = oopsPage.Id;

        // Use SaveAsync for proper slug reservation
        await SavePublishedPageAsync(oopsPage, ct);

        // Create alias /404 → /oops
        var alias404 = new AliasDocument
        {
            Id = Snowflake.NewId(),
            SiteId = siteId,
            Culture = defaultCulture,
            OldPath = "/404",
            NormalizedOldPath = AliasDocument.NormalizePath("/404"),
            NewPath = "/oops",
            Notes = "Auto-seeded 404 redirect"
        };
        session.Store(alias404);

        // Create alias /500 → /oops
        var alias500 = new AliasDocument
        {
            Id = Snowflake.NewId(),
            SiteId = siteId,
            Culture = defaultCulture,
            OldPath = "/500",
            NormalizedOldPath = AliasDocument.NormalizePath("/500"),
            NewPath = "/oops",
            Notes = "Auto-seeded 500 redirect"
        };
        session.Store(alias500);

        // Create alias /setup → /
        var aliasSetup = new AliasDocument
        {
            Id = Snowflake.NewId(),
            SiteId = siteId,
            Culture = defaultCulture,
            OldPath = "/setup",
            NormalizedOldPath = AliasDocument.NormalizePath("/setup"),
            NewPath = "/",
            Notes = "Auto-seeded setup redirect"
        };
        session.Store(aliasSetup);

        await session.SaveChangesAsync(ct);
        Log.Information("Seeded /oops error page with /404 → /oops, /500 → /oops, /setup → / aliases");
    }


    /// <summary>
    /// Stages top-level web-root media and delegates hydrated-image import to the media service.
    /// </summary>
    /// <remarks>A missing media directory and hydrated-image failures are logged and do not fail setup.</remarks>
    private async Task SeedStarterMediaAsync(CancellationToken ct)
    {
        var mediaDir = Path.Combine(env.WebRootPath, "media");
        if (!Directory.Exists(mediaDir))
        {
            Log.Warning("Media directory not found at {Path}. Skipping media seed.", mediaDir);
            return;
        }

        var mimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".svg"] = "image/svg+xml",
            [".ico"] = "image/x-icon"
        };

        // Seed top-level media files in wwwroot/media/
        foreach (var filePath in Directory.EnumerateFiles(mediaDir))
        {
            var fileName = Path.GetFileName(filePath);
            var ext = Path.GetExtension(filePath);
            var mime = mimeMap.TryGetValue(ext, out var m) ? m : "application/octet-stream";

            var altText = Path.GetFileNameWithoutExtension(fileName)
                .Replace('-', ' ').Replace('_', ' ');

            var media = new MediaAsset
            {
                Id = Snowflake.NewId(),
                FileName = fileName,
                Url = $"/media/{fileName}",
                MimeType = mime,
                FileSize = new FileInfo(filePath).Length,
                AltText = altText,
                IsFolder = false
            };

            session.Store(media);
        }

        // Seed hydrated Pexels images with full attribution metadata
        var hydratedResult = await mediaService.SeedFromDirectoryAsync("hydrated-images", ct);
        if (hydratedResult.IsFailure)
        {
            var error = hydratedResult is Result<int, AeroError>.Failure f
                ? (f.Error is AeroError.Error e ? e.msg : "Unknown error")
                : "Failed to seed hydrated images";
            Log.Warning("Failed to seed hydrated images: {Error}", error);
        }
        else
        {
            var count = hydratedResult is Result<int, AeroError>.Ok ok ? ok.Value : 0;
            Log.Information("Seeded {Count} hydrated media assets", count);
        }

        Log.Information("Seeded top-level media assets from {Path}",
            Directory.GetFiles(mediaDir).Length);
    }

    /// <summary>
    /// Initializes persisted state for every descriptor supplied to the setup service.
    /// </summary>
    private async Task SaveModuleStateAsync(CancellationToken cancellationToken)
    {
        await moduleInitializationService.InitializeModulesAsync(moduleDescriptors, cancellationToken);
    }

    /// <summary>
    /// Builds the default-culture home-page metadata before HTML content is attached.
    /// </summary>
    private static PageDocument BuildHomepage(SeedDatabaseRequest request)
    {
        var homepageSummary = $"A high-performance, block-based content platform built for scale. Experience the next generation of web management with {Normalize(request.SiteName)}.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Homepage,
            Slug = "/",
            Path = "/",
            Depth = 0,
            Order = 0,
            Title = Normalize(request.HomepageTitle),
            Summary = homepageSummary,
            SeoTitle = $"{Normalize(request.HomepageTitle)} | {Normalize(request.SiteName)}",
            SeoDescription = $"Welcome to {Normalize(request.SiteName)}. A modern CMS built on .NET 10, AeroDB, and Microsoft Orleans.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the default-culture blog listing metadata before HTML content is attached.
    /// </summary>
    private static PageDocument BuildBlogListingPage(SeedDatabaseRequest request)
    {
        return new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.BlogListing,
            Slug = "blog",
            Path = "/blog",
            Depth = 0,
            Order = 0,
            Title = Normalize(request.BlogName),
            Summary = $"Updates and field notes from {Normalize(request.SiteName)}.",
            SeoTitle = $"{Normalize(request.BlogName)} | {Normalize(request.SiteName)}",
            SeoDescription = $"Read the latest posts from {Normalize(request.SiteName)}.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the default-culture about-page metadata.
    /// </summary>
    private static PageDocument BuildAboutPage()
    {
        const string summary = "Learn more about our mission and the team behind the platform.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Standard,
            Slug = "about",
            Path = "/about",
            Depth = 0,
            Order = 0,
            Title = "About Us",
            Summary = summary,
            SeoTitle = "About Us | Aero CMS",
            SeoDescription = "Discover our story, mission, and commitment to building great digital experiences.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the default-culture contact-page metadata.
    /// </summary>
    private static PageDocument BuildContactPage()
    {
        const string summary = "Get in touch with our team.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Standard,
            Slug = "contact",
            Path = "/contact",
            Depth = 0,
            Order = 0,
            Title = "Contact Us",
            Summary = summary,
            SeoTitle = "Contact Us | Aero CMS",
            SeoDescription = "Have questions? We'd love to hear from you. Send us a message today.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the default-culture privacy-page metadata.
    /// </summary>
    private static PageDocument BuildPrivacyPage()
    {
        const string summary = "Our commitment to your privacy and data protection.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Standard,
            Slug = "privacy",
            Path = "/privacy",
            Depth = 0,
            Order = 0,
            Title = "Privacy Policy",
            Summary = summary,
            SeoTitle = "Privacy Policy | Aero CMS",
            SeoDescription = "Learn how we collect, use, and protect your personal information.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the default-culture terms-page metadata.
    /// </summary>
    private static PageDocument BuildTermsPage()
    {
        const string summary = "Terms and conditions governing the use of our site.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Standard,
            Slug = "terms",
            Path = "/terms",
            Depth = 0,
            Order = 0,
            Title = "Terms of Service",
            Summary = summary,
            SeoTitle = "Terms of Service | Aero CMS",
            SeoDescription = "Read the terms and conditions for using our site.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the default-culture cookie-policy metadata.
    /// </summary>
    private static PageDocument BuildCookiesPage()
    {
        const string summary = "How we use cookies to improve your browsing experience.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            Kind = PageKind.Standard,
            Slug = "cookies",
            Path = "/cookies",
            Depth = 0,
            Order = 0,
            Title = "Cookie Policy",
            Summary = summary,
            SeoTitle = "Cookie Policy | Aero CMS",
            SeoDescription = "Learn how we use cookies and how you can manage them.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the Spanish (Mexico) home-page variant for an existing translation group.
    /// </summary>
    private static PageDocument BuildSpanishHomepage(
        SeedDatabaseRequest request,
        long TranslationGroupId)
    {
        var title = $"Bienvenido a {Normalize(request.SiteName)}";
        var summary = "Una plataforma de contenido modular, rapida y preparada para sitios modernos.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            SiteId = 0,
            TranslationGroupId = TranslationGroupId,
            Culture = "es-MX",
            Kind = PageKind.Homepage,
            Slug = "/",
            Path = "/",
            Depth = 0,
            Order = 0,
            Title = title,
            Summary = summary,
            SeoTitle = $"{title} | {Normalize(request.SiteName)}",
            SeoDescription = $"Bienvenido a {Normalize(request.SiteName)}.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the Spanish (Mexico) blog-listing variant for an existing translation group.
    /// </summary>
    private static PageDocument BuildSpanishBlogListingPage(
        SeedDatabaseRequest request,
        long TranslationGroupId)
    {
        return new PageDocument
        {
            Id = Snowflake.NewId(),
            TranslationGroupId = TranslationGroupId,
            Culture = "es-MX",
            Kind = PageKind.BlogListing,
            Slug = "blog",
            Path = "/blog",
            Depth = 0,
            Order = 0,
            Title = "Blog",
            Summary = $"Novedades y notas de {Normalize(request.SiteName)}.",
            SeoTitle = $"Blog | {Normalize(request.SiteName)}",
            SeoDescription = $"Lee las publicaciones mas recientes de {Normalize(request.SiteName)}.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the Spanish (Mexico) about-page variant for an existing translation group.
    /// </summary>
    private static PageDocument BuildSpanishAboutPage(long TranslationGroupId)
    {
        const string title = "Acerca de";
        const string summary = "Conoce nuestra mision y la historia detras de la plataforma.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            TranslationGroupId = TranslationGroupId,
            Culture = "es-MX",
            Kind = PageKind.Standard,
            Slug = "acerca-de",
            Path = "/acerca-de",
            Depth = 0,
            Order = 0,
            Title = title,
            Summary = summary,
            SeoTitle = "Acerca de | Aero CMS",
            SeoDescription = "Conoce nuestra historia y nuestra forma de construir experiencias digitales.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds the Spanish (Mexico) contact-page variant for an existing translation group.
    /// </summary>
    private static PageDocument BuildSpanishContactPage(long TranslationGroupId)
    {
        const string title = "Contacto";
        const string summary = "Ponte en contacto con nuestro equipo.";

        return new PageDocument
        {
            Id = Snowflake.NewId(),
            TranslationGroupId = TranslationGroupId,
            Culture = "es-MX",
            Kind = PageKind.Standard,
            Slug = "contacto",
            Path = "/contacto",
            Depth = 0,
            Order = 0,
            Title = title,
            Summary = summary,
            SeoTitle = "Contacto | Aero CMS",
            SeoDescription = "Tienes preguntas? Envia un mensaje al equipo.",
            PublicationState = ContentPublicationState.Published
        };
    }


    /// <summary>
    /// Builds tags and randomized starter posts without storing them.
    /// </summary>
    /// <remarks>Like counts and three-tag selections are intentionally non-deterministic.</remarks>
    private static (IReadOnlyList<PostDocument> Posts, IReadOnlyList<Tag> Tags) BuildStarterBlogContent(SeedDatabaseRequest request)
    {
        var random = new Random();
        var tags = CreateTags();
        var tagMap = tags.ToDictionary(t => t.Name, t => t.Id);

        const string H = "/media/hydrated-images/";
        var techPool = new[]
        {
            $"{H}pexels-14057520.jpg", $"{H}pexels-14928877.jpg",
            $"{H}pexels-16038076.jpg", $"{H}pexels-33402136.jpg",
            $"{H}pexels-36586080.jpg", $"{H}pexels-37298148.jpg",
            $"{H}pexels-35774019.jpg", $"{H}pexels-36713338.jpg",
            $"{H}pexels-36009140.jpg", $"{H}pexels-36578877.jpg"
        };

        var posts = new List<PostDocument>
        {
            BuildPost(Snowflake.NewId(), "welcome-to-our-new-platform", "Welcome to Our New Platform",
                "Launching a better way to share updates and connect with our community.",
                "# Welcome to Our New Platform\n\nWe're thrilled to unveil our new digital home. This platform marks a significant step forward in how we communicate, share, and engage with you&#8212;our community.\n\nBuilt from the ground up with modern technology, this site represents our commitment to speed, accessibility, and user experience. Every pixel has been crafted with care, every feature designed with purpose.\n\nAs you explore, you'll find our blog at the heart of this platform. This is where we'll share insights, announce updates, and tell the stories behind our work.",
                [tagMap["announcements"], tagMap["community"]],
                $"{H}pexels-34077030.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "behind-the-scenes-building-a-content-management-system", "Behind the Scenes: Building a Content Management System",
                "A deep dive into the technical decisions that power our content platform.",
                "# Behind the Scenes: Building a Content Management System\n\nCreating a CMS from scratch is both exhilarating and challenging. In this post, we're pulling back the curtain on the architectural decisions that shape our platform.\n\nWe chose **.NET 10** for its performance and robust ecosystem. **AeroDB** provides the document storage layer, giving us flexibility in our schema while maintaining query performance. The block-based content model allows for rich, modular layouts.\n\nThe result is a system that's fast, flexible, and fun to use. Stay tuned for more technical deep dives.\n\n> The best systems are those that disappear, letting creators focus on what matters&#8212;creating.\n\n&#8212; Our Team",
                [tagMap["architecture"], tagMap["cms"], tagMap[".net"]],
                $"{H}pexels-27254940.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "design-principles-for-modern-web-platforms", "Design Principles for Modern Web Platforms",
                "How we approach design to create interfaces that feel natural and intuitive.",
                "# Design Principles for Modern Web Platforms\n\nGood design is invisible. It works so well that users never notice the effort behind it. That's the standard we hold ourselves to.\n\nOur design philosophy centers on three pillars: **clarity**, **speed**, and **delightful interactions**. Every component we build must serve a purpose, load instantly, and feel natural to use.\n\nWe believe in progressive enhancement&#8212;starting with a solid, accessible foundation and layering on polished experiences for capable browsers. This ensures everyone gets a great experience, regardless of device or connection.",
                [tagMap["design"], tagMap["ux"]],
                $"{H}pexels-37178225.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "choosing-orleans-for-distributed-systems", "Choosing Orleans for Distributed Systems",
                "Why we selected Orleans as our service framework and what we've learned.",
                "# Choosing Orleans for Distributed Systems\n\nWhen architecting a platform that needs to scale, the choice of service framework is critical. After evaluating several options, we chose **Microsoft Orleans** for its balance of simplicity and power.\n\nOrleans brings the actor model to .NET in a way that feels natural. Grains provide a clean mental model for stateful services, while the runtime handles distribution, persistence, and scaling concerns.\n\nWhat sold us most was the developer experience. The programming model is intuitive, the debugging story is solid, and the documentation is excellent. After months of building, we haven't looked back.\n\n> Orleans lets us think about business logic, not infrastructure. That's exactly what we needed.",
                [tagMap["orleans"], tagMap["distributed-systems"], tagMap[".net"]],
                $"{H}pexels-36723270.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "content-strategy-blogging-best-practices", "Content Strategy: Blogging Best Practices",
                "Tips for maintaining a consistent publishing cadence and quality content.",
                "# Content Strategy: Blogging Best Practices\n\nStarting a blog is easy. Maintaining one is hard. Here's what we've learned about building a sustainable content practice.\n\n**First, quality beats quantity.** We'd rather publish one excellent article than three mediocre ones. Each post should add genuine value&#8212;whether that's solving a problem, sharing insight, or telling a compelling story.\n\n**Second, consistency builds trust.** When readers know they can expect content on a regular schedule, they become loyal followers. We publish weekly, every Tuesday morning.\n\n**Finally, engage with your audience.** Respond to comments, answer questions, and acknowledge feedback. The best blogs are conversations, not monologues.",
                [tagMap["content-strategy"], tagMap["blogging"]],
                $"{H}pexels-36391026.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "scaling-postgres-for-high-traffic", "Scaling Postgres for High Traffic",
                "Lessons learned from optimizing our database layer for performance.",
                "# Scaling Postgres for High Traffic\n\nPostgreSQL is remarkably capable, but pushing it to its limits requires thoughtfulness. Here's how we handle traffic spikes without breaking a sweat.\n\n**Indexing is everything.** Every query was analyzed and optimized. We use covering indexes for read-heavy paths, partial indexes for filtered queries, and GIN indexes for full-text search. The difference in performance is night and day.\n\n**Connection pooling is essential.** With AeroDB's pooling built-in, we reuse connections efficiently, avoiding the overhead of establishing new connections for each request.\n\n**And always, always monitor.** Query stats, connection counts, cache hit ratios&#8212;know your system's vital signs before problems arise.\n\n> Premature optimization is the root of all evil. But so is ignoring performance until it bites you.",
                [tagMap["postgresql"], tagMap["performance"], tagMap["database"]],
                $"{H}pexels-34043108.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "embracing-blazor-and-htmx", "Embracing Blazor and HTMX for Interactive UIs",
                "How we combine server-side rendering with progressive enhancement.",
                "# Embracing Blazor and HTMX for Interactive UIs\n\nThe web development landscape is fractured between full-page reload purists and SPA enthusiasts. We found a middle ground that gives us the best of both worlds.\n\n**Blazor** provides rich, interactive components in C#. We use Radzen's component library for rapid development of complex UI elements&#8212;data grids, editors, dialogs. Everything works without writing JavaScript.\n\n**HTMX** adds the dynamic touch. With a few attributes, we enable seamless partial page updates, infinite scroll, and real-time interactions. The pattern is simple: server renders HTML, HTMX swaps it in. No client-side routing, no hydration complexity.\n\nThe result is a site that loads fast, works without JavaScript, yet feels modern and responsive. That's the sweet spot.",
                [tagMap["blazor"], tagMap["htmx"], tagMap["frontend"]],
                $"{H}pexels-13860372.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "open-telemetry-observability-at-scale", "OpenTelemetry: Observability at Scale",
                "Implementing distributed tracing and metrics in our platform.",
                "# OpenTelemetry: Observability at Scale\n\nWhen systems grow complex, intuition fails. You need data. That's where observability comes in, and **OpenTelemetry** is our tool of choice.\n\nWe instrument everything with OpenTelemetry: requests, database calls, cache operations, message processing. Using **Serilog** as our logging foundation and **OpenObserve** for storage and visualization, we have complete visibility into system behavior.\n\nTraces let us follow requests across service boundaries, finding where latency bubbles up. Metrics show trends over time&#8212;error rates, response times, throughput. Logs provide the detail when something goes wrong.\n\nThe investment pays dividends every incident. Instead of guessing, we know exactly what happened and where.\n\n> Without observability, you're flying blind. With it, you can debug with confidence.",
                [tagMap["observability"], tagMap["opentelemetry"], tagMap["monitoring"]],
                $"{H}pexels-29243214.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "getting-started-with-aero-cms", "Getting Started with Aero CMS",
                $"Use {Normalize(request.SiteName)} to publish your first update in minutes.",
                $"# Getting Started with Aero CMS\n\nYour site is live with a homepage and blog. Use this starter post as the baseline for your first editorial update in **{Normalize(request.SiteName)}**.\n\nThe platform is designed to be intuitive. Create pages, arrange blocks, publish content&#8212;all without touching code. But if you need to extend functionality, the architecture is open and extensible.\n\nBrowse the admin panel to explore what's possible. Add new pages, create blog posts, arrange content blocks, customize the design. This is just the beginning.",
                [tagMap["cms"], tagMap["tutorial"], tagMap["guide"]],
                $"{H}pexels-12491947.jpg", random.Next(1, 1001)),
            BuildPost(Snowflake.NewId(), "the-future-of-content-management", "The Future of Content Management",
                "Where we see the CMS space heading and what it means for content creators.",
                "# The Future of Content Management\n\nThe CMS landscape is evolving. Traditional monoliths give way to composable architectures. Proprietary formats yield to open standards. We're excited about where it's heading.\n\nBlock-based content models are becoming the norm. Instead of rigid templates, editors compose with reusable components. This flexibility unlocks creativity while maintaining consistency.\n\n**Headless is hot, but we're skeptical** of the one-size-fits-all pitch. Most teams need a cohesive system, not a puzzle of separate products. We believe in integrated solutions that just work.\n\nThe future is fast, accessible, and focused on the creator experience. That's the future we're building toward.",
                [tagMap["future"], tagMap["cms"], tagMap["trends"]],
                $"{H}pexels-37292919.jpg", random.Next(1, 1001))
        };

        // Add 20 more posts
        var techTopics = new[]
        {
            "Cloud Native Architecture", "Microservices Patterns", "WebAssembly and Blazor", "AI Integration in CMS",
            "Performance Tuning .NET 10", "Durable Functions Workflows", "Message Queues with Wolverine", "GraphQL API Design",
            "Modern CSS with Tailwind", "Responsive Design Best Practices", "Unit Testing with TUnit", "Infrastructure as Code",
            "Identity and Access Management", "Distributed Caching Strategies", "Real-time Apps with SignalR", "SEO Optimization Guide",
            "Static Site Generation", "Dynamic Block Rendering", "Headless CMS Advantages", "The Creator Economy Tools"
        };

        var allTagIds = tags.Select(t => t.Id).ToList();

        for (int i = 0; i < 20; i++)
        {
            var topic = techTopics[i];
            var slugTopic = topic.ToLowerInvariant().Replace(' ', '-');
            
            posts.Add(BuildPost(Snowflake.NewId(),
                $"{slugTopic}",
                topic,
                $"Exploring the nuances of {topic} in the context of modern enterprise applications.",
                $"# {topic}\n\n{topic} is a crucial area of modern software development. In this deep dive, we examine the core principles and how they apply to building high-performance systems.\n\nAs we move towards more distributed and resilient architectures, understanding the underlying patterns becomes even more important.",
                allTagIds.OrderBy(_ => random.Next()).Take(3).ToList(),
                techPool[i % techPool.Length],
                random.Next(1, 500)));
        }

        return (posts, tags);
    }

    /// <summary>
    /// Builds a published starter post with matching SEO metadata.
    /// </summary>
    private static PostDocument BuildPost(long id, string slug, string title, string excerpt, string markdown, List<long>? tagIds = null, string? imageUrl = null, int likes = 0) =>
        new PostDocument
        {
            Id = id,
            Slug = slug,
            Title = title,
            Excerpt = excerpt,
            SeoTitle = title,
            SeoDescription = excerpt,
            MarkdownContent = markdown,
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            TagIds = tagIds ?? [],
            ImageUrl = imageUrl,
            Likes = likes
        };

    /// <summary>
    /// Trims setup-supplied display text before it is copied into starter content.
    /// </summary>
    private static string Normalize(string value)
        => value.Trim();

    /// <summary>
    /// Extracts a page workflow error message for seed logging.
    /// </summary>
    private static string ErrMsg(Result<PageDocument, AeroError> r) =>
        r is Result<PageDocument, AeroError>.Failure f && f.Error is AeroError.Error e ? e.msg : "seed save failed";

    /// <summary>
    /// Saves a page draft and immediately publishes it when the save succeeds.
    /// </summary>
    private async Task<Result<PageDocument, AeroError>> SavePublishedPageAsync(
        PageDocument page,
        CancellationToken cancellationToken)
    {
        var saved = await pageContentService.SaveAsync(page, cancellationToken);
        if (saved is Result<PageDocument, AeroError>.Failure saveFailure)
        {
            return saveFailure;
        }

        var published = await pagePublishingWorkflowService.PublishNowAsync(page.Id, cancellationToken);
        return published is Result<bool, AeroError>.Failure publishFailure
            ? Prelude.Fail<PageDocument, AeroError>(publishFailure.Error)
            : saved;
    }

    /// <summary>
    /// Assigns a page culture and initializes its translation group to its own identifier.
    /// </summary>
    private static void StampPageCulture(PageDocument page, string culture)
    {
        page.Culture = culture;
        page.TranslationGroupId ??= page.Id;
    }

    /// <summary>
    /// Determines whether a separate Spanish (Mexico) variant should be seeded.
    /// </summary>
    private static bool ShouldSeedSpanishMexico(string defaultCulture, IReadOnlyList<string> supportedCultures)
        => !string.Equals(defaultCulture, "es-MX", StringComparison.OrdinalIgnoreCase)
           && supportedCultures.Any(culture => string.Equals(culture, "es-MX", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Canonicalizes culture names, removes duplicates, and ensures the default is supported.
    /// </summary>
    private static (string DefaultCulture, List<string> SupportedCultures) NormalizeCultureSettings(
        string? defaultCulture,
        IEnumerable<string>? supportedCultures)
    {
        var normalizedDefault = NormalizeCultureName(defaultCulture);
        var cultures = (supportedCultures ?? [])
            .Select(NormalizeCultureName)
            .Where(culture => !string.IsNullOrWhiteSpace(culture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!cultures.Any(culture => string.Equals(culture, normalizedDefault, StringComparison.OrdinalIgnoreCase)))
        {
            cultures.Insert(0, normalizedDefault);
        }

        return (normalizedDefault, cultures.Count == 0 ? [normalizedDefault] : cultures);
    }

    /// <summary>
    /// Returns a canonical culture name or the site default when the input is blank or invalid.
    /// </summary>
    private static string NormalizeCultureName(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return SitesModel.DefaultCultureName;
        }

        try
        {
            return CultureInfo.GetCultureInfo(culture.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return SitesModel.DefaultCultureName;
        }
    }

    /// <summary>
    /// Builds the fixed starter tag catalog with new Snowflake identifiers.
    /// </summary>
    private static List<Tag> CreateTags()
    {
        var tagNames = new[]
        {
            "announcements", "community", "architecture", "cms", ".net", "design", "ux", "orleans",
            "distributed-systems", "content-strategy", "blogging", "postgresql", "performance",
            "database", "blazor", "htmx", "frontend", "observability", "opentelemetry", "monitoring",
            "tutorial", "guide", "future", "trends"
        };
        return tagNames.Select(name => new Tag
        {
            Id = Snowflake.NewId(),
            Name = name,
            Slug = name.ToLowerInvariant().Replace(' ', '-')
        }).ToList();
    }

    /// <summary>
    /// Builds Spanish (Mexico) translations for recognized starter tags.
    /// </summary>
    private static IReadOnlyList<TagTranslation> BuildSpanishMexicoTagTranslations(IReadOnlyList<Tag> tags)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["announcements"] = "anuncios",
            ["community"] = "comunidad",
            ["architecture"] = "arquitectura",
            ["cms"] = "cms",
            [".net"] = ".net",
            ["design"] = "diseno",
            ["ux"] = "ux",
            ["orleans"] = "orleans",
            ["distributed-systems"] = "sistemas distribuidos",
            ["content-strategy"] = "estrategia de contenido",
            ["blogging"] = "blogging",
            ["postgresql"] = "postgresql",
            ["performance"] = "rendimiento",
            ["database"] = "base de datos",
            ["blazor"] = "blazor",
            ["htmx"] = "htmx",
            ["frontend"] = "frontend",
            ["observability"] = "observabilidad",
            ["opentelemetry"] = "opentelemetry",
            ["monitoring"] = "monitoreo",
            ["tutorial"] = "tutorial",
            ["guide"] = "guia",
            ["future"] = "futuro",
            ["trends"] = "tendencias"
        };

        return tags
            .Where(tag => names.ContainsKey(tag.Name))
            .Select(tag => new TagTranslation
            {
                Id = Snowflake.NewId(),
                TagId = tag.Id,
                Culture = "es-MX",
                Name = names[tag.Name],
                Description = $"Etiqueta para contenido sobre {names[tag.Name]}."
            })
            .ToList();
    }

    /// <summary>
    /// Builds the initial published documentation hierarchy without storing it.
    /// </summary>
    private List<DocsPage> BuildStarterDocsContent()
    {
        var docs = new List<DocsPage>();
        
        // 1. Root Documentation Page
        var rootDoc = new DocsPage
        {
            Id = Snowflake.NewId(),
            Title = "Aero CMS Documentation",
            Slug = "docs",
            Summary = "Official developer documentation for Aero CMS—the high-performance, AOT-compatible content platform.",
            MarkdownContent = @"# Aero CMS Documentation

Welcome to the official developer documentation for **Aero CMS**. Use the guides below to explore our architecture and features.

## Getting Started
Learn how to install and configure Aero CMS for your next project.

## Advanced Guides
Deep dives into theming, localization, and custom module development.

## API Reference
Technical documentation for integrating with the Aero CMS core services.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0,
            HeaderImageUrl = "/media/hydrated-images/pexels-30556872.jpg",
            SeoTitle = "Aero CMS Documentation - Knowledge Base",
            SeoDescription = "Learn how to build and extend Aero CMS with our comprehensive developer guides."
        };
        docs.Add(rootDoc);

        // 2. Getting Started Chapter
        var gettingStarted = new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = rootDoc.Id,
            Title = "Getting Started",
            Slug = "docs/getting-started",
            Summary = "Everything you need to know to get Aero CMS up and running.",
            MarkdownContent = "# Getting Started\n\nWelcome to Aero CMS! This chapter covers the basics of settting up your development environment. Aero CMS is designed to be lean and fast, leveraging the latest .NET features.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0
        };
        docs.Add(gettingStarted);

        // 3. Installation Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = gettingStarted.Id,
            Title = "Installation",
            Slug = "docs/getting-started/installation",
            Summary = "Step-by-step guide to installing Aero CMS via CLI or source.",
            MarkdownContent = "# Installation\n\nTo install Aero CMS, you can clone the repository directly or use our upcoming dotnet new templates.\n\n```bash\ngit clone https://github.com/microbian-systems/AeroCMS.git\ncd AeroCMS\ndotnet build\n```",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0
        });

        // 4. Configuration Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = gettingStarted.Id,
            Title = "Configuration",
            Slug = "docs/getting-started/configuration",
            Summary = "How to configure your database, caching, and storage providers.",
            MarkdownContent = "# Configuration\n\nAll configuration is handled through standard .NET configuration providers. The primary settings are located in `appsettings.json` and can be overridden by environment variables.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 1
        });

        // 5. Guides Chapter
        var guides = new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = rootDoc.Id,
            Title = "Guides",
            Slug = "docs/guides",
            Summary = "Practical tutorials for common tasks like theming and localization.",
            MarkdownContent = "# Guides\n\nOur guides are designed to help you solve real-world problems with Aero CMS. Whether you're building a simple blog or a complex enterprise portal, you'll find what you need here.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 1
        };
        docs.Add(guides);

        // 6. Theming Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = guides.Id,
            Title = "Theming",
            Slug = "docs/guides/theming",
            Summary = "Learn how to use Tailwind CSS and CSS variables to style your site.",
            MarkdownContent = "# Theming\n\nAero CMS uses a modern CSS utility approach. You can customize the look and feel by modifying the `tailwind.config.js` or providing custom global styles via the admin interface.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0
        });

        // 7. Localization Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = guides.Id,
            Title = "Localization",
            Slug = "docs/guides/localization",
            Summary = "Setting up multi-lingual sites and managing translations.",
            MarkdownContent = "# Localization\n\nInternationalization is built into the core. You can define multiple languages and provide translated versions of all your content, including pages, posts, and documentation.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 1
        });

        // 8. API Reference Chapter
        var api = new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = rootDoc.Id,
            Title = "API Reference",
            Slug = "docs/api",
            Summary = "Detailed technical specifications for the Aero CMS core API.",
            MarkdownContent = "# API Reference\n\nThe Aero CMS API provides programmatic access to all system functionality. This reference documentation covers the REST endpoints and the underlying C# service contracts.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 2
        };
        docs.Add(api);

        // 9. Authentication Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = api.Id,
            Title = "Authentication",
            Slug = "docs/api/authentication",
            Summary = "Securing your API requests with Bearer tokens and OAuth.",
            MarkdownContent = "# API Authentication\n\nAero CMS uses JWT-based authentication for its API. To make requests, you must obtain a token and include it in the `Authorization` header of your HTTP requests.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 0
        });

        // 10. Content Management Section
        docs.Add(new DocsPage
        {
            Id = Snowflake.NewId(),
            ParentId = api.Id,
            Title = "Content Mgmt",
            Slug = "docs/api/content-management",
            Summary = "Programmatically creating and updating pages and blocks.",
            MarkdownContent = "# Content Management Service\n\nThe `IContentService` is the primary interface for managing content entities. It provides methods for creating, retrieving, updating, and deleting documents across all modules.",
            PublishedOn = DateTimeOffset.UtcNow,
            PublicationState = ContentPublicationState.Published,
            Order = 1
        });

        return docs;
    }
}
