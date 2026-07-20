using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberBoundaryTests
{
    private const long MemberId = 101;
    private const long SessionId = 202;
    private const long SiteId = 303;
    private const long TenantId = 404;

    [Test]
    public async Task Cookie_validator_accepts_matching_active_member_and_session()
    {
        var member = ActiveMember();
        var externalSession = ActiveSession();
        var (context, authenticationService, services) = CreateValidationContext(member, externalSession);
        using (services)
        {
            await new ExternalMemberCookieValidator().ValidateAsync(context);

            await Assert.That(context.Principal).IsNotNull();
            await authenticationService.DidNotReceiveWithAnyArgs()
                .SignOutAsync(default!, default, default);
        }
    }

    [Test]
    [Arguments("missing-member")]
    [Arguments("inactive-member")]
    [Arguments("stale-member-version")]
    [Arguments("missing-session")]
    [Arguments("wrong-owner")]
    [Arguments("wrong-provider")]
    [Arguments("stale-session-version")]
    [Arguments("revoked-session")]
    [Arguments("expired-session")]
    public async Task Cookie_validator_rejects_invalid_local_state(string scenario)
    {
        ExternalMember? member = ActiveMember();
        ExternalMemberSession? externalSession = ActiveSession();

        switch (scenario)
        {
            case "missing-member":
                member = null;
                break;
            case "inactive-member":
                member!.IsActive = false;
                break;
            case "stale-member-version":
                member!.SecurityVersion++;
                break;
            case "missing-session":
                externalSession = null;
                break;
            case "wrong-owner":
                externalSession!.ExternalMemberId++;
                break;
            case "wrong-provider":
                externalSession!.AuthenticationProvider = "entra_external_id";
                break;
            case "stale-session-version":
                externalSession!.SecurityVersion++;
                break;
            case "revoked-session":
                externalSession!.RevokedAt = DateTimeOffset.UtcNow;
                break;
            case "expired-session":
                externalSession!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
                break;
        }

        var (context, authenticationService, services) = CreateValidationContext(member, externalSession);
        using (services)
        {
            await new ExternalMemberCookieValidator().ValidateAsync(context);

            await Assert.That(context.Principal).IsNull();
            await authenticationService.Received(1).SignOutAsync(
                context.HttpContext,
                ExternalMemberAuthenticationDefaults.Scheme,
                Arg.Any<AuthenticationProperties?>());
        }
    }

    [Test]
    public async Task Cookie_validator_rejects_when_the_member_store_fails()
    {
        var querySession = Substitute.For<IQuerySession>();
        querySession.LoadAsync<ExternalMember>(MemberId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ExternalMember?>(new InvalidOperationException("store unavailable")));
        var (context, authenticationService, services) = CreateValidationContext(querySession);
        using (services)
        {
            await new ExternalMemberCookieValidator().ValidateAsync(context);

            await Assert.That(context.Principal).IsNull();
            await authenticationService.Received(1).SignOutAsync(
                context.HttpContext,
                ExternalMemberAuthenticationDefaults.Scheme,
                Arg.Any<AuthenticationProperties?>());
        }
    }

    [Test]
    public async Task Host_site_assignment_authorizes_without_manager_selected_site_state()
    {
        await using var harness = await CreateSiteHarnessAsync(
            siteEnabled: true,
            siteTenantId: TenantId,
            includeAssignment: true,
            assignmentActive: true,
            assignmentTenantId: TenantId);
        var context = CreateSiteAuthorizationContext();
        var siteContext = CreateSiteContext();

        await new ExternalMemberSiteAuthorizationHandler(siteContext, harness.Session).HandleAsync(context);

        await Assert.That(context.HasSucceeded).IsTrue();
    }

    [Test]
    [Arguments(false, 404, true, true, 404)]
    [Arguments(true, 999, true, true, 404)]
    [Arguments(true, 404, false, true, 404)]
    [Arguments(true, 404, true, false, 404)]
    [Arguments(true, 404, true, true, 999)]
    public async Task Host_site_assignment_rejects_disabled_cross_tenant_or_inactive_state(
        bool siteEnabled,
        long siteTenantId,
        bool includeAssignment,
        bool assignmentActive,
        long assignmentTenantId)
    {
        await using var harness = await CreateSiteHarnessAsync(
            siteEnabled,
            siteTenantId,
            includeAssignment,
            assignmentActive,
            assignmentTenantId);
        var context = CreateSiteAuthorizationContext();

        await new ExternalMemberSiteAuthorizationHandler(CreateSiteContext(), harness.Session).HandleAsync(context);

        await Assert.That(context.HasSucceeded).IsFalse();
    }

    [Test]
    public async Task Host_site_assignment_rejects_when_the_store_fails()
    {
        var querySession = Substitute.For<IQuerySession>();
        querySession.LoadAsync<SitesModel>(SiteId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SitesModel?>(new InvalidOperationException("store unavailable")));
        var context = CreateSiteAuthorizationContext();

        await new ExternalMemberSiteAuthorizationHandler(CreateSiteContext(), querySession).HandleAsync(context);

        await Assert.That(context.HasSucceeded).IsFalse();
    }

    [Test]
    public async Task Identity_schema_rejects_duplicate_member_site_assignments()
    {
        await using var harness = new SableTestHarness()
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        harness.Session.Store(
            new ExternalMemberSiteAssignment
            {
                Id = 601,
                ExternalMemberId = MemberId,
                TenantId = TenantId,
                SiteId = SiteId
            },
            new ExternalMemberSiteAssignment
            {
                Id = 602,
                ExternalMemberId = MemberId,
                TenantId = TenantId,
                SiteId = SiteId
            });

        await Assert.That(async () => await harness.Session.SaveChangesAsync())
            .ThrowsException();
    }

    private static ExternalMember ActiveMember() => new()
    {
        Id = MemberId,
        IsActive = true,
        SecurityVersion = 3
    };

    private static ExternalMemberSession ActiveSession() => new()
    {
        Id = SessionId,
        ExternalMemberId = MemberId,
        AuthenticationProvider = "workos",
        SecurityVersion = 3,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
    };

    private static (
        CookieValidatePrincipalContext Context,
        IAuthenticationService AuthenticationService,
        ServiceProvider Services)
        CreateValidationContext(ExternalMember? member, ExternalMemberSession? externalSession)
    {
        var querySession = Substitute.For<IQuerySession>();
        querySession.LoadAsync<ExternalMember>(MemberId, Arg.Any<CancellationToken>()).Returns(member);
        querySession.LoadAsync<ExternalMemberSession>(SessionId, Arg.Any<CancellationToken>()).Returns(externalSession);
        return CreateValidationContext(querySession);
    }

    private static (
        CookieValidatePrincipalContext Context,
        IAuthenticationService AuthenticationService,
        ServiceProvider Services)
        CreateValidationContext(IQuerySession querySession)
    {
        var authenticationService = Substitute.For<IAuthenticationService>();
        var services = new ServiceCollection()
            .AddSingleton(querySession)
            .AddSingleton(authenticationService)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var scheme = new AuthenticationScheme(
            ExternalMemberAuthenticationDefaults.Scheme,
            ExternalMemberAuthenticationDefaults.Scheme,
            typeof(CookieAuthenticationHandler));
        var principal = ExternalMemberPrincipal.Create(MemberId, "workos", SessionId, 3);
        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties(),
            ExternalMemberAuthenticationDefaults.Scheme);
        var context = new CookieValidatePrincipalContext(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            ticket);
        return (context, authenticationService, services);
    }

    private static async Task<SableTestHarness> CreateSiteHarnessAsync(
        bool siteEnabled,
        long siteTenantId,
        bool includeAssignment,
        bool assignmentActive,
        long assignmentTenantId)
    {
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithSchema<ExternalMemberSiteAssignment>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel
        {
            Id = SiteId,
            TenantId = siteTenantId,
            Name = "Store",
            IsEnabled = siteEnabled
        });
        if (includeAssignment)
        {
            harness.Session.Store(new ExternalMemberSiteAssignment
            {
                Id = 505,
                ExternalMemberId = MemberId,
                TenantId = assignmentTenantId,
                SiteId = SiteId,
                IsActive = assignmentActive
            });
        }

        await harness.Session.SaveChangesAsync();
        return harness;
    }

    private static AuthorizationHandlerContext CreateSiteAuthorizationContext()
    {
        var requirement = new ExternalMemberSiteRequirement();
        return new AuthorizationHandlerContext(
            [requirement],
            ExternalMemberPrincipal.Create(MemberId, "workos", SessionId, 3),
            resource: null);
    }

    private static ISiteContext CreateSiteContext()
    {
        var siteContext = Substitute.For<ISiteContext>();
        siteContext.SiteId.Returns(SiteId);
        siteContext.TenantId.Returns(TenantId);
        return siteContext;
    }
}
