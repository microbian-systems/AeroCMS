using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Alba;
using Aero.Cms.Abstractions.Authentication;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Modules.Identity;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Aero.Cms.Core.Tests.Authentication;

public sealed class ExternalIdentityAdminApiLifecycleTests
{
    private const string AuthorityRoute = "api/v1/admin/external-identity/authority";
    private const string InvitationRoute = "api/v1/admin/external-identity/invitations";
    private const string ExternalMembersRoute = "api/v1/admin/external-members";

    [Test]
    public async Task Lifecycle_request_rejects_missing_activity_or_security_version_instead_of_defaulting_to_disable()
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var complete = JsonSerializer.Deserialize<ExternalMemberLifecycleRequest>(
            "{\"isActive\":false,\"expectedSecurityVersion\":3}", jsonOptions);
        await Assert.That(complete).IsNotNull();
        await Assert.That(complete!.IsActive).IsFalse();
        await Assert.That(complete.ExpectedSecurityVersion).IsEqualTo(3);
        await Assert.That(() => JsonSerializer.Deserialize<ExternalMemberLifecycleRequest>(
            "{\"expectedSecurityVersion\":3}", jsonOptions))
            .Throws<JsonException>();
        await Assert.That(() => JsonSerializer.Deserialize<ExternalMemberLifecycleRequest>(
            "{\"isActive\":false}", jsonOptions))
            .Throws<JsonException>();
    }

    [Test]
    public async Task Lifecycle_route_is_aero_admin_only_put_and_requires_antiforgery()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IExternalIdentityManagerScopeResolver>());
        builder.Services.AddSingleton(Substitute.For<IExternalMemberLifecycleService>());
        await using var app = builder.Build();
        app.MapExternalIdentityAdminApi();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(value => value.RoutePattern.RawText == "/api/v1/admin/external-members/{memberId:long}/lifecycle");

        await Assert.That(endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods).Contains("PUT");
        await Assert.That(endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation).IsTrue();
        await Assert.That(endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Any(value => value.Policy == "AeroAdmin")).IsTrue();
    }

    [Test]
    public async Task Manager_scheme_global_admin_and_antiforgery_are_required_before_lifecycle_resolution()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAntiforgery();
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
            })
            .AddCookie("wrong-manager", options =>
            {
                options.Cookie.Name = ".Wrong.Manager";
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization(options => options.AddPolicy("AeroAdmin", policy =>
        {
            policy.AddAuthenticationSchemes(ManagerRecoveryDefaults.ManagerScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin");
        }));
        var scopeResolver = Substitute.For<IExternalIdentityManagerScopeResolver>();
        scopeResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Prelude.Ok<ExternalIdentityManagerScope, AeroError>(new(1, 2))));
        builder.Services.AddSingleton(scopeResolver);
        var lifecycle = Substitute.For<IExternalMemberLifecycleService>();
        lifecycle.TransitionAsync(Arg.Any<ExternalMemberLifecycleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Prelude.Fail<ExternalMemberLifecycleResult, AeroError>(
                AeroError.DatabaseError("private store exception"))));
        builder.Services.AddSingleton(lifecycle);
        builder.Services.AddScoped<FluentValidation.IValidator<ExternalMemberLifecycleRequest>, ExternalMemberLifecycleRequestValidator>();
        await using var host = await AlbaHost.For(builder, app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.MapGet("/test/signin/customer-admin", async (HttpContext context) =>
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, "CustomerAdmin")],
                    IdentityConstants.ApplicationScheme));
                await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                return Results.NoContent();
            }).AllowAnonymous();
            app.MapGet("/test/signin/admin", async (HttpContext context) =>
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, "Admin")],
                    IdentityConstants.ApplicationScheme));
                await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                return Results.NoContent();
            }).AllowAnonymous();
            app.MapGet("/test/signin/wrong-scheme-admin", async (HttpContext context) =>
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Role, "Admin")],
                    "wrong-manager"));
                await context.SignInAsync("wrong-manager", principal);
                return Results.NoContent();
            }).AllowAnonymous();
            app.MapExternalIdentityAdminApi();
        });

        await host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/9/lifecycle");
            x.WriteJson(new ExternalMemberLifecycleRequest
            {
                IsActive = false,
                ExpectedSecurityVersion = 3
            }, JsonStyle.MinimalApi);
            x.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });

        var signIn = await host.Scenario(x =>
        {
            x.Get.Url("/test/signin/customer-admin");
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var cookie = signIn.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        await host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/9/lifecycle");
            x.WriteJson(new ExternalMemberLifecycleRequest
            {
                IsActive = false,
                ExpectedSecurityVersion = 3
            }, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", cookie);
            x.StatusCodeShouldBe(HttpStatusCode.Forbidden);
        });

        var wrongSchemeSignIn = await host.Scenario(x =>
        {
            x.Get.Url("/test/signin/wrong-scheme-admin");
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var wrongSchemeCookie = wrongSchemeSignIn.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        await host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/9/lifecycle");
            x.WriteJson(new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 }, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", wrongSchemeCookie);
            x.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });

        var adminSignIn = await host.Scenario(x =>
        {
            x.Get.Url("/test/signin/admin");
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var adminCookie = adminSignIn.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        await host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/9/lifecycle");
            x.WriteJson(new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 }, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", adminCookie);
            x.StatusCodeShouldBe(HttpStatusCode.BadRequest);
        });
        lifecycle.DidNotReceiveWithAnyArgs().TransitionAsync(default!, default);
    }

    [Test]
    public async Task Valid_antiforgery_manager_request_uses_the_exact_single_manager_and_selected_scope()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAntiforgery();
        var antiforgery = Substitute.For<IAntiforgery>();
        antiforgery.ValidateRequestAsync(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(antiforgery);
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
            });
        builder.Services.AddAuthorization(options => options.AddPolicy("AeroAdmin", policy =>
        {
            policy.AddAuthenticationSchemes(ManagerRecoveryDefaults.ManagerScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin");
        }));
        var scopeResolver = Substitute.For<IExternalIdentityManagerScopeResolver>();
        scopeResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Prelude.Ok<ExternalIdentityManagerScope, AeroError>(new(401, 402))));
        builder.Services.AddSingleton(scopeResolver);
        var lifecycle = Substitute.For<IExternalMemberLifecycleService>();
        lifecycle.TransitionAsync(Arg.Any<ExternalMemberLifecycleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Prelude.Fail<ExternalMemberLifecycleResult, AeroError>(
                AeroError.DatabaseError("private store detail"))));
        builder.Services.AddSingleton(lifecycle);
        builder.Services.AddScoped<FluentValidation.IValidator<ExternalMemberLifecycleRequest>, ExternalMemberLifecycleRequestValidator>();
        await using var host = await AlbaHost.For(builder, app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.MapGet("/test/signin/admin", async (HttpContext context) =>
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "403"), new Claim(ClaimTypes.Role, "Admin")],
                    IdentityConstants.ApplicationScheme));
                await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                return Results.NoContent();
            }).AllowAnonymous();
            app.MapGet("/test/signin/ambiguous", async (HttpContext context) =>
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "403"), new Claim(ClaimTypes.NameIdentifier, "404"),
                        new Claim(ClaimTypes.Role, "Admin")],
                    IdentityConstants.ApplicationScheme));
                await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                return Results.NoContent();
            }).AllowAnonymous();
            app.MapExternalIdentityAdminApi();
        });

        var ambiguousSignIn = await host.Scenario(x =>
        {
            x.Get.Url("/test/signin/ambiguous");
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var ambiguousCookie = ambiguousSignIn.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        await host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/405/lifecycle");
            x.WriteJson(new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 }, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", ambiguousCookie);
            x.StatusCodeShouldBe(HttpStatusCode.Forbidden);
        });
        lifecycle.DidNotReceiveWithAnyArgs().TransitionAsync(default!, default);

        var adminSignIn = await host.Scenario(x =>
        {
            x.Get.Url("/test/signin/admin");
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var adminCookie = adminSignIn.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        await host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/405/lifecycle");
            x.WriteJson(new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 }, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", adminCookie);
            x.StatusCodeShouldBe(HttpStatusCode.ServiceUnavailable);
        });
        await lifecycle.Received(1).TransitionAsync(
            Arg.Is<ExternalMemberLifecycleCommand>(value => value.TenantId == 401 && value.SiteId == 402 &&
                value.ManagerUserId == 403 && value.ExternalMemberId == 405 && !value.IsActive &&
                value.ExpectedSecurityVersion == 3),
            Arg.Any<CancellationToken>());
        antiforgery.Received().ValidateRequestAsync(Arg.Any<HttpContext>());
    }

    [Test]
    public async Task Real_antiforgery_token_round_trip_reaches_unavailable_scope_without_calling_lifecycle_service()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAntiforgery();
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
            });
        builder.Services.AddAuthorization(options => options.AddPolicy("AeroAdmin", policy =>
        {
            policy.AddAuthenticationSchemes(ManagerRecoveryDefaults.ManagerScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin");
        }));
        var scopeResolver = Substitute.For<IExternalIdentityManagerScopeResolver>();
        scopeResolver.ResolveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(
            Prelude.Fail<ExternalIdentityManagerScope, AeroError>(AeroError.NotFoundError("scope unavailable"))));
        builder.Services.AddSingleton(scopeResolver);
        var lifecycle = Substitute.For<IExternalMemberLifecycleService>();
        builder.Services.AddSingleton(lifecycle);
        builder.Services.AddScoped<FluentValidation.IValidator<ExternalMemberLifecycleRequest>, ExternalMemberLifecycleRequestValidator>();
        await using var host = await AlbaHost.For(builder, app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.MapGet("/test/signin/admin", async (HttpContext context) =>
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "403"), new Claim(ClaimTypes.Role, "Admin")],
                    IdentityConstants.ApplicationScheme));
                await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                return Results.NoContent();
            }).AllowAnonymous();
            app.MapGet("/test/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
                Results.Ok(new AntiforgeryTokenResponse(antiforgery.GetAndStoreTokens(context).RequestToken!)))
                .RequireAuthorization("AeroAdmin");
            app.MapExternalIdentityAdminApi();
        });

        var signIn = await host.Scenario(x =>
        {
            x.Get.Url("/test/signin/admin");
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var managerCookie = signIn.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        var tokenResponse = await host.Scenario(x =>
        {
            x.Get.Url("/test/antiforgery");
            x.WithRequestHeader("Cookie", managerCookie);
            x.StatusCodeShouldBe(HttpStatusCode.OK);
        });
        var antiforgeryCookie = tokenResponse.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        var token = await tokenResponse.ReadAsJsonAsync<AntiforgeryTokenResponse>();
        await Assert.That(token).IsNotNull();

        await host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/405/lifecycle");
            x.WriteJson(new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 }, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", $"{managerCookie}; {antiforgeryCookie}");
            x.WithRequestHeader("RequestVerificationToken", token!.RequestToken);
            x.StatusCodeShouldBe(HttpStatusCode.NotFound);
        });
        lifecycle.DidNotReceiveWithAnyArgs().TransitionAsync(default!, default);
    }

    [Test]
    public async Task Lifecycle_commit_outcome_unknown_is_redacted_to_a_fixed_service_unavailable_code()
    {
        await AssertLifecycleFaultIsRedactedAsync(
            new CommitOutcomeUnknownException(new InvalidOperationException("private transport detail")),
            "lifecycle_commit_unknown",
            "private transport detail");
    }

    [Test]
    public async Task Lifecycle_post_commit_callback_failure_is_redacted_to_a_distinct_fixed_service_unavailable_code()
    {
        await AssertLifecycleFaultIsRedactedAsync(
            new PostCommitCallbackException(new InvalidOperationException("private callback detail")),
            "lifecycle_committed_postprocessing_failed",
            "private callback detail");
    }

    [Test]
    public async Task Lifecycle_unexpected_failure_is_redacted_to_a_fixed_service_unavailable_code()
    {
        await AssertLifecycleFaultIsRedactedAsync(
            new InvalidOperationException("private lifecycle detail"),
            "lifecycle_unavailable",
            "private lifecycle detail");
    }

    [Test]
    public async Task Lifecycle_client_preserves_the_commit_outcome_unknown_service_unavailable_code()
    {
        await AssertLifecycleClientFailureCodeAsync("lifecycle_commit_unknown");
    }

    [Test]
    public async Task Lifecycle_client_preserves_the_post_commit_processing_service_unavailable_code()
    {
        await AssertLifecycleClientFailureCodeAsync("lifecycle_committed_postprocessing_failed");
    }

    [Test]
    public async Task Sender_dispatches_all_external_identity_admin_routes()
    {
        var sender = new RecordingSender();
        var client = new ExternalIdentityAdminClient(new HttpClient(), sender);

        await client.GetAuthorityAsync();
        await client.SetLifecycleAsync(405, new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 });
        await client.ConfigureAuthorityAsync(
            new ConfigureExternalIdentityAuthorityRequest("workos", "org-1", "https://api.workos.com", 9, "test", true));
        await client.CreateInvitationAsync(
            new CreateExternalIdentityInvitationRequest("member@example.test", DateTimeOffset.UtcNow.AddHours(1)));
        await client.IssueLocalPasswordResetAsync(405,
            new IssueLocalExternalMemberPasswordResetAdminRequest(DateTimeOffset.UtcNow.AddHours(1)));

        await Assert.That(sender.Requests).IsEquivalentTo([
            new RequestCapture("GET", AuthorityRoute),
            new RequestCapture("PUT", $"{ExternalMembersRoute}/405/lifecycle"),
            new RequestCapture("PUT", AuthorityRoute),
            new RequestCapture("POST", InvitationRoute),
            new RequestCapture("POST", $"{ExternalMembersRoute}/405/local-password-reset")]);
    }

    [Test]
    public async Task Unsafe_external_identity_admin_requests_fail_closed_without_a_sender()
    {
        using var httpClient = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("https://cms.test/") };
        var client = new ExternalIdentityAdminClient(httpClient);

        var lifecycle = await client.SetLifecycleAsync(405, new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 });
        var authority = await client.ConfigureAuthorityAsync(
            new ConfigureExternalIdentityAuthorityRequest("workos", "org-1", "https://api.workos.com", 9, "test", true));
        var invitation = await client.CreateInvitationAsync(
            new CreateExternalIdentityInvitationRequest("member@example.test", DateTimeOffset.UtcNow.AddHours(1)));
        var reset = await client.IssueLocalPasswordResetAsync(405,
            new IssueLocalExternalMemberPasswordResetAdminRequest(DateTimeOffset.UtcNow.AddHours(1)));

        await Assert.That(lifecycle).IsTypeOf<Result<ExternalMemberLifecycleResponse, AeroError>.Failure>();
        await Assert.That(authority).IsTypeOf<Result<ExternalIdentityAuthorityState, AeroError>.Failure>();
        await Assert.That(invitation).IsTypeOf<Result<ExternalIdentityInvitationResponse, AeroError>.Failure>();
        await Assert.That(reset).IsTypeOf<Result<LocalExternalMemberPasswordResetResponse, AeroError>.Failure>();
    }

    [Test]
    public async Task Get_authority_falls_back_to_the_http_client_without_a_sender()
    {
        var handler = new CountingUnavailableHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://cms.test/") };
        var client = new ExternalIdentityAdminClient(httpClient);

        var result = await client.GetAuthorityAsync();

        await Assert.That(result).IsTypeOf<Result<ExternalIdentityAuthorityState, AeroError>.Failure>();
        await Assert.That(handler.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task Typed_http_client_uses_the_registered_request_sender()
    {
        var sender = new RecordingSender();
        var services = new ServiceCollection();
        services.AddSingleton<IExternalIdentityAdminRequestSender>(sender);
        services.AddHttpClient<IExternalIdentityAdminClient, ExternalIdentityAdminClient>(client =>
                client.BaseAddress = new Uri("https://cms.test/"))
            .ConfigurePrimaryHttpMessageHandler(ThrowingHandler.Create);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IExternalIdentityAdminClient>();

        await client.SetLifecycleAsync(405, new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 });

        await Assert.That(sender.Requests).IsEquivalentTo([
            new RequestCapture("PUT", $"{ExternalMembersRoute}/405/lifecycle")]);
    }

    private static async Task AssertLifecycleFaultIsRedactedAsync(
        Exception exception,
        string expectedCode,
        string privateDetail)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAntiforgery();
        var antiforgery = Substitute.For<IAntiforgery>();
        antiforgery.ValidateRequestAsync(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(antiforgery);
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
            });
        builder.Services.AddAuthorization(options => options.AddPolicy("AeroAdmin", policy =>
        {
            policy.AddAuthenticationSchemes(ManagerRecoveryDefaults.ManagerScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin");
        }));
        var scopeResolver = Substitute.For<IExternalIdentityManagerScopeResolver>();
        scopeResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Prelude.Ok<ExternalIdentityManagerScope, AeroError>(new(401, 402))));
        builder.Services.AddSingleton(scopeResolver);
        var lifecycle = Substitute.For<IExternalMemberLifecycleService>();
        lifecycle.TransitionAsync(Arg.Any<ExternalMemberLifecycleCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<ExternalMemberLifecycleResult, AeroError>>(exception));
        builder.Services.AddSingleton(lifecycle);
        builder.Services.AddScoped<FluentValidation.IValidator<ExternalMemberLifecycleRequest>, ExternalMemberLifecycleRequestValidator>();
        await using var host = await AlbaHost.For(builder, app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAntiforgery();
            app.MapGet("/test/signin/admin", async (HttpContext context) =>
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "403"), new Claim(ClaimTypes.Role, "Admin")],
                    IdentityConstants.ApplicationScheme));
                await context.SignInAsync(IdentityConstants.ApplicationScheme, principal);
                return Results.NoContent();
            }).AllowAnonymous();
            app.MapExternalIdentityAdminApi();
        });

        var signIn = await host.Scenario(x =>
        {
            x.Get.Url("/test/signin/admin");
            x.StatusCodeShouldBe(HttpStatusCode.NoContent);
        });
        var cookie = signIn.Context.Response.Headers.SetCookie.ToString().Split(';', 2)[0];
        var scenario = await host.Scenario(x =>
        {
            x.Put.Url("/api/v1/admin/external-members/405/lifecycle");
            x.WriteJson(new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 }, JsonStyle.MinimalApi);
            x.WithRequestHeader("Cookie", cookie);
            x.StatusCodeShouldBe(HttpStatusCode.ServiceUnavailable);
        });
        var body = await scenario.ReadAsTextAsync();

        await Assert.That(body).Contains(expectedCode);
        await Assert.That(body).DoesNotContain(privateDetail);
        antiforgery.Received().ValidateRequestAsync(Arg.Any<HttpContext>());
    }

    private static async Task AssertLifecycleClientFailureCodeAsync(string expectedCode)
    {
        var sender = new FixedResponseSender(
            $"{{\"code\":\"{expectedCode}\",\"detail\":\"private server text\"}}");
        var client = new ExternalIdentityAdminClient(new HttpClient(), sender);

        var result = await client.SetLifecycleAsync(405,
            new ExternalMemberLifecycleRequest { IsActive = false, ExpectedSecurityVersion = 3 });

        var error = result is Result<ExternalMemberLifecycleResponse, AeroError>.Failure(var value)
            ? value
            : throw new InvalidOperationException("Expected an unavailable lifecycle result.");
        await Assert.That(error).IsTypeOf<AeroError.HttpRequest>();
        var request = (AeroError.HttpRequest)error;
        await Assert.That(request.code).IsEqualTo(HttpStatusCode.ServiceUnavailable);
        await Assert.That(request.msg).IsEqualTo(expectedCode);
    }

    private sealed class FixedResponseSender(string body) : IExternalIdentityAdminRequestSender
    {
        public Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken = default) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/problem+json")
        });
    }

    private sealed class RecordingSender : IExternalIdentityAdminRequestSender
    {
        public List<RequestCapture> Requests { get; } = [];

        public Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(new RequestCapture(request.Method.Method, request.RequestUri!.OriginalString));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class CountingUnavailableHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public static ThrowingHandler Create() => new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => throw new InvalidOperationException("No HTTP request expected.");
    }

    private sealed record RequestCapture(string Method, string Route);

    private sealed record AntiforgeryTokenResponse(string RequestToken);
}
