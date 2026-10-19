using System.Globalization;
using System.Net;
using System.Security.Claims;
using Alba;
using Aero.Auth.Services;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Cms.Modules.Jwt;
using Aero.Core.Http;
using AeroDB.Sable;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberLocalApiAuthorizationIntegrationTests
{
    private const long TenantId = 404;
    private const long SiteId = 303;
    private const long ForeignSiteId = 304;
    private const long CustomerAdminId = 101;
    private const long GeneralUserId = 102;
    private const long PowerUserId = 103;
    private const long SameSiteTargetId = 104;
    private const long ForeignSiteTargetId = 105;
    private const string Provider = "workos";

    [Test]
    public async Task Customer_admin_cookie_without_member_bearer_cannot_mutate_and_assignment_is_unchanged()
    {
        await using var fixture = await MemberApiFixture.CreateAsync();
        var signIn = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/test/signin/customer-admin");
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var cookie = signIn.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];

        await PutRoleAsync(fixture.Host, SameSiteTargetId, null, cookie, HttpStatusCode.Unauthorized);

        await fixture.AssertRoleAsync(SameSiteTargetId, SiteId, ExternalMemberCustomerRole.GeneralUser);
    }

    [Test]
    [Arguments(null, HttpStatusCode.Unauthorized)]
    [Arguments("general-token", HttpStatusCode.Forbidden)]
    [Arguments("power-token", HttpStatusCode.Forbidden)]
    public async Task Anonymous_general_and_power_member_bearers_cannot_mutate_and_assignment_is_unchanged(
        string? token,
        HttpStatusCode expectedStatus)
    {
        await using var fixture = await MemberApiFixture.CreateAsync();

        await PutRoleAsync(fixture.Host, SameSiteTargetId, token, null, expectedStatus);

        await fixture.AssertRoleAsync(SameSiteTargetId, SiteId, ExternalMemberCustomerRole.GeneralUser);
    }

    [Test]
    public async Task Same_site_customer_admin_member_bearer_can_mutate_the_assignment()
    {
        await using var fixture = await MemberApiFixture.CreateAsync();

        await PutRoleAsync(fixture.Host, SameSiteTargetId, "customer-admin-token", null, HttpStatusCode.NoContent);

        await fixture.AssertRoleAsync(SameSiteTargetId, SiteId, ExternalMemberCustomerRole.CustomerAdmin);
    }

    [Test]
    public async Task Customer_admin_member_bearer_cannot_mutate_foreign_site_target_and_assignment_is_unchanged()
    {
        await using var fixture = await MemberApiFixture.CreateAsync();

        await PutRoleAsync(fixture.Host, ForeignSiteTargetId, "customer-admin-token", null, HttpStatusCode.NotFound);

        await fixture.AssertRoleAsync(ForeignSiteTargetId, ForeignSiteId, ExternalMemberCustomerRole.GeneralUser);
    }

    [Test]
    public async Task Manager_bearer_handler_rejects_an_external_member_jwt()
    {
        var tokens = Substitute.For<IJwtTokenService>();
        tokens.ValidateAccessTokenAsync("customer-member-token", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(bool IsValid, ClaimsPrincipal? Principal)>((true, CreateManagerRejectedCustomerPrincipal())));
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(tokens);
        builder.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AeroJwtAuthenticationHandler>(AeroJwtAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("manager", policy =>
        {
            policy.AddAuthenticationSchemes(AeroJwtAuthenticationHandler.SchemeName);
            policy.RequireAuthenticatedUser();
        }));
        await using var host = await AlbaHost.For(builder, app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/manager-probe", () => Results.NoContent()).RequireAuthorization("manager");
        });

        await host.Scenario(x =>
        {
            x.Get.Url("/manager-probe");
            x.WithBearerToken("customer-member-token");
            x.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }

    private static Task PutRoleAsync(
        IAlbaHost host,
        long memberId,
        string? bearerToken,
        string? cookie,
        HttpStatusCode expectedStatus) => host.Scenario(x =>
    {
        x.Put.Url($"/api/v1/member/local/assignments/{memberId}/role");
        x.WriteJson(new ExternalIdentityAdminApi.ExternalMemberRoleAssignmentRequest(nameof(ExternalMemberCustomerRole.CustomerAdmin)), JsonStyle.MinimalApi);
        if (bearerToken is not null)
            x.WithBearerToken(bearerToken);
        if (cookie is not null)
            x.WithRequestHeader("Cookie", cookie);
        x.StatusCodeShouldBe(expectedStatus);
    });

    private static ClaimsPrincipal CreateManagerRejectedCustomerPrincipal() => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, CustomerAdminId.ToString(CultureInfo.InvariantCulture)),
        new Claim(ClaimTypes.Email, string.Empty),
        new Claim(ExternalMemberClaimTypes.PrincipalKind, ExternalMemberClaimTypes.ExternalMember),
        new Claim(ExternalMemberClaimTypes.AuthenticationProvider, Provider),
        new Claim(ExternalMemberClaimTypes.SessionId, "201"),
        new Claim(ExternalMemberClaimTypes.SecurityVersion, "3"),
        new Claim(ExternalMemberClaimTypes.TenantId, TenantId.ToString(CultureInfo.InvariantCulture)),
        new Claim(ExternalMemberClaimTypes.SiteId, SiteId.ToString(CultureInfo.InvariantCulture)),
        new Claim(ExternalMemberClaimTypes.TokenUse, ExternalMemberClaimTypes.AccessTokenUse)
    ]));

    private sealed class MemberApiFixture : IAsyncDisposable
    {
        private readonly SableTestHarness _harness;

        private MemberApiFixture(SableTestHarness harness, IAlbaHost host)
        {
            _harness = harness;
            Host = host;
        }

        public IAlbaHost Host { get; }

        public static async Task<MemberApiFixture> CreateAsync()
        {
            var harness = new SableTestHarness()
                .WithSchema<SitesModel>(SchemaMode.Flexible)
                .WithConfiguration(options => new IdentityModule().Configure(options));
            await harness.InitializeAsync();
            await SeedAsync(harness.Session);

            var tokens = Substitute.For<IJwtTokenService>();
            foreach (var (token, memberId, sessionId) in new[]
            {
                ("customer-admin-token", CustomerAdminId, 201L),
                ("general-token", GeneralUserId, 202L),
                ("power-token", PowerUserId, 203L)
            })
            {
                tokens.ValidateAccessTokenAsync(token, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<(bool IsValid, ClaimsPrincipal? Principal)>((true, CreateToken(memberId, sessionId))));
            }

            var siteContext = Substitute.For<ISiteContext>();
            siteContext.TenantId.Returns(TenantId);
            siteContext.SiteId.Returns(SiteId);
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddLogging();
            builder.Services.AddSingleton<IQuerySession>(harness.Session);
            builder.Services.AddSingleton<IDocumentSession>(harness.Session);
            builder.Services.AddSingleton(siteContext);
            builder.Services.AddSingleton(tokens);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton(Substitute.For<ILocalExternalMemberAuthenticationService>());
            builder.Services.AddSingleton(new ExternalMemberCookieIssuer(
                Substitute.For<IExternalMemberSessionRevocationService>(),
                TimeProvider.System));
            builder.Services.AddScoped<IValidator<ExternalIdentityAdminApi.ExternalMemberRoleAssignmentRequest>, ExternalMemberRoleAssignmentRequestValidator>();
            builder.Services.AddScoped<IAuthorizationHandler, ExternalMemberSiteAuthorizationHandler>();
            builder.Services.AddScoped<IAuthorizationHandler, ExternalMemberPermissionAuthorizationHandler>();
            builder.Services.AddAuthentication()
                .AddCookie(ExternalMemberAuthenticationDefaults.Scheme, options =>
                {
                    options.Cookie.Name = ".AeroCms.Member";
                    options.Events.OnRedirectToLogin = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, ExternalMemberBearerAuthenticationHandler>(
                    ExternalMemberAuthenticationDefaults.BearerScheme,
                    _ => { });
            builder.Services.AddAuthorization(options => options.AddPolicy(
                ExternalMemberAuthenticationDefaults.MemberUsersManageBearerPolicy,
                policy =>
                {
                    policy.AddAuthenticationSchemes(ExternalMemberAuthenticationDefaults.BearerScheme);
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(
                        new ExternalMemberSiteRequirement(),
                        new ExternalMemberPermissionRequirement(ExternalMemberPermissions.MemberUsersManage));
                }));

            var host = await AlbaHost.For(builder, app =>
            {
                app.UseAuthentication();
                app.UseAuthorization();
                app.MapGet("/test/signin/customer-admin", async (HttpContext context) =>
                {
                    await context.SignInAsync(
                        ExternalMemberAuthenticationDefaults.Scheme,
                        ExternalMemberPrincipal.Create(CustomerAdminId, Provider, 201, 3));
                    return Results.NoContent();
                }).AllowAnonymous();
                app.MapExternalMemberLocalApi();
            });
            return new MemberApiFixture(harness, host);
        }

        public async Task AssertRoleAsync(long memberId, long siteId, ExternalMemberCustomerRole expectedRole)
        {
            var assignment = await _harness.Session.Query<ExternalMemberSiteAssignment>()
                .FirstOrDefaultAsync(x => x.ExternalMemberId == memberId && x.TenantId == TenantId && x.SiteId == siteId);
            await Assert.That(assignment).IsNotNull();
            await Assert.That(assignment!.CustomerRole).IsEqualTo(expectedRole);
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            await _harness.DisposeAsync();
        }

        private static async Task SeedAsync(IDocumentSession session)
        {
            session.Store(new SitesModel { Id = SiteId, TenantId = TenantId, Name = "Current site", IsEnabled = true });
            session.Store(new SitesModel { Id = ForeignSiteId, TenantId = TenantId, Name = "Foreign site", IsEnabled = true });
            foreach (var (memberId, sessionId, role, siteId) in new[]
            {
                (CustomerAdminId, 201L, ExternalMemberCustomerRole.CustomerAdmin, SiteId),
                (GeneralUserId, 202L, ExternalMemberCustomerRole.GeneralUser, SiteId),
                (PowerUserId, 203L, ExternalMemberCustomerRole.PowerUser, SiteId),
                (SameSiteTargetId, 204L, ExternalMemberCustomerRole.GeneralUser, SiteId),
                (ForeignSiteTargetId, 205L, ExternalMemberCustomerRole.GeneralUser, ForeignSiteId)
            })
            {
                session.Store(new ExternalMember { Id = memberId, IsActive = true, SecurityVersion = 3 });
                session.Store(new ExternalIdentityLink
                {
                    Id = 1_000 + memberId,
                    ExternalMemberId = memberId,
                    Provider = Provider,
                    Issuer = "https://issuer.example",
                    Subject = $"subject-{memberId}",
                    IdentityKey = $"digest-{memberId}",
                    IsActive = true
                });
                session.Store(new ExternalMemberSession
                {
                    Id = sessionId,
                    ExternalMemberId = memberId,
                    TenantId = TenantId,
                    SiteId = siteId,
                    AuthenticationProvider = Provider,
                    ExternalIdentityLinkId = 1_000 + memberId,
                    SecurityVersion = 3,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                });
                session.Store(new ExternalMemberSiteAssignment
                {
                    Id = 2_000 + memberId,
                    ExternalMemberId = memberId,
                    TenantId = TenantId,
                    SiteId = siteId,
                    CustomerRole = role,
                    IsActive = true
                });
            }
            await session.SaveChangesAsync();
        }

        private static ClaimsPrincipal CreateToken(long memberId, long sessionId) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, memberId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Email, string.Empty),
            new Claim(ExternalMemberClaimTypes.PrincipalKind, ExternalMemberClaimTypes.ExternalMember),
            new Claim(ExternalMemberClaimTypes.AuthenticationProvider, Provider),
            new Claim(ExternalMemberClaimTypes.SessionId, sessionId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ExternalMemberClaimTypes.SecurityVersion, "3"),
            new Claim(ExternalMemberClaimTypes.TenantId, TenantId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ExternalMemberClaimTypes.SiteId, SiteId.ToString(CultureInfo.InvariantCulture)),
            new Claim(ExternalMemberClaimTypes.TokenUse, ExternalMemberClaimTypes.AccessTokenUse)
        ]));
    }
}
