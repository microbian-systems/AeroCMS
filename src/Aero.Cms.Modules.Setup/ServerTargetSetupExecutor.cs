using Aero.Cms.Core;
using Aero.Cms.Core.Blocks;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Blog;
using Aero.Cms.Modules.Pages;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Tenant;
using Aero.Cms.Web.Core.Blocks;
using Aero.Cms.Web.Core.Modules;
using Aero.Core.Data;
using Aero.MartenDB.Identity;
using Aero.EfCore;
using Aero.Models.Entities;
using Aero.Core.Identity;
using Aero.Services.Images;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wolverine;
using System.Reflection;

namespace Aero.Cms.Modules.Setup;

public interface IServerTargetSetupExecutor
{
    Task<SeedDatabaseResult> ExecuteAsync(string serverConnectionString, SeedDatabaseRequest request, CancellationToken cancellationToken = default);
}

public sealed class ServerTargetSetupExecutor(
    IServiceProvider rootServiceProvider,
    ILogger<ServerTargetSetupExecutor> logger,
    IBootstrapCompletionWriter bootstrapCompletionWriter) : IServerTargetSetupExecutor
{
    public async Task<SeedDatabaseResult> ExecuteAsync(string serverConnectionString, SeedDatabaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverConnectionString);
        ArgumentNullException.ThrowIfNull(request);

        await MigrateAsync(serverConnectionString, cancellationToken);

        var store = DocumentStore.For(options =>
        {
            options.Connection(serverConnectionString);
            options.DatabaseSchemaName = global::Aero.Core.Data.Schemas.Aero;
            options.UseSystemTextJsonForSerialization(new System.Text.Json.JsonSerializerOptions
            {
                AllowOutOfOrderMetadataProperties = true
            });
            options.Schema.For<AeroRole>().Identity(x => x.Id);
            options.Schema.For<AeroUser>().Identity(x => x.Id);

            foreach (var configure in rootServiceProvider.GetServices<IConfigureMarten>())
            {
                configure.Configure(rootServiceProvider, options);
            }
        });

        await using var session = store.LightweightSession();
        var blockService = new MartenBlockService(session);
        var bus = rootServiceProvider.GetRequiredService<IMessageBus>();
        var pageContentService = new MartenPageContentService(session, blockService, bus);
        var blogPostContentService = new MartenBlogPostContentService(session);
        var userStore = CreateUserStore(session, rootServiceProvider);
        var userManager = CreateUserManager(userStore, rootServiceProvider);
        var identityBootstrapper = new SetupIdentityBootstrapper(userManager);
        var moduleStateStore = new ModuleStateStore(session);
        var staticPhotosClient = rootServiceProvider.GetRequiredService<IStaticPhotosClient>();
        var moduleDiscoveryService = rootServiceProvider.GetRequiredService<IModuleDiscoveryService>();

        var tenantService = rootServiceProvider.GetRequiredService<ITenantService>();
        var siteService = rootServiceProvider.GetRequiredService<ISiteService>();

        var seedService = new SeedDatabaseService(
            session,
            identityBootstrapper,
            pageContentService,
            blogPostContentService,
            staticPhotosClient,
            moduleDiscoveryService,
            moduleStateStore,
            bootstrapCompletionWriter,
            tenantService,
            siteService);

        var result = await seedService.CompleteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            logger.LogWarning("Server-targeted setup seeding failed: {Errors}", string.Join("; ", result.Errors));
            return result;
        }

        await bootstrapCompletionWriter.MarkCompleteAsync(cancellationToken);
        return result;
    }

    private async Task MigrateAsync(string connectionString, CancellationToken cancellationToken)
    {
        var apiOptions = new DbContextOptionsBuilder<AeroApiContext>().UseNpgsql(connectionString).Options;
        var dbOptions = new DbContextOptionsBuilder<AeroDbContext>().UseNpgsql(connectionString).Options;

        await using (var apiContext = new AeroApiContext(apiOptions))
        {
            await apiContext.Database.MigrateAsync(cancellationToken);
        }

        await using (var dbContext = new AeroDbContext(dbOptions))
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }

    private static IUserStore<AeroUser> CreateUserStore(IDocumentSession session, IServiceProvider services)
    {
        var userStoreType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .First(type => type.FullName == "Aero.MartenDB.Identity.UserStore`2" && type.Assembly.GetName().Name == "Aero.Cms.Modules.Identity");

        var closedType = userStoreType.MakeGenericType(typeof(AeroUser), typeof(AeroRole));
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(closedType.FullName!);
        return (IUserStore<AeroUser>)Activator.CreateInstance(closedType, session, logger)!;
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
