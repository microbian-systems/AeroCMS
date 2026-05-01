# Refactor Plan: Consolidate Setup Web App into Aero.Cms.Modules.Setup

## Problem

The setup web application factory lives in `Aero.Cms.Web/Setup/SetupWebApplication.cs` — outside the Setup module project where it belongs.

**Two specific issues:**

1. **Wrong home** — `SetupWebApplication.cs` is in `Aero.Cms.Web`, with a `// todo - move this` comment on line 16. The Setup module project (`Aero.Cms.Modules.Setup`) already has all the bootstrap service interfaces and implementations. The factory should live alongside them.

2. **Duplicate registrations** — `SetupWebApplication.RegisterBootstrapServices()` (12 registrations) duplicates what `SetupModule.ConfigureServices()` already does via `TryAdd*`. Any new bootstrap service must be added in both places.

## Goal

Consolidate ALL setup web app creation into `Aero.Cms.Modules.Setup` so that `Program.cs` calls a **single entry point**, and the duplicate service registrations are eliminated.

---

## Dependency Check

### What `SetupWebApplication.cs` currently references

| Dependency | Source | Available in Setup csproj? |
|---|---|---|
| `Microsoft.AspNetCore.Builder` | `Microsoft.AspNetCore.App` | ✅ (FrameworkReference) |
| `Microsoft.AspNetCore.DataProtection` | `Microsoft.AspNetCore.App` | ✅ |
| `Microsoft.Extensions.Hosting` | `Microsoft.AspNetCore.App` | ✅ |
| `Aero.AppServer.Startup` | Project ref | ✅ (already in csproj) |
| `Aero.Secrets` | Project ref | ✅ (already in csproj) |
| `Aero.Cms.Modules.Setup.Bootstrap` | Own project | ✅ |
| `Aero.Cms.Modules.Setup.Configuration` | Own project | ✅ |
| `Radzen` | NuGet | ❌ Not in Setup csproj |
| `Serilog` | NuGet | ❌ Not in Setup csproj |

The `Radzen` and `Serilog` references are the only gaps. Two options:
- **A**: Add them to `Aero.Cms.Modules.Setup.csproj`
- **B**: Keep Radzen/Blazor UI configuration in `Program.cs` and only move the service registration + pipeline logic

**Recommendation: Option A** — Add the missing package references. They're lightweight and the setup app factory is a complete unit.

---

## Implementation Steps

### Step 1: Create `SetupAppFactory.cs` in Aero.Cms.Modules.Setup

**File**: `src/Aero.Cms.Modules.Setup/SetupAppFactory.cs`

This replaces `Aero.Cms.Web/Setup/SetupWebApplication.cs`.

```csharp
namespace Aero.Cms.Modules.Setup;

public static class SetupAppFactory
{
    public static async Task<WebApplication> CreateSetupAppAsync(string[] args, IConfiguration earlyConfig)
    {
        var webProjectPath = AppSettingsPathResolver.GetWebProjectPath();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = webProjectPath,
            EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development
        });
        var services = builder.Services;
        var config = builder.Configuration;
        var env = builder.Environment;

        // Configure Data Protection with shared settings
        ConfigureDataProtection(services, config);

        // Add logging
        services.AddLogging(logging =>
        {
            logging.AddSerilog();
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Add Radzen + Blazor for setup UI
        services.AddRadzenComponents();
        services.AddRazorComponents().AddInteractiveServerComponents();

        // Memory cache + antiforgery + cookies
        services.AddMemoryCache();
        services.AddAntiforgery(options =>
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
        services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.Secure = CookieSecurePolicy.SameAsRequest;
        });
        services.AddHttpContextAccessor();

        // REGISTER ALL SETUP SERVICES via SetupModule — eliminates duplication
        var setupModule = new SetupModule();
        setupModule.ConfigureServices(services, config, env);

        // Build and configure pipeline
        var app = builder.Build();
        ConfigureSetupPipeline(app);

        return app;
    }

    private static void ConfigureDataProtection(IServiceCollection services, IConfiguration config)
    {
        var settings = DataProtectionCertificateBootstrapper.ResolveSettings(config);
        var certificate = DataProtectionCertificateBootstrapper.GetOrCreateCertificate(settings);

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(settings.KeyRingPath))
            .ProtectKeysWithCertificate(certificate)
            .SetApplicationName(settings.ApplicationName);
    }

    private static void ConfigureSetupPipeline(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.MapStaticAssets();
        app.UseAntiforgery();
        app.UseCmsSetupGate();

        app.MapRazorComponents<Areas.Setup.Pages.SetupRoot>()
            .AddInteractiveServerRenderMode();
    }
}
```

**Key differences from `SetupWebApplication.cs`:**
- ❌ **Removed**: `RegisterBootstrapServices()` method entirely
- ✅ **Replaced with**: `var setupModule = new SetupModule(); setupModule.ConfigureServices(services, config, env);`
- All 12 bootstrap services are now registered from a single source of truth
- Same `ConfigureDataProtection()` and `ConfigureSetupPipeline()` — moved verbatim

### Step 2: Update `Aero.Cms.Modules.Setup.csproj`

Add missing package references:

```xml
<PackageReference Include="Radzen" />
<PackageReference Include="Serilog" />
<PackageReference Include="Serilog.Extensions.Logging" />
```

### Step 3: Simplify `Program.cs`

**Before:**
```csharp
using Aero.Cms.Web.Setup;   // Need this for SetupWebApplication

static async Task RunSetupAppAsync(string[] args, IConfiguration earlyConfig)
{
    var setupApp = await SetupWebApplication.CreateAsync(args, earlyConfig);
    // ...start, wait, stop...
}
```

**After:**
```csharp
// Remove: using Aero.Cms.Web.Setup;   (no longer needed)

static async Task RunSetupAppAsync(string[] args, IConfiguration earlyConfig)
{
    var setupApp = await SetupModule.CreateSetupAppAsync(args, earlyConfig);
    // ...start, wait, stop...
}
```

Wait — the method above is `SetupAppFactory.CreateSetupAppAsync()`. Should it be on `SetupModule` or a separate static class?

**Recommendation**: Keep it as `SetupAppFactory.CreateSetupAppAsync()`. Modules are discovered and instantiated by the module system — they shouldn't have static factory methods that bypass module discovery. A separate `SetupAppFactory` is cleaner:

```csharp
// In Program.cs:
var setupApp = await SetupAppFactory.CreateSetupAppAsync(args, earlyConfig);
```

The `using` for this comes from `using Aero.Cms.Modules.Setup;` which is already present in Program.cs.

### Step 4: Delete `Aero.Cms.Web/Setup/SetupWebApplication.cs`

After verifying Step 1 works, delete the old file.

### Step 5: Delete `SetupWebApplication.cs` TODO comment

The `// todo - move this (SetupWebApplication.cs) to the aero.cms.modules.setup csproj` comment on line 16 is resolved by this refactoring.

---

## Verification

1. `dotnet build` — 0 errors
2. Run the app in Setup mode — setup wizard renders, completes
3. Verify `SetupModule.ConfigureServices()` is called during setup app creation
4. Verify no duplicate registrations exist (check for `TryAddSingleton<IBootstrapStateProvider>` etc. in the codebase outside of `SetupModule.ConfigureServices`)

## Files Changed

| File | Action |
|------|--------|
| `src/Aero.Cms.Modules.Setup/SetupAppFactory.cs` | **NEW** — setup app factory |
| `src/Aero.Cms.Modules.Setup/Aero.Cms.Modules.Setup.csproj` | **MODIFY** — add Radzen, Serilog package refs |
| `src/Aero.Cms.Web/Program.cs` | **MODIFY** — update `RunSetupAppAsync` to call `SetupAppFactory.CreateSetupAppAsync()` |
| `src/Aero.Cms.Web/Setup/SetupWebApplication.cs` | **DELETE** — replaced by `SetupAppFactory.cs` |

## Future Considerations

### Main App Inline Orchestration

`RunMainAppAsync()` in `Program.cs` (~170 lines) is a separate concern. It mixes:
- Web-specific config (auth, middleware, components) — belongs in Program.cs
- Module bootstrap/initialization logic — could migrate to `Aero.Cms.Modules.Setup`

A future refactoring could extract `RunMainAppAsync()`'s module lifecycle logic into `SetupModule`:

```csharp
// Future — in Program.cs:
var app = builder.Build();
await SetupModule.InitializeRuntimeAsync(app, bootstrapState);
await app.WaitForShutdownAsync();
```

This is out of scope for this refactor but follows the same SRP principle.
