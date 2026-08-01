using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Aero.Cms.Modules.RateLimiting;
using Aero.Core.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aero.Cms.Core.Tests.Infrastructure;

public sealed class AeroRateLimitingInfrastructureTests
{
    [Test]
    public async Task FixedWindowPolicyRejectsWithSafeProblemAndRetryMetadata()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<ISiteContext>(new TestSiteContext(17, 23));

        new RateLimitingModule().ConfigureServices(builder.Services);
        builder.Services.AddAeroFixedWindowRateLimitPolicy(
            configuration: null,
            policyName: "Test.Fixed",
            configurationName: "TestFixed",
            defaults: new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 1,
                WindowSeconds = 60,
                QueueLimit = 0
            });

        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/limited", () => TypedResults.Ok("accepted"))
            .RequireRateLimiting("Test.Fixed");
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var accepted = await client.GetAsync("/limited");
        using var rejected = await client.GetAsync("/limited");

        await Assert.That(accepted.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(rejected.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
        await Assert.That(rejected.Headers.CacheControl?.NoStore).IsTrue();
        await Assert.That(rejected.Headers.Contains("X-Correlation-Id")).IsTrue();
        await Assert.That(rejected.Headers.RetryAfter?.Delta).IsNotNull();

        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();
        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.Status).IsEqualTo(StatusCodes.Status429TooManyRequests);
        await Assert.That(problem.Title).IsEqualTo("Too many requests");
        await Assert.That(problem.Extensions.ContainsKey("correlationId")).IsTrue();
    }

    [Test]
    public async Task PartitionKeyUsesAuthenticatedPrincipalAndSiteWithoutRequestSecrets()
    {
        var services = new ServiceCollection()
            .AddSingleton<ISiteContext>(new TestSiteContext(101, 202))
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "303")],
                    authenticationType: "test"))
        };
        context.Request.Headers.Authorization = "Bearer secret-value";

        var key = AeroRateLimitPartitionKeyFactory.Create(
            context,
            AeroRateLimitPolicyNames.AiManager);

        await Assert.That(key).IsEqualTo("101|202|principal|303|Aero.Ai.Manager");
        await Assert.That(key.Contains("secret-value", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task InvalidPolicyConfigurationFailsDuringRegistration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AeroCms:RateLimiting:Policies:Invalid:PermitLimit"] = "0"
            })
            .Build();
        var services = new ServiceCollection();

        Action action = () => services.AddAeroFixedWindowRateLimitPolicy(
            configuration,
            "Test.Invalid",
            "Invalid",
            new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 1,
                WindowSeconds = 60,
                QueueLimit = 0
            });

        await Assert.That(action).Throws<OptionsValidationException>();
    }

    [Test]
    public async Task ApplicationPolicyLimitsOneSubjectWithoutAffectingAnother()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAeroApplicationFixedWindowRateLimitPolicy(
            configuration: null,
            policyName: "Test.Application",
            configurationName: "TestApplication",
            defaults: new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 1,
                WindowSeconds = 60,
                QueueLimit = 0
            });

        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IAeroApplicationRateLimiter>();
        var firstSubject = new AeroRateLimitSubject(11, 22, "mcp", "principal", "33");
        var secondSubject = firstSubject with { SiteId = 44 };

        var first = await limiter.AcquireAsync("Test.Application", firstSubject);
        var rejected = await limiter.AcquireAsync("Test.Application", firstSubject);
        var isolated = await limiter.AcquireAsync("Test.Application", secondSubject);

        await Assert.That(first.IsAcquired).IsTrue();
        await Assert.That(rejected.IsAcquired).IsFalse();
        await Assert.That(rejected.RetryAfter).IsNotNull();
        await Assert.That(isolated.IsAcquired).IsTrue();
    }

    [Test]
    public async Task MissingApplicationPolicyFailsClosed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAeroApplicationFixedWindowRateLimitPolicy(
            configuration: null,
            policyName: "Test.Registered",
            configurationName: "TestRegistered",
            defaults: new AeroFixedWindowRateLimitOptions
            {
                PermitLimit = 1,
                WindowSeconds = 60,
                QueueLimit = 0
            });

        using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IAeroApplicationRateLimiter>();

        var action = async () => await limiter.AcquireAsync(
            "Test.Missing",
            new AeroRateLimitSubject(11, 22, "mcp", "principal", "33"));

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    private sealed record TestSiteContext(long TenantId, long SiteId) : ISiteContext;
}
