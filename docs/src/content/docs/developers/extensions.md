---
title: Extension development
description: Source-generated AeroCMS feature modules, endpoints, services, persistence, UI, validation, and tests.
---

An AeroCMS extension is a Razor Class Library or .NET project that participates in the compile-time module catalog. Keep the feature inside `Aero.Cms.Modules.<FeatureName>`, avoid reflection-based discovery, and depend only on the contracts the feature actually needs.

## Shape of a module

This example is illustrative but uses the current AeroCMS web-module boundary:

```csharp
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;

namespace Aero.Cms.Modules.Weather;

[Module(nameof(WeatherModule))]
public sealed class WeatherModule : AeroWebModule, IAeroPipelineModule
{
    public override string Name => nameof(WeatherModule);
    public override string Version => "0.1.0-alpha";
    public override string Author => "Your team";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["content"];
    public override IReadOnlyList<string> Tags => ["weather"];
    public int PipelineOrder => 500;

    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration? config = null,
        IHostEnvironment? env = null)
    {
        services.AddScoped<IWeatherService, WeatherService>();
        services.AddScoped<IValidator<CreateForecastRequest>, CreateForecastValidator>();
    }

    public override Task RunAsync(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/weather", GetForecasts)
            .RequireAuthorization("site:read");
        return Task.CompletedTask;
    }

    public void ConfigurePipeline(IApplicationBuilder app)
    {
        // Add middleware only when endpoint routing cannot express the behavior.
    }
}
```

The source generator emits descriptors consumed by the host's generated catalog. Do not add assembly scanning or runtime reflection as a fallback.

## Business and persistence flow

Use a thin endpoint, FluentValidation, a business/application service returning `Result<T>` or `Option<T>`, and a Sable session/repository at the data boundary:

```csharp
public async Task<Result<Forecast>> CreateAsync(
    CreateForecast command,
    CancellationToken cancellationToken)
{
    var existing = await querySession.Query<Forecast>()
        .FirstOrDefaultAsync(x =>
            x.SiteId == command.SiteId &&
            x.Culture == command.Culture &&
            x.Slug == command.Slug,
            cancellationToken);

    if (existing is not null)
    {
        return AeroError.ValidationError(["A forecast with this slug already exists."]);
    }

    var forecast = Forecast.Create(Snowflake.NewId(), command);
    documentSession.Store(forecast);
    await documentSession.SaveChangesAsync(cancellationToken);
    return forecast;
}
```

This is illustrative: exact Sable query methods depend on the current referenced contract. The required behavior is site/culture scope, Snowflake IDs, railway errors for expected failures, and one explicit commit boundary.

## Endpoints and authorization

Prefer minimal APIs. Manager/admin endpoints require authentication and the least `site:*` policy. Public endpoints must be explicitly anonymous and must filter to published records for the resolved site/culture. Treat IDs in route values as untrusted and re-check ownership after load.

## UI

Prefer Razor/Blazor with code-behind files. Reuse manager layouts and tokens. Use TypeScript only for browser behavior that cannot be expressed cleanly in Blazor or CSS. Do not add npm; use the existing CDN, LibMan, TypeScript MSBuild, committed asset, and Tailwind standalone workflows.

## Verification

- Unit tests: TUnit.
- ASP.NET Core integration: Alba.
- Browser integration: Microsoft Playwright.
- Mocking: NSubstitute or FakeItEasy where beneficial; no Moq.
- Validate endpoint authorization metadata, foreign-site access, wrong culture, draft/public state, cancellation, and railway error mapping.

Build a focused project first, then the Web host. If UI/theme sources changed, regenerate committed theme assets before the final Web build.

See the [public API reference](/api/index.html) for the supported AeroCMS contracts. External Git-submodule APIs are intentionally not regenerated here.
