using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Alba;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Core.Entities;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalIdentityAdminAntiforgeryTests
{
    [Test]
    public async Task Antiforgery_handshake_requires_the_existing_manager_scheme_and_global_administrator_role()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();

        await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/v1/admin/external-identity/antiforgery");
            x.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });

        var customerCookie = await fixture.SignInCustomerAdminAsync();
        await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/v1/admin/external-identity/antiforgery");
            x.WithRequestHeader("Cookie", customerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.Forbidden);
        });
    }

    [Test]
    public async Task Antiforgery_handshake_returns_the_server_selected_header_a_non_cacheable_token_and_ignores_caller_header_selection()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        var scenario = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/v1/admin/external-identity/antiforgery?headerName=X-Caller-Selected");
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.OK);
        });

        var response = await ReadHandshakeAsync(scenario);
        await Assert.That(response.HeaderName).IsEqualTo(ManagerAdminFixture.AntiforgeryHeader);
        await Assert.That(response.HeaderName).IsNotEqualTo("X-Caller-Selected");
        await Assert.That(response.RequestToken).IsNotNull().And.IsNotEmpty();
        await Assert.That(scenario.Context.Response.Headers.CacheControl.ToString()).Contains("no-store");
        await Assert.That(scenario.Context.Response.Headers.Pragma.ToString()).Contains("no-cache");
        await Assert.That(scenario.Context.Response.Headers.SetCookie.ToString()).Contains(".AspNetCore.Antiforgery");
    }

    [Test]
    public async Task Antiforgery_handshake_token_uses_the_returned_server_header_and_rejects_missing_or_invalid_mutation_headers_without_details()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);
        var handshake = await fixture.GetHandshakeAsync(managerCookie);

        var missing = await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-identity/authority");
            x.WriteJson(fixture.AuthorityRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", handshake.CookieHeader);
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await AssertThatAntiforgeryFailureIsRedactedAsync(missing, handshake.Response.RequestToken);
        await fixture.AuthorityService.DidNotReceiveWithAnyArgs().ConfigureAsync(default!, default!, default);

        var invalid = await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-identity/authority");
            x.WriteJson(fixture.AuthorityRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", handshake.CookieHeader);
            x.WithRequestHeader(handshake.Response.HeaderName, "not-a-token");
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await AssertThatAntiforgeryFailureIsRedactedAsync(invalid, "not-a-token");
        await fixture.AuthorityService.DidNotReceiveWithAnyArgs().ConfigureAsync(default!, default!, default);

        await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-identity/authority");
            x.WriteJson(fixture.AuthorityRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", handshake.CookieHeader);
            x.WithRequestHeader(handshake.Response.HeaderName, handshake.Response.RequestToken);
            x.StatusCodeShouldBe(HttpStatusCode.OK);
        });
        await fixture.AuthorityService.Received(1).ConfigureAsync(
            Arg.Any<ExternalIdentityManagerScope>(), fixture.AuthorityRequest, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Antiforgery_handshake_redacts_invalid_server_header_configuration()
    {
        const string privateHeader = "private invalid header";
        await using var fixture = await ManagerAdminFixture.CreateAsync(
            options: OptionsWithHeader(privateHeader), useAntiforgeryMiddleware: false);
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        var scenario = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/v1/admin/external-identity/antiforgery");
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.ServiceUnavailable);
        });
        await AssertThatHandshakeUnavailableIsRedactedAsync(scenario, privateHeader);
    }

    [Test]
    public async Task Antiforgery_handshake_redacts_options_resolution_failures()
    {
        const string privateMarker = "private-options-resolution-marker";
        var options = Substitute.For<IOptions<AntiforgeryOptions>>();
        _ = options.Value.Returns(_ => throw new InvalidOperationException(privateMarker));
        var antiforgery = Substitute.For<IAntiforgery>();
        _ = antiforgery.GetAndStoreTokens(Arg.Any<HttpContext>()).Returns(
            new AntiforgeryTokenSet("request-token", "cookie-token", "__RequestVerificationToken", "X-AeroCMS-Antiforgery"));
        await using var fixture = await ManagerAdminFixture.CreateAsync(
            options: options, antiforgery: antiforgery, useAntiforgeryMiddleware: false);
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        var scenario = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/v1/admin/external-identity/antiforgery");
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.ServiceUnavailable);
        });
        await AssertThatHandshakeUnavailableIsRedactedAsync(scenario, privateMarker);
    }

    [Test]
    public async Task Antiforgery_handshake_redacts_antiforgery_provider_failures()
    {
        const string privateMarker = "private-antiforgery-provider-marker";
        var antiforgery = Substitute.For<IAntiforgery>();
        _ = antiforgery.GetAndStoreTokens(Arg.Any<HttpContext>())
            .Returns(_ => throw new InvalidOperationException(privateMarker));
        await using var fixture = await ManagerAdminFixture.CreateAsync(
            antiforgery: antiforgery, useAntiforgeryMiddleware: false);
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        var scenario = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/v1/admin/external-identity/antiforgery");
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.ServiceUnavailable);
        });
        await AssertThatHandshakeUnavailableIsRedactedAsync(scenario, privateMarker);
    }

    [Test]
    public async Task Antiforgery_handshake_redacts_an_absent_provider_token_set()
    {
        var antiforgery = Substitute.For<IAntiforgery>();
        _ = antiforgery.GetAndStoreTokens(Arg.Any<HttpContext>()).Returns((AntiforgeryTokenSet)null!);
        await using var fixture = await ManagerAdminFixture.CreateAsync(
            antiforgery: antiforgery, useAntiforgeryMiddleware: false);
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        var scenario = await fixture.Host.Scenario(x =>
        {
            x.Get.Url("/api/v1/admin/external-identity/antiforgery");
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.ServiceUnavailable);
        });
        await AssertThatHandshakeUnavailableIsRedactedAsync(scenario, "token");
    }

    [Test]
    public async Task Missing_antiforgery_token_blocks_authority_configuration_before_the_service()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-identity/authority");
            x.WriteJson(fixture.AuthorityRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await fixture.AuthorityService.DidNotReceiveWithAnyArgs().ConfigureAsync(default!, default!, default);
    }

    [Test]
    public async Task Missing_antiforgery_token_blocks_invitation_creation_before_the_service()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        await fixture.Host.Scenario(x =>
        {
            x.Post.Url("/api/v1/admin/external-identity/invitations");
            x.WriteJson(fixture.InvitationRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await fixture.LocalAuthentication.DidNotReceiveWithAnyArgs().CreateInvitationAsync(default!, default);
    }

    [Test]
    public async Task Missing_antiforgery_token_blocks_local_password_reset_before_the_service()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        await fixture.Host.Scenario(x =>
        {
            x.Post.Url("/api/v1/admin/external-members/702/local-password-reset");
            x.WriteJson(fixture.PasswordResetRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await fixture.LocalAuthentication.DidNotReceiveWithAnyArgs().IssuePasswordResetAsync(default!, default);
    }

    [Test]
    public async Task Missing_antiforgery_token_blocks_role_assignment_before_the_session_mutation()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);

        await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/702/role");
            x.WriteJson(new ExternalIdentityAdminApi.ExternalMemberRoleAssignmentRequest("PowerUser"), JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await fixture.AssertRoleAsync(ExternalMemberCustomerRole.GeneralUser);
    }

    [Test]
    public async Task Invalid_or_cross_manager_antiforgery_tokens_block_authority_configuration_before_the_service()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();
        var firstCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);
        var firstTokens = await fixture.GetTokenAsync(firstCookie);

        await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-identity/authority");
            x.WriteJson(fixture.AuthorityRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", firstTokens.CookieHeader);
            x.WithRequestHeader(ManagerAdminFixture.AntiforgeryHeader, "not-a-token");
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await fixture.AuthorityService.DidNotReceiveWithAnyArgs().ConfigureAsync(default!, default!, default);

        var secondCookie = await fixture.SignInAsync(ManagerAdminFixture.SecondManagerId);
        await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-identity/authority");
            x.WriteJson(fixture.AuthorityRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", CombineCookies(secondCookie, firstTokens.AntiforgeryCookie));
            x.WithRequestHeader(ManagerAdminFixture.AntiforgeryHeader, firstTokens.RequestToken);
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        await fixture.AuthorityService.DidNotReceiveWithAnyArgs().ConfigureAsync(default!, default!, default);
    }

    [Test]
    public async Task Valid_manager_antiforgery_token_allows_each_legacy_mutation_to_reach_its_existing_callback()
    {
        await using var fixture = await ManagerAdminFixture.CreateAsync();
        var managerCookie = await fixture.SignInAsync(ManagerAdminFixture.FirstManagerId);
        var tokens = await fixture.GetTokenAsync(managerCookie);

        await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-identity/authority");
            x.WriteJson(fixture.AuthorityRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", tokens.CookieHeader);
            x.WithRequestHeader(ManagerAdminFixture.AntiforgeryHeader, tokens.RequestToken);
            x.StatusCodeShouldBe(HttpStatusCode.OK);
        });
        await fixture.AuthorityService.Received(1).ConfigureAsync(
            Arg.Is<ExternalIdentityManagerScope>(value => value.TenantId == ManagerAdminFixture.TenantId && value.SiteId == ManagerAdminFixture.SiteId),
            fixture.AuthorityRequest,
            Arg.Any<CancellationToken>());

        await fixture.Host.Scenario(x =>
        {
            x.Post.Url("/api/v1/admin/external-identity/invitations");
            x.WriteJson(fixture.InvitationRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", tokens.CookieHeader);
            x.WithRequestHeader(ManagerAdminFixture.AntiforgeryHeader, tokens.RequestToken);
            x.StatusCodeShouldBe(HttpStatusCode.OK);
        });
        await fixture.LocalAuthentication.Received(1).CreateInvitationAsync(
            Arg.Is<CreateLocalExternalMemberInvitationRequest>(value => value.TenantId == ManagerAdminFixture.TenantId &&
                value.SiteId == ManagerAdminFixture.SiteId && value.LocalAuthorityId == ManagerAdminFixture.LocalAuthorityId),
            Arg.Any<CancellationToken>());

        await fixture.Host.Scenario(x =>
        {
            x.Post.Url("/api/v1/admin/external-members/702/local-password-reset");
            x.WriteJson(fixture.PasswordResetRequest, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", tokens.CookieHeader);
            x.WithRequestHeader(ManagerAdminFixture.AntiforgeryHeader, tokens.RequestToken);
            x.StatusCodeShouldBe(HttpStatusCode.OK);
        });
        await fixture.LocalAuthentication.Received(1).IssuePasswordResetAsync(
            Arg.Is<IssueLocalExternalMemberPasswordResetRequest>(value => value.ExternalMemberId == 702 &&
                value.IssuedByManagerUserId == ManagerAdminFixture.FirstManagerId),
            Arg.Any<CancellationToken>());

        await fixture.Host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/702/role");
            x.WriteJson(new ExternalIdentityAdminApi.ExternalMemberRoleAssignmentRequest("PowerUser"), JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", tokens.CookieHeader);
            x.WithRequestHeader(ManagerAdminFixture.AntiforgeryHeader, tokens.RequestToken);
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        await fixture.AssertRoleAsync(ExternalMemberCustomerRole.PowerUser);
    }

    private static string CombineCookies(string first, string second) => $"{first}; {second}";

    private static async Task<ExternalIdentityAntiforgeryResponse> ReadHandshakeAsync(IScenarioResult scenario)
    {
        var response = JsonSerializer.Deserialize<ExternalIdentityAntiforgeryResponse>(
            await scenario.ReadAsTextAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Assert.That(response).IsNotNull();
        return response!;
    }

    private static async Task AssertThatAntiforgeryFailureIsRedactedAsync(IScenarioResult scenario, string secret)
    {
        var body = await scenario.ReadAsTextAsync();
        await Assert.That(body).Contains("Antiforgery validation failed.");
        await Assert.That(body).DoesNotContain(secret);
    }

    private static async Task AssertThatHandshakeUnavailableIsRedactedAsync(IScenarioResult scenario, string privateMarker)
    {
        var body = await scenario.ReadAsTextAsync();
        await Assert.That(body).Contains("External identity antiforgery is unavailable.");
        await Assert.That(body).DoesNotContain(privateMarker);
    }

    private static IOptions<AntiforgeryOptions> OptionsWithHeader(string? headerName)
    {
        var options = Substitute.For<IOptions<AntiforgeryOptions>>();
        options.Value.Returns(new AntiforgeryOptions { HeaderName = headerName! });
        return options;
    }

    private sealed class ManagerAdminFixture : IAsyncDisposable
    {
        internal const long TenantId = 700;
        internal const long SiteId = 701;
        internal const long FirstManagerId = 42;
        internal const long SecondManagerId = 43;
        internal const long LocalAuthorityId = 703;
        internal const string AntiforgeryHeader = "X-AeroCMS-Antiforgery";

        private readonly SableTestHarness _harness;

        private ManagerAdminFixture(SableTestHarness harness, IAlbaHost host,
            IExternalIdentityAuthorityService authorityService,
            ILocalExternalMemberAuthenticationService localAuthentication)
        {
            _harness = harness;
            Host = host;
            AuthorityService = authorityService;
            LocalAuthentication = localAuthentication;
        }

        public IAlbaHost Host { get; }
        public IExternalIdentityAuthorityService AuthorityService { get; }
        public ILocalExternalMemberAuthenticationService LocalAuthentication { get; }

        public ConfigureExternalIdentityAuthorityRequest AuthorityRequest { get; } = new(
            ExternalMemberProviders.WorkOs,
            "organization_01HZY9KCW4DTBHQY3H1X8Y7V5Q",
            "https://api.workos.com",
            99,
            "development",
            true);

        public CreateExternalIdentityInvitationRequest InvitationRequest { get; } = new(
            "invitee@example.test", DateTimeOffset.UtcNow.AddHours(1));

        public IssueLocalExternalMemberPasswordResetAdminRequest PasswordResetRequest { get; } = new(
            DateTimeOffset.UtcNow.AddHours(1));

        public static async Task<ManagerAdminFixture> CreateAsync(
            IOptions<AntiforgeryOptions>? options = null,
            IAntiforgery? antiforgery = null,
            bool useAntiforgeryMiddleware = true)
        {
            var harness = new SableTestHarness()
                .WithSchema<SitesModel>(SchemaMode.Flexible)
                .WithConfiguration(options => new IdentityModule().Configure(options));
            await harness.InitializeAsync();
            await SeedAsync(harness.Session);

            var scope = Substitute.For<IExternalIdentityManagerScopeResolver>();
            scope.ResolveAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Prelude.Ok<ExternalIdentityManagerScope, AeroError>(new(TenantId, SiteId))));
            var authority = Substitute.For<IExternalIdentityAuthorityService>();
            authority.ConfigureAsync(Arg.Any<ExternalIdentityManagerScope>(), Arg.Any<ConfigureExternalIdentityAuthorityRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(Prelude.Ok<ExternalIdentityAuthorityResult, AeroError>(new(
                    704, TenantId, ExternalMemberProviders.WorkOs, "https://api.workos.com", "organization_01HZY9KCW4DTBHQY3H1X8Y7V5Q",
                    "https://api.workos.com", 99, "development", true))));
            var local = Substitute.For<ILocalExternalMemberAuthenticationService>();
            local.CreateInvitationAsync(Arg.Any<CreateLocalExternalMemberInvitationRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(Prelude.Ok<ExternalMemberInvitationHandle, AeroError>(new("invite-handle", DateTimeOffset.UtcNow.AddHours(1)))));
            local.IssuePasswordResetAsync(Arg.Any<IssueLocalExternalMemberPasswordResetRequest>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(Prelude.Ok<LocalExternalMemberPasswordResetHandle, AeroError>(new("reset-handle", DateTimeOffset.UtcNow.AddHours(1)))));

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddAntiforgery(options => options.HeaderName = AntiforgeryHeader);
            if (options is not null)
                builder.Services.AddSingleton(options);
            if (antiforgery is not null)
                builder.Services.AddSingleton(antiforgery);
            builder.Services.AddSingleton<IQuerySession>(harness.Session);
            builder.Services.AddSingleton<IDocumentSession>(harness.Session);
            builder.Services.AddSingleton(scope);
            builder.Services.AddSingleton(authority);
            builder.Services.AddSingleton(local);
            builder.Services.AddSingleton(Substitute.For<IExternalMemberIssuanceService>());
            builder.Services.AddSingleton(new ManagerLocalPasswordResetRateLimiter(TimeProvider.System));
            builder.Services.AddScoped<FluentValidation.IValidator<ExternalIdentityAdminApi.ExternalMemberRoleAssignmentRequest>, ExternalMemberRoleAssignmentRequestValidator>();
            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = ManagerRecoveryDefaults.ManagerScheme;
                    options.DefaultAuthenticateScheme = ManagerRecoveryDefaults.ManagerScheme;
                    options.DefaultChallengeScheme = ManagerRecoveryDefaults.ManagerScheme;
                    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
                })
                .AddPolicyScheme(ManagerRecoveryDefaults.ManagerScheme, null, ManagerAuthenticationSchemeRouting.Configure)
                .AddCookie(IdentityConstants.ApplicationScheme, options =>
                {
                    options.Cookie.Name = ".AeroCms.Auth";
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
                });
            builder.Services.AddAuthorization(options => options.AddPolicy("AeroAdmin", policy =>
            {
                policy.AddAuthenticationSchemes(ManagerRecoveryDefaults.ManagerScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireRole("Admin");
            }));

            var host = await AlbaHost.For(builder, app =>
            {
                app.UseAuthentication();
                app.UseAuthorization();
                if (useAntiforgeryMiddleware)
                    app.UseAntiforgery();
                app.MapGet("/test/signin/{managerId:long}", async (long managerId, HttpContext context) =>
                {
                    var principal = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, managerId.ToString()), new Claim(ClaimTypes.Role, "Admin")],
                        IdentityConstants.ApplicationScheme));
                    await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                    return Results.NoContent();
                }).AllowAnonymous();
                app.MapGet("/test/signin/customer-admin", async (HttpContext context) =>
                {
                    var principal = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "44"), new Claim(ClaimTypes.Role, "CustomerAdmin")],
                        IdentityConstants.ApplicationScheme));
                    await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                    return Results.NoContent();
                }).AllowAnonymous();
                app.MapGet("/test/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
                {
                    var tokens = antiforgery.GetAndStoreTokens(context);
                    return Results.Ok(new { requestToken = tokens.RequestToken });
                }).RequireAuthorization("AeroAdmin");
                app.MapExternalIdentityAdminApi();
            });
            return new ManagerAdminFixture(harness, host, authority, local);
        }

        public async Task<string> SignInAsync(long managerId)
        {
            var scenario = await Host.Scenario(x =>
            {
                x.Get.Url($"/test/signin/{managerId}");
                x.StatusCodeShouldBe(HttpStatusCode.NoContent);
            });
            return ExtractCookie(scenario.Context.Response.Headers.SetCookie.ToString());
        }

        public async Task<string> SignInCustomerAdminAsync()
        {
            var scenario = await Host.Scenario(x =>
            {
                x.Get.Url("/test/signin/customer-admin");
                x.StatusCodeShouldBe(HttpStatusCode.NoContent);
            });
            return ExtractCookie(scenario.Context.Response.Headers.SetCookie.ToString());
        }

        public async Task<HandshakeTokens> GetHandshakeAsync(string managerCookie)
        {
            var scenario = await Host.Scenario(x =>
            {
                x.Get.Url("/api/v1/admin/external-identity/antiforgery");
                x.WithRequestHeader("Cookie", managerCookie);
                x.StatusCodeShouldBe(HttpStatusCode.OK);
            });
            var response = await ReadHandshakeAsync(scenario);
            var antiforgeryCookie = ExtractCookie(scenario.Context.Response.Headers.SetCookie.ToString());
            return new HandshakeTokens(response, CombineCookies(managerCookie, antiforgeryCookie));
        }

        public async Task<AntiforgeryTokens> GetTokenAsync(string managerCookie)
        {
            var scenario = await Host.Scenario(x =>
            {
                x.Get.Url("/test/antiforgery");
                x.WithRequestHeader("Cookie", managerCookie);
                x.StatusCodeShouldBe(HttpStatusCode.OK);
            });
            using var json = JsonDocument.Parse(await scenario.ReadAsTextAsync());
            var requestToken = json.RootElement.GetProperty("requestToken").GetString();
            if (string.IsNullOrWhiteSpace(requestToken))
                throw new InvalidOperationException("The test host did not produce an antiforgery request token.");
            var antiforgeryCookie = ExtractCookie(scenario.Context.Response.Headers.SetCookie.ToString());
            return new AntiforgeryTokens(requestToken, antiforgeryCookie, CombineCookies(managerCookie, antiforgeryCookie));
        }

        public async Task AssertRoleAsync(ExternalMemberCustomerRole expected)
        {
            var assignment = await _harness.Session.Query<ExternalMemberSiteAssignment>()
                .FirstOrDefaultAsync(value => value.ExternalMemberId == 702 && value.TenantId == TenantId && value.SiteId == SiteId);
            await Assert.That(assignment).IsNotNull();
            await Assert.That(assignment!.CustomerRole).IsEqualTo(expected);
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            await _harness.DisposeAsync();
        }

        private static async Task SeedAsync(IDocumentSession session)
        {
            session.Store(new ExternalMemberLocalAuthority { Id = LocalAuthorityId, TenantId = TenantId, IsActive = true });
            session.Store(new ExternalMemberSiteAssignment
            {
                Id = 705,
                ExternalMemberId = 702,
                TenantId = TenantId,
                SiteId = SiteId,
                IsActive = true,
                CustomerRole = ExternalMemberCustomerRole.GeneralUser
            });
            await session.SaveChangesAsync();
        }

        private static string ExtractCookie(string setCookie) => setCookie.Split(';', 2)[0];

        public sealed record AntiforgeryTokens(string RequestToken, string AntiforgeryCookie, string CookieHeader);

        public sealed record HandshakeTokens(ExternalIdentityAntiforgeryResponse Response, string CookieHeader);
    }
}
