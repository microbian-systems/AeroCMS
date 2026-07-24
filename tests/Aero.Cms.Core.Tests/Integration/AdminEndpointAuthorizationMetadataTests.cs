using System.Net;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Modules.Audit.Areas.Api.v1;
using Aero.Cms.Modules.Aliases.Areas.Api.v1;
using Aero.Cms.Modules.Ai.Api;
using Aero.Cms.Modules.Ai.Configuration;
using Aero.Cms.Modules.Ai.Services;
using Aero.Cms.Modules.Content.Areas.Api.v1;
using Aero.Cms.Modules.Docs.Areas.Api.v1;
using Aero.Cms.Modules.Footer.Areas.Api.v1;
using Aero.Cms.Modules.Identity;
using Aero.Cms.Modules.Manager.Areas.Api.v1;
using Aero.Cms.Modules.Media.Areas.Api.v1;
using Aero.Cms.Modules.Modules.Areas.Api.v1;
using Aero.Cms.Modules.Navigation.Areas.Api.v1;
using Aero.Cms.Modules.Pages.Areas.Api.v1;
using Aero.Cms.Modules.Posts.Areas.Api.v1;
using Aero.Cms.Modules.Settings.Areas.Api.v1;
using Aero.Cms.Modules.Setup.Endpoints;
using Aero.Cms.Modules.Setup.Services;
using Aero.Cms.Modules.Sites;
using Aero.Cms.Modules.Theming.Areas.Api.v1;
using Aero.Cms.Modules.Users.Areas.Api.v1;
using Aero.Cms.Abstractions.Actors;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Content.Services;
using Aero.Core.Http;
using AeroDB.Sable;
using Aero.Models.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class AdminEndpointAuthorizationMetadataTests
{
    [Test]
    public async Task KnownAdminMappersDeclareExplicitAuthorizationIntent()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IAeroAliasActor>(_ => null!);
        builder.Services.AddSingleton<IAeroCategoryActor>(_ => null!);
        builder.Services.AddSingleton<IAeroContentItemActor>(_ => null!);
        builder.Services.AddSingleton<IAeroContentTypeActor>(_ => null!);
        builder.Services.AddSingleton<IAeroDocsActor>(_ => null!);
        builder.Services.AddSingleton<IAeroMediaActor>(_ => null!);
        builder.Services.AddSingleton<IAeroPageActor>(_ => null!);
        builder.Services.AddSingleton<IAeroPostActor>(_ => null!);
        builder.Services.AddSingleton<IAeroSeriesActor>(_ => null!);
        builder.Services.AddSingleton<IAeroTagActor>(_ => null!);
        builder.Services.AddSingleton<IAiContentEnhancementService>(_ => null!);
        builder.Services.AddSingleton<IAiContentTranslationService>(_ => null!);
        builder.Services.AddSingleton<IAiSettingsStore>(_ => null!);
        builder.Services.AddSingleton<IContentQueryService>(_ => null!);
        builder.Services.AddSingleton<IValidator<EnhanceContentRequest>>(_ => null!);
        builder.Services.AddSingleton<IValidator<TranslateDocumentRequest>>(_ => null!);
        builder.Services.AddSingleton<IDocumentSession>(_ => null!);
        builder.Services.AddSingleton<IQuerySession>(_ => null!);
        builder.Services.AddSingleton<ISiteContext>(_ => null!);
        builder.Services.AddSingleton<ITranslationImportService>(_ => null!);
        builder.Services.AddSingleton<UserManager<AeroUser>>(_ => null!);
        builder.Services.AddSingleton<SignInManager<AeroUser>>(_ => null!);
        var app = builder.Build();

        app.MapAuditApi();
        app.MapAiApi();
        app.MapSitesApi();
        app.MapDashboardApi();
        app.MapPagesApi();
        app.MapPagesTreeApi();
        app.MapBlogApi();
        app.MapCategoriesApi();
        app.MapContentItemsApi();
        app.MapContentHierarchyManagerApi();
        app.MapContentTypesApi();
        app.MapTagsApi();
        app.MapSeriesApi();
        app.MapDocsApi();
        app.MapMediaApi();
        app.MapFilesApi();
        app.MapAliasesApi();
        app.MapNavigationAdminApi();
        app.MapFooterAdminApi();
        app.MapSettingsApi();
        app.MapThemesApi();
        app.MapModulesApi();
        app.MapTranslationImportEndpoint();
        app.MapUsersApi();
        app.MapProfileApi();
        app.MapIdentityApi();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var expectedSurfaces = new[]
        {
            "/api/v1/admin/sites",
            "/api/v1/admin/errors",
            "/api/v1/admin/audit",
            "/api/v1/admin/ai",
            "/api/v1/admin/dashboard",
            "/api/v1/admin/pages",
            "/api/v1/admin/pages/tree",
            "/api/v1/admin/preview/pages",
            "/api/v1/admin/blogs",
            "/api/v1/admin/preview/blog-posts",
            "/api/v1/admin/categories",
            "/api/v1/admin/content-items",
            "/api/v1/admin/content-types",
            "/api/v1/admin/tags",
            "/api/v1/admin/series",
            "/api/v1/admin/docs",
            "/api/v1/admin/media",
            "/api/v1/admin/files",
            "/api/v1/admin/aliases",
            "/api/v1/admin/navigations",
            "/api/v1/admin/footers",
            "/api/v1/admin/settings",
            "/api/v1/admin/themes",
            "/api/v1/admin/modules",
            "/api/v1/admin/localization",
            "/api/v1/admin/users",
            "/api/v1/admin/profile",
            "/api/v1/admin/auth"
        };

        var missingSurfaces = expectedSurfaces
            .Where(prefix => !endpoints.Any(endpoint =>
                endpoint.RoutePattern.RawText?.StartsWith(prefix, StringComparison.Ordinal) == true))
            .ToList();
        await Assert.That(missingSurfaces).IsEmpty();

        var endpointsWithoutExplicitAccessIntent = endpoints
            .Where(endpoint =>
                endpoint.RoutePattern.RawText?.StartsWith(
                    "/api/v1/admin/",
                    StringComparison.Ordinal) == true)
            .Where(endpoint =>
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0
                && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();
        await Assert.That(endpointsWithoutExplicitAccessIntent).IsEmpty();

        var privilegedGlobalSurfaces = new[]
        {
            "/api/v1/admin/audit",
            "/api/v1/admin/dashboard",
            "/api/v1/admin/settings",
            "/api/v1/admin/modules",
            "/api/v1/admin/localization",
            "/api/v1/admin/users"
        };
        var globalEndpointsWithoutAdminPolicy = endpoints
            .Where(endpoint => privilegedGlobalSurfaces.Any(prefix =>
                endpoint.RoutePattern.RawText?.StartsWith(prefix, StringComparison.Ordinal) == true))
            .Where(endpoint => !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(data => string.Equals(data.Policy, "AeroAdmin", StringComparison.Ordinal)))
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();
        await Assert.That(globalEndpointsWithoutAdminPolicy).IsEmpty();

        var themeEndpoints = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/api/v1/admin/themes",
                StringComparison.Ordinal) == true)
            .ToList();
        await Assert.That(themeEndpoints).IsNotEmpty();
        await Assert.That(themeEndpoints.All(endpoint =>
            endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any()
            && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .All(data => data.Policy is null))).IsTrue();

        var aiSettingsEndpoints = endpoints
            .Where(endpoint => string.Equals(
                endpoint.RoutePattern.RawText,
                "/api/v1/admin/ai/settings",
                StringComparison.Ordinal))
            .ToList();
        await Assert.That(aiSettingsEndpoints.Count).IsEqualTo(2);
        await Assert.That(aiSettingsEndpoints.All(endpoint =>
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(data => string.Equals(data.Policy, "AeroAdmin", StringComparison.Ordinal))))
            .IsTrue();

        var editorAiEndpoints = endpoints
            .Where(endpoint =>
                endpoint.RoutePattern.RawText?.StartsWith(
                    "/api/v1/admin/ai/",
                    StringComparison.Ordinal) == true
                && !string.Equals(
                    endpoint.RoutePattern.RawText,
                    "/api/v1/admin/ai/settings",
                    StringComparison.Ordinal))
            .ToList();
        await Assert.That(editorAiEndpoints.Count).IsEqualTo(3);
        await Assert.That(editorAiEndpoints.All(endpoint =>
            endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
            && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any()
            && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .All(data => data.Policy is null))).IsTrue();

        var profileEndpoints = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "/api/v1/admin/profile",
                StringComparison.Ordinal) == true)
            .ToList();
        await Assert.That(profileEndpoints).IsNotEmpty();
        await Assert.That(profileEndpoints.All(endpoint =>
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0
            && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)).IsTrue();

        var anonymousAuthRoutes = new[]
        {
            "/api/v1/admin/auth/config",
            "/api/v1/admin/auth/local/login",
            "/api/v1/admin/auth/logout"
        };
        foreach (var route in anonymousAuthRoutes)
        {
            var endpoint = endpoints.Single(candidate =>
                string.Equals(candidate.RoutePattern.RawText, route, StringComparison.Ordinal));
            await Assert.That(endpoint.Metadata.GetMetadata<IAllowAnonymous>()).IsNotNull();
            await Assert.That(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()).IsEmpty();
        }

        var currentUserEndpoint = endpoints.Single(endpoint =>
            string.Equals(
                endpoint.RoutePattern.RawText,
                "/api/v1/admin/auth/me",
                StringComparison.Ordinal));
        var currentUserAuthorization =
            currentUserEndpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        await Assert.That(currentUserEndpoint.Metadata.GetMetadata<IAllowAnonymous>()).IsNull();
        await Assert.That(currentUserAuthorization.Count).IsEqualTo(1);
        await Assert.That(currentUserAuthorization[0].Policy).IsNull();

        await app.DisposeAsync();
    }

    [Test]
    public async Task AnonymousRequestToModulesApiReturnsUnauthorized()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddTestAuthentication();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapModulesApi();
        await app.StartAsync();

        using var response = await app.GetTestClient()
            .GetAsync($"/{HttpConstants.ApiPrefix}admin/modules/");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
