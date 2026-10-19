using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Aero.Auth.Services;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core.Http;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalMemberBearerLiveStateTests
{
    private const long MemberId = 101;
    private const long SessionId = 202;
    private const long LinkId = 203;
    private const long AssignmentId = 204;
    private const long SiteId = 303;
    private const long TenantId = 404;
    private const string Provider = "workos";

    [Test]
    [Arguments("foreign-tenant")]
    [Arguments("foreign-site")]
    [Arguments("inactive-assignment")]
    [Arguments("deleted-assignment")]
    [Arguments("disabled-member")]
    [Arguments("disabled-site")]
    [Arguments("revoked-session")]
    [Arguments("expired-session")]
    [Arguments("security-version-mismatch")]
    [Arguments("identity-link-mismatch")]
    public async Task Bearer_handler_rejects_invalid_live_external_member_state(string scenario)
    {
        await using var harness = await CreateHarnessAsync();
        await ApplyScenarioAsync(harness.Session, scenario);
        var result = await AuthenticateAsync(harness.Session, CreateSiteContext(), CreateMemberToken());

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Failure?.Message).IsEqualTo("The bearer token is invalid.");
    }

    [Test]
    public async Task Bearer_handler_accepts_matching_enabled_live_state_and_sanitizes_the_principal()
    {
        await using var harness = await CreateHarnessAsync();
        var result = await AuthenticateAsync(harness.Session, CreateSiteContext(), CreateMemberToken());

        await Assert.That(result.Succeeded).IsTrue();
        var principal = result.Ticket!.Principal;
        await Assert.That(principal.HasClaim(ClaimTypes.Email, string.Empty)).IsFalse();
        await Assert.That(principal.Claims.Any(claim => claim.Type is ClaimTypes.Role or "role" or "roles")).IsFalse();
        await Assert.That(ExternalMemberPrincipal.TryRead(principal, out _)).IsTrue();
    }

    [Test]
    public async Task Bearer_handler_preserves_request_cancellation_during_live_state_validation()
    {
        await using var harness = await CreateHarnessAsync();
        using var cancellation = new CancellationTokenSource();

        await Assert.That(async () => await AuthenticateAsync(
                harness.Session,
                CreateSiteContext(),
                CreateMemberToken(),
                tokens => tokens.ValidateAccessTokenAsync("member-token", Arg.Any<CancellationToken>())
                    .Returns(_ =>
                    {
                        cancellation.Cancel();
                        return Task.FromResult<(bool IsValid, ClaimsPrincipal? Principal)>((true, CreateMemberToken()));
                    }),
                cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Bearer_handler_fails_closed_when_live_state_read_reports_a_nonrequest_cancellation()
    {
        using var requestCancellation = new CancellationTokenSource();
        using var unrelatedCancellation = new CancellationTokenSource();
        unrelatedCancellation.Cancel();
        var query = Substitute.For<IQuerySession>();
        query.LoadAsync<SitesModel>(SiteId, Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<SitesModel?>(unrelatedCancellation.Token));

        var result = await AuthenticateAsync(query, CreateSiteContext(), CreateMemberToken(), requestAborted: requestCancellation.Token);

        await Assert.That(result.Succeeded).IsFalse();
        await Assert.That(result.Failure?.Message).IsEqualTo("The bearer token is invalid.");
    }

    private static async Task<SableTestHarness> CreateHarnessAsync()
    {
        var harness = new SableTestHarness()
            .WithSchema<SitesModel>(SchemaMode.Flexible)
            .WithConfiguration(options => new IdentityModule().Configure(options));
        await harness.InitializeAsync();
        harness.Session.Store(new SitesModel { Id = SiteId, TenantId = TenantId, Name = "Store", IsEnabled = true });
        harness.Session.Store(new ExternalMember { Id = MemberId, IsActive = true, SecurityVersion = 3 });
        harness.Session.Store(new ExternalMemberSession
        {
            Id = SessionId,
            ExternalMemberId = MemberId,
            TenantId = TenantId,
            SiteId = SiteId,
            AuthenticationProvider = Provider,
            ExternalIdentityLinkId = LinkId,
            SecurityVersion = 3,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
        harness.Session.Store(new ExternalIdentityLink
        {
            Id = LinkId,
            ExternalMemberId = MemberId,
            Provider = Provider,
            Issuer = "https://issuer.example",
            Subject = "subject-1",
            IdentityKey = "digest",
            IsActive = true
        });
        harness.Session.Store(new ExternalMemberSiteAssignment
        {
            Id = AssignmentId,
            ExternalMemberId = MemberId,
            TenantId = TenantId,
            SiteId = SiteId,
            IsActive = true
        });
        await harness.Session.SaveChangesAsync();
        return harness;
    }

    private static async Task ApplyScenarioAsync(IDocumentSession session, string scenario)
    {
        switch (scenario)
        {
            case "foreign-tenant":
            {
                var externalSession = (await session.LoadAsync<ExternalMemberSession>(SessionId))!;
                externalSession.TenantId++;
                session.Store(externalSession);
                break;
            }
            case "foreign-site":
            {
                var externalSession = (await session.LoadAsync<ExternalMemberSession>(SessionId))!;
                externalSession.SiteId++;
                session.Store(externalSession);
                break;
            }
            case "inactive-assignment":
            {
                var assignment = (await session.LoadAsync<ExternalMemberSiteAssignment>(AssignmentId))!;
                assignment.IsActive = false;
                session.Store(assignment);
                break;
            }
            case "deleted-assignment":
                session.Delete<ExternalMemberSiteAssignment>(AssignmentId);
                break;
            case "disabled-member":
            {
                var member = (await session.LoadAsync<ExternalMember>(MemberId))!;
                member.IsActive = false;
                session.Store(member);
                break;
            }
            case "disabled-site":
            {
                var site = (await session.LoadAsync<SitesModel>(SiteId))!;
                site.IsEnabled = false;
                session.Store(site);
                break;
            }
            case "revoked-session":
            {
                var externalSession = (await session.LoadAsync<ExternalMemberSession>(SessionId))!;
                externalSession.RevokedAt = DateTimeOffset.UtcNow;
                session.Store(externalSession);
                break;
            }
            case "expired-session":
            {
                var externalSession = (await session.LoadAsync<ExternalMemberSession>(SessionId))!;
                externalSession.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
                session.Store(externalSession);
                break;
            }
            case "security-version-mismatch":
            {
                var member = (await session.LoadAsync<ExternalMember>(MemberId))!;
                member.SecurityVersion++;
                session.Store(member);
                break;
            }
            case "identity-link-mismatch":
            {
                var link = (await session.LoadAsync<ExternalIdentityLink>(LinkId))!;
                link.ExternalMemberId++;
                session.Store(link);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown test scenario.");
        }

        await session.SaveChangesAsync();
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        IQuerySession session,
        ISiteContext siteContext,
        ClaimsPrincipal tokenPrincipal,
        Action<IJwtTokenService>? configureTokens = null,
        CancellationToken requestAborted = default)
    {
        var tokens = Substitute.For<IJwtTokenService>();
        if (configureTokens is null)
        {
            tokens.ValidateAccessTokenAsync("member-token", Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<(bool IsValid, ClaimsPrincipal? Principal)>((true, tokenPrincipal)));
        }
        else
        {
            configureTokens(tokens);
        }
        var options = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Get(ExternalMemberAuthenticationDefaults.BearerScheme)
            .Returns(new AuthenticationSchemeOptions());
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var handler = new ExternalMemberBearerAuthenticationHandler(
            options,
            loggerFactory,
            UrlEncoder.Default,
            tokens,
            session,
            siteContext,
            TimeProvider.System);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer member-token";
        context.RequestAborted = requestAborted;
        var scheme = new AuthenticationScheme(
            ExternalMemberAuthenticationDefaults.BearerScheme,
            ExternalMemberAuthenticationDefaults.BearerScheme,
            typeof(ExternalMemberBearerAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);

        return await handler.AuthenticateAsync();
    }

    private static ClaimsPrincipal CreateMemberToken() => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, MemberId.ToString(CultureInfo.InvariantCulture)),
        new Claim(ClaimTypes.Email, string.Empty),
        new Claim(ExternalMemberClaimTypes.PrincipalKind, ExternalMemberClaimTypes.ExternalMember),
        new Claim(ExternalMemberClaimTypes.AuthenticationProvider, Provider),
        new Claim(ExternalMemberClaimTypes.SessionId, SessionId.ToString(CultureInfo.InvariantCulture)),
        new Claim(ExternalMemberClaimTypes.SecurityVersion, "3"),
        new Claim(ExternalMemberClaimTypes.TenantId, TenantId.ToString(CultureInfo.InvariantCulture)),
        new Claim(ExternalMemberClaimTypes.SiteId, SiteId.ToString(CultureInfo.InvariantCulture)),
        new Claim(ExternalMemberClaimTypes.TokenUse, ExternalMemberClaimTypes.AccessTokenUse)
    ]));

    private static ISiteContext CreateSiteContext()
    {
        var context = Substitute.For<ISiteContext>();
        context.TenantId.Returns(TenantId);
        context.SiteId.Returns(SiteId);
        return context;
    }
}
