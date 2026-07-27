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
using AeroDB.Sable;
using AeroDB.AspNetIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wolverine;
using Aero.Cms.Modules.Setup.Bootstrap;
using Aero.Modular;

namespace Aero.Cms.Modules.Setup;

/// <summary>
/// Executes initial setup against a caller-selected remote AeroDB server.
/// </summary>
public interface IServerTargetSetupExecutor
{
    /// <summary>
    /// Configures a temporary server-backed document store, seeds installation data, and records bootstrap completion.
    /// </summary>
    /// <param name="serverConnectionString">The remote AeroDB endpoint used by the temporary store.</param>
    /// <param name="request">The initial administrator, tenant, site, and content selections.</param>
    /// <param name="descriptors">Optional module descriptors to initialize; an empty collection is used when omitted.</param>
    /// <param name="cancellationToken">
    /// Is forwarded to seeding and completion persistence. The current migration stage is a
    /// no-op, and creation of the lightweight document session does not receive this token.
    /// </param>
    /// <returns>The aggregate seed result. A failed seed result is returned without writing the completion marker.</returns>
    /// <exception cref="ArgumentException"><paramref name="serverConnectionString"/> is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
Task<SeedDatabaseResult> ExecuteAsync(
        string serverConnectionString,
        SeedDatabaseRequest request,
        IReadOnlyList<ModuleDescriptor>? descriptors = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Builds the service graph needed to seed a remote server without replacing the application's root container.
/// </summary>
/// <remarks>
/// The supplied connection string is used for the temporary document store and only a
/// short prefix is logged. Database credentials are passed to store options and must not
/// be logged. Successful seeding is followed by a file-based running-state write.
/// </remarks>
public sealed class ServerTargetSetupExecutor(
    IServiceProvider rootServiceProvider,
    ILogger<ServerTargetSetupExecutor> logger,
    IBootstrapCompletionWriter bootstrapCompletionWriter) : IServerTargetSetupExecutor
{
    /// <inheritdoc />
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
        if (!request.DatabaseUnauthenticated)
        {
            opts.Username = request.DatabaseUsername;
            opts.Password = request.DatabasePassword;
        }
        opts.DatabaseSchemaName = global::Aero.Core.Data.Schemas.Aero;
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
        var styleProfileResolver = new SiteStyleProfileResolver(store);
        var pageContentService = new AeroPageContentService(session, bus, noopSiteContext,
            rootServiceProvider.GetRequiredService<ILogger<AeroPageContentService>>(),
            rootServiceProvider.GetRequiredService<IHtmlContentValidator>(),
            rootServiceProvider.GetRequiredService<IStyleCompiler>(),
            styleProfileResolver);
        var pagePublishingWorkflowService = new PagePublishingWorkflowService(
            session,
            bus,
            rootServiceProvider.GetRequiredService<IHtmlContentValidator>(),
            rootServiceProvider.GetRequiredService<IStyleCompiler>(),
            styleProfileResolver,
            rootServiceProvider.GetRequiredService<ILogger<PagePublishingWorkflowService>>());
        var blogPostContentService = new PostContentService(session, noopSiteContext);
        var userStore = CreateUserStore(store, rootServiceProvider);
        var userManager = CreateUserManager(userStore, rootServiceProvider);
        var roleManager = CreateRoleManager(store);
        var identityBootstrapper = new SetupIdentityBootstrapper(userManager, roleManager);
        
        var mediaService = rootServiceProvider.GetRequiredService<IMediaService>();
        var commerceSeedService = rootServiceProvider.GetRequiredService<ICommerceSeedService>();
        var tenantService = rootServiceProvider.GetRequiredService<ITenantService>();
        var siteService = rootServiceProvider.GetRequiredService<ISiteService>();

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

    /// <summary>
    /// Preserves the former migration stage as a no-op because Sable owns persistence setup.
    /// </summary>
    private Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        // EF Core Npgsql migrations removed.
        // All persistence now handled by AeroDB.Sable (IDocumentSession).
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates an identity user store over the temporary document store.
    /// </summary>
    private static IUserStore<AeroUser> CreateUserStore(IDocumentStore store, IServiceProvider services)
    {
        return new AeroDBUserStore<AeroUser, AeroRole, long>(
            store,
            (ILogger<AeroDBUserStore<AeroUser, AeroRole, long>>)services.GetRequiredService(typeof(ILogger<AeroDBUserStore<AeroUser, AeroRole, long>>)));
    }

    /// <summary>
    /// Creates the user manager required for setup without registering it in the root container.
    /// </summary>
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

    /// <summary>
    /// Creates the role manager required for setup over the temporary document store.
    /// </summary>
    private static RoleManager<AeroRole> CreateRoleManager(IDocumentStore store)
    {
        var roleStore = new AeroDBRoleStore<AeroRole, long>(
            store,
            NullLogger<AeroDBRoleStore<AeroRole, long>>.Instance);

        return new RoleManager<AeroRole>(
            roleStore,
            Array.Empty<IRoleValidator<AeroRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<AeroRole>>.Instance);
    }

}
