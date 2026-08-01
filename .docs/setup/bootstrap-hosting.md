# Aero CMS bootstrap and hosting lifecycle

## Status

This document describes the current setup and hosting integration. Superseded plans and progress notes live under `.docs/legacy/setup/` and are not implementation guidance.

## Consumer host

An application hosting Aero CMS uses the ordinary ASP.NET Core lifecycle:

```csharp
var builder = WebApplication.CreateBuilder(args);

await builder.AddAeroCmsAsync<Program>(
    args,
    GeneratedAeroCmsHostCatalog.Configure);

var app = builder.Build();

app.UseAeroCms();
app.MapAeroCms<App>();

await app.RunAsync();
```

The host can register its own services after `AddAeroCmsAsync`, insert its own middleware around `UseAeroCms`, and map application endpoints before or after `MapAeroCms` as endpoint ordering requires.

`Aero.Cms.Web.Bootstrap` owns this integration surface for now. Creating another extensions project would add a package boundary without removing any current dependency. A later packaging pass can rename or fold the project after its public API and dependency graph stabilize.

## Why registration is asynchronous

`AddAeroCmsAsync` hides the one-time first-run handoff. When setup is incomplete, it temporarily serves the existing setup application and waits for `Setup.razor` to persist configuration and request the handoff. It then reloads configuration into the normal builder before service registration continues.

The normal application still has one `WebApplication`, one `Build()` call, and the standard `RunAsync()` lifetime. The lightweight setup application exists only during an incomplete first-run installation; it is not a second long-running web application.

## Setup wizard

`Aero.Cms.Modules.Setup/Areas/Setup/Pages/Setup.razor` remains the setup experience. The wizard still collects deployment choices, administrator/site data, and the information required by the selected secret provider.

Database deployment remains a wizard choice:

- `Embedded` uses SurrealDB through the embedded SurrealKV provider.
- `Server` uses an external SurrealDB endpoint and protected credentials or references.

Cache deployment remains independently selectable. Embedded Garnet readiness is relevant only when the chosen cache topology requires it. The setup wizard does not need to wait for Garnet merely to render or persist the initial choices.

## Configuration boundaries

Setup lifecycle state and deployment topology are deliberately separate.

### `AeroCms:Bootstrap`

This section records lifecycle state and setup-completion metadata, including:

- `State`: `Setup`, `Configured`, `Running`, or `Failed`
- `HasBootstrapConfig`
- setup authentication and handoff metadata

Infrastructure settings alone do not imply that setup is complete. This prevents an operator-provided database mode from accidentally skipping the wizard.

### `AeroCms:Infrastructure`

This section records non-secret deployment topology and protected secret references:

- `DatabaseMode`
- `CacheMode`
- `SecretProvider`
- provider-specific connection metadata and protected references

Plain-text credentials do not belong in committed appsettings files. Local protected material continues through the configured protection service, while external secret providers persist references and protected authentication material. Aero Vault can replace or extend that implementation later without changing the lifecycle model.

ASP.NET Core configuration precedence still applies. Environment variables or another higher-priority provider may preconfigure infrastructure. Until a phase-two option explicitly locks a setup choice, the wizard remains the authoritative first-run confirmation and persistence step.

## Runtime initialization

The normal host registers `AeroCmsRuntimeInitializationHostedService`. Its `StartAsync` method runs before the server begins accepting requests and performs the startup barrier:

1. wait for only the infrastructure required by the selected topology;
2. run pending runtime bootstrap/seeding when state is `Configured`;
3. initialize modules in generated dependency/load order;
4. signal runtime readiness;
5. fail host startup and persist `Failed` when initialization cannot complete.

The middleware readiness gate protects both `Configured` and `Running` states. `Configured` means configuration was saved, not that the runtime is ready for traffic.

## Extension points

Phase one intentionally exposes a small surface:

- `AddAeroCmsAsync` for configuration and service registration;
- `UseAeroCms` for middleware;
- `MapAeroCms` for components and endpoints;
- the generated host catalog callback for reflection-free module wiring.

Phase two may add narrowly scoped options callbacks, such as:

```csharp
await builder.AddAeroCmsAsync<Program>(args, options =>
{
    // Surgical service and feature customization.
});

app.UseAeroCms(options =>
{
    // Surgical middleware customization.
});
```

Those callbacks should customize explicit policies and feature registrations rather than expose the internal setup handoff or module initialization sequence.
