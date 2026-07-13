using Aero.Cms.Abstractions.Services;
using Aero.Cms.Modules.Posts;
using Aero.Cms.Modules.Commerce.Data;
using Aero.Cms.Modules.Media;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Html;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Tenant;
using Aero.Cms.Modules.Modules.Services;
using Aero.Core.Http;
using Aero.Models.Entities;
using Aero.Core.Identity;
using AeroDB.Sable;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wolverine;
using Aero.Cms.Generated;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Modular;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Defines an interface for IServerTargetSetupExecutor.
/// </summary>
public interface IServerTargetSetupExecutor
{
        /// <summary>
    /// ExecuteAsync method.
    /// </summary>
Task<SeedDatabaseResult> ExecuteAsync(
        string serverConnectionString,
        SeedDatabaseRequest request,
        IReadOnlyList<ModuleDescriptor>? descriptors = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a class for ServerTargetSetupExecutor.
/// </summary>
public sealed class ServerTargetSetupExecutor(
    IServiceProvider rootServiceProvider,
    ILogger<ServerTargetSetupExecutor> logger,
    IBootstrapCompletionWriter bootstrapCompletionWriter) : IServerTargetSetupExecutor
{
        /// <summary>
    /// ExecuteAsync method.
    /// </summary>
public async Task<SeedDatabaseResult> ExecuteAsync(
        string serverConnectionString,
        SeedDatabaseRequest request,
        IReadOnlyList<ModuleDescriptor>? descriptors = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverConnectionString);
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation("=== ServerTargetSetup starting ===");
        logger.LogInformation("Connection: {Connection} (masked)", serverConnectionString[..Math.Min(20, serverConnectionString.Length)] + "...");
        logger.LogInformation("Seed request: siteName={SiteName}, adminEmail={AdminEmail}, hostname={Hostname}",
            request.SiteName, request.AdminEmail, request.Hostname);

        logger.LogInformation("Step 1/6: Running EF Core migrations...");
        await MigrateAsync(serverConnectionString, cancellationToken);
        logger.LogInformation("Step 1/6: Migrations complete.");

        logger.LogInformation("Step 2/6: Creating AeroDB DocumentStore...");
        var opts = new global::AeroDB.Sable.StoreOptions();
        opts.Endpoint = serverConnectionString;
        opts.DatabaseSchemaName = global::Aero.Core.Data.Schemas.Aero;
        opts.UseAeroGeneratedJsonContext();
        opts.Events.StreamIdentity = global::AeroDB.Sable.StreamIdentity.AsString;
        opts.Schema.For<AeroRole>().Identity(x => x.Id);
        opts.Schema.For<AeroUser>().Identity(x => x.Id);

        foreach (var configure in rootServiceProvider.GetServices<IConfigureAeroDB>())
        {
            configure.Configure(rootServiceProvider, opts);
        }

        var store = new DocumentStore(opts);

        logger.LogInformation("Step 2/6: AeroDB DocumentStore created with {ConfigCount} IConfigureAeroDB registrations.",
            rootServiceProvider.GetServices<IConfigureAeroDB>().Count());

        logger.LogInformation("Step 3/6: Creating session and services...");
        await using var session = await store.LightweightSessionAsync();
        var bus = rootServiceProvider.GetRequiredService<IMessageBus>();
        var noopSiteContext = new NoopSiteContext();
        var pageContentService = new AeroPageContentService(session, bus, noopSiteContext,
            rootServiceProvider.GetRequiredService<ILogger<AeroPageContentService>>(),
            rootServiceProvider.GetRequiredService<IHtmlContentValidator>(),
            rootServiceProvider.GetRequiredService<IStyleCompiler>(),
            rootServiceProvider.GetRequiredService<IStyleProfile>());
        var pagePublishingWorkflowService = new PagePublishingWorkflowService(
            session,
            bus,
            rootServiceProvider.GetRequiredService<IHtmlContentValidator>(),
            rootServiceProvider.GetRequiredService<IStyleCompiler>(),
            rootServiceProvider.GetRequiredService<IStyleProfile>(),
            rootServiceProvider.GetRequiredService<ILogger<PagePublishingWorkflowService>>());
        var blogPostContentService = new PostContentService(session, noopSiteContext);
        var userStore = CreateUserStore(store, rootServiceProvider);
        var userManager = CreateUserManager(userStore, rootServiceProvider);
        var identityBootstrapper = new SetupIdentityBootstrapper(userManager);
        
        var mediaService = rootServiceProvider.GetRequiredService<IMediaService>();
        var commerceSeedService = rootServiceProvider.GetRequiredService<ICommerceSeedService>();
        var tenantService = rootServiceProvider.GetRequiredService<ITenantService>();
        var siteService = rootServiceProvider.GetRequiredService<ISiteService>();
        var apiKeyService = rootServiceProvider.GetRequiredService<IApiKeyService>();

        logger.LogInformation("Step 3/6: Services resolved. PageContent={PageSvc}, Media={MediaSvc}, Tenant={TenantSvc}",
            pageContentService.GetType().Name, mediaService.GetType().Name, tenantService.GetType().Name);

        var moduleInitializationService = new ModuleInitializationService(
            new ModuleStateStore(session));

        logger.LogInformation("Step 4/6: Creating SeedDatabaseService...");

        var env = rootServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var seedService = new SeedDatabaseService(
            session,
            env,
            identityBootstrapper,
            pageContentService,
            pagePublishingWorkflowService,
            blogPostContentService,
            mediaService,
            //commerceSeedService,
            commerceSeedService,
            moduleInitializationService,
            bootstrapCompletionWriter,
            tenantService,
            siteService,
            apiKeyService,
            descriptors ?? Array.Empty<ModuleDescriptor>());

        logger.LogInformation("Step 5/6: Executing seed (descriptors={DescriptorCount})...",
            descriptors?.Count ?? 0);
        var result = await seedService.CompleteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            logger.LogWarning("Server-targeted setup seeding failed: {Errors}", string.Join("; ", result.Errors));
            logger.LogError("=== ServerTargetSetup FAILED after seeding ===");
            return result;
        }

        logger.LogInformation("Step 5/6: Seed completed successfully. admin={CreatedAdmin}, roles={CreatedRoles}, tenant={CreatedTenant}, site={CreatedSite} (siteId={SiteId})",
            result.CreatedAdmin, result.CreatedRoles, result.CreatedTenant, result.CreatedSite, result.SiteId);

        logger.LogInformation("Step 6/6: Writing bootstrap completion marker...");
        await bootstrapCompletionWriter.MarkCompleteAsync(cancellationToken);
        logger.LogInformation("Step 6/6: Bootstrap marker written.");
        logger.LogInformation("=== ServerTargetSetup COMPLETE (siteId={SiteId}, tenantId={TenantId}) ===",
            result.SiteId, result.TenantId);
        return result;
    }

    private Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        // EF Core Npgsql migrations removed.
        // All persistence now handled by AeroDB.Sable (IDocumentSession).
        return Task.CompletedTask;
    }

    private static IUserStore<AeroUser> CreateUserStore(IDocumentStore store, IServiceProvider services)
    {
        return new AeroDBUserStore<AeroUser, AeroRole, long>(
            store,
            (ILogger<AeroDBUserStore<AeroUser, AeroRole, long>>)services.GetRequiredService(typeof(ILogger<AeroDBUserStore<AeroUser, AeroRole, long>>)));
    }

    private static UserManager<AeroUser> CreateUserManager(IUserStore<AeroUser> userStore, IServiceProvider services)
    {
        var options = Options.Create(new IdentityOptions());
        var passwordHasher = new PasswordHasher<AeroUser>();
        var userValidators = Array.Empty<IUserValidator<AeroUser>>();
        var passwordValidators = Array.Empty<IPasswordValidator<AeroUser>>();
        var keyNormalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();
        var logger = NullLogger<UserManager<AeroUser>>.Instance;
        return new UserManager<AeroUser>(userStore, options, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger);
    }

}
