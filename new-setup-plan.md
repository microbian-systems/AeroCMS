# Aero CMS Two-App Startup Implementation Plan

## Executive Summary

This document outlines the implementation of a two-stage WebApplication startup pattern for Aero CMS where:

1. **Setup App** runs first (if needed) with a minimal DI container - only Razor Components, Data Protection, and setup services
2. **Main App** runs after setup completes with full DI container - Marten, Orleans, Identity, all modules
3. **No manual restart required** - uses `IHostApplicationLifetime.StopApplication()` to transition between apps

This approach follows the specification in `aero-cms-startup-spec.md` and uses Microsoft's recommended patterns for ASP.NET Core host lifecycle management.

---

## Architecture Overview

```
Program.cs Entry
    ↓
Check Bootstrap State (appsettings.json)
    ↓
IF State = "Setup":
    Create Setup WebApplication
        - Minimal services (Razor, Data Protection, Secrets)
        - Map Setup.razor page
        - await app.StartAsync()
        - await app.WaitForShutdownAsync()  ← Blocks here until setup complete
        - await app.StopAsync()
    ↓
    Re-verify configuration
    ↓
Create Main WebApplication
    - Full services (Marten, Orleans, Identity, etc.)
    - If Embedded mode: Start services, wait for ready via TaskCompletionSource
    - await app.RunAsync()
```

---

## Key Technical Requirements

### 1. Data Protection Sharing (Critical)

Both apps MUST use identical Data Protection configuration:

```csharp
services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .ProtectKeysWithCertificate(certificate)
    .SetApplicationName("AeroCMS");  // Same name in both apps
```

**Rationale**: According to Microsoft Learn, the application discriminator (SetApplicationName) ensures both apps use the same key ring, enabling the main app to decrypt values encrypted by the setup app.

### 2. Background Service Coordination (for Embedded Mode)

Embedded PostgreSQL and Garnet services must implement:

```csharp
public interface IEmbeddedServiceHealth
{
    bool IsReady { get; }
    Task WaitForReadyAsync(CancellationToken cancellationToken = default);
}
```

The main app will wait for all embedded services:

```csharp
await app.StartAsync();
await Task.WhenAll(
    postgres.WaitForReadyAsync(cts.Token),
    garnet.WaitForReadyAsync(cts.Token)
);
```

### 3. Configuration Persistence

- Setup saves to `appsettings.{Environment}.json`
- Both apps read from the same file
- State transitions: Setup → Configured → Running

---

## Configuration Schema

Add to all environment-specific appsettings files:

```json
{
  "AeroCms": {
    "DataProtection": {
      "KeyStoragePath": "keys",
      "Certificate": {
        "Path": "certs/aero-cms.pfx",
        "Password": null
      }
    },
    "Bootstrap": {
      "State": "Setup",
      "SetupComplete": false,
      "SeedComplete": false,
      "DatabaseMode": "Embedded",
      "CacheMode": "Memory",
      "SecretProvider": "Local Certificate"
    }
  }
}
```

**Environment-Specific Values:**

- **Development**: `KeyStoragePath: "keys"` (relative to project)
- **Production/Docker**: `KeyStoragePath: "/app/keys"` (volume mount)

---

## New Files to Create

### 1. `src/Aero.Cms.Web/Setup/SetupWebApplication.cs`

**Purpose**: Factory class for creating the setup-specific WebApplication

**Key Responsibilities**:
- Configure Data Protection with shared key storage
- Register only setup-required services (no Marten, Orleans, Identity)
- Build and return WebApplication ready for `StartAsync()`

**Service Registration** (Setup App Only):
- `AddRazorComponents().AddInteractiveServerComponents()`
- `AddRadzenComponents()`
- Data Protection (shared configuration)
- `IEnvironmentAppSettingsWriter`
- `ISecretManager`
- `ISetupCompletionService`
- `SetupPathAllowlist`
- `ISetupInitializationService`
- Memory caching

**Pipeline Configuration**:
- HTTPS redirection
- Static files
- Routing
- Antiforgery
- Razor Components (Setup page only)

### 2. `src/Aero.Cms.Web/Setup/SetupCompletionService.cs`

**Purpose**: Service that handles setup form submission and triggers application shutdown

**Interface**:
```csharp
public interface ISetupCompletionService
{
    Task CompleteSetupAsync(SetupConfiguration config);
}
```

**Implementation Flow**:
1. Validate configuration
2. Encrypt connection strings/secrets
3. Save to `appsettings.{Environment}.json`
4. Update Bootstrap state to "Configured"
5. Call `_lifetime.StopApplication()`

**Important**: No database operations in setup app - embedded DB not running yet

### 3. `src/Aero.AppServer/Startup/IEmbeddedServiceHealth.cs`

**Purpose**: Interface for embedded services to signal readiness

```csharp
public interface IEmbeddedServiceHealth
{
    bool IsReady { get; }
    Task WaitForReadyAsync(CancellationToken cancellationToken = default);
}
```

---

## Modified Files

### 4. `src/Aero.Cms.Web/Program.cs` (Complete Rewrite)

**New Structure**:

```csharp
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Phase 1: Check if setup needed
            var setupResult = await CheckSetupAsync(args);
            
            // Phase 2: Run Setup App if needed
            if (setupResult.NeedsSetup)
            {
                await RunSetupAppAsync(args);
                setupResult = await CheckSetupAsync(args); // Re-verify
            }
            
            // Phase 3: Create Main Application
            await RunMainAppAsync(args, setupResult);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            Environment.Exit(1);
        }
    }
    
    private static async Task RunSetupAppAsync(string[] args)
    {
        var earlyConfig = BuildEarlyConfiguration(args);
        var setupApp = await SetupWebApplication.CreateAsync(args, earlyConfig);
        
        await setupApp.StartAsync();
        
        try
        {
            // Block until IHostApplicationLifetime.StopApplication() called
            await setupApp.WaitForShutdownAsync();
        }
        finally
        {
            await setupApp.StopAsync();
        }
    }
    
    private static async Task RunMainAppAsync(string[] args, SetupResult setupResult)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure Data Protection with SAME settings as setup app
        ConfigureDataProtection(builder.Services, builder.Configuration);
        
        // Full service registration
        builder.AddServiceDefaults();
        await builder.AddAeroApplicationServer();
        await builder.AddAeroCmsRuntimeAsync<Program>();
        
        var app = builder.Build();
        
        // If embedded mode, start and wait for services
        if (setupResult.Mode == SecretsMode.Embedded)
        {
            await app.StartAsync();
            await WaitForEmbeddedServicesAsync(app);
        }
        
        await app.MapAeroAppAsync();
        await app.RunAsync();
    }
}
```

### 5. `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.razor.cs`

**Changes**:
- Inject `ISetupCompletionService`
- Modify `HandleSubmit()` to call completion service
- Remove "restart required" message
- Show "Setup complete, starting main application..."

```csharp
[Inject]
private ISetupCompletionService SetupCompletionService { get; set; } = default!;

protected async Task HandleSubmit()
{
    // ... validation logic ...
    
    var config = new SetupConfiguration(
        DatabaseMode: databaseMode,
        CacheMode: cacheMode,
        SecretProvider: secretProvider,
        // ... other fields
    );
    
    StatusMessage = "Setup complete! Starting main application...";
    StateHasChanged();
    
    await SetupCompletionService.CompleteSetupAsync(config);
    
    // App will shut down and main app will start automatically
}
```

### 6. `src/Aero.AppServer/Startup/AeroEmbeddedDbService.cs`

**Changes**: Implement `IEmbeddedServiceHealth`

```csharp
public class AeroEmbeddedDbService : BackgroundService, IEmbeddedServiceHealth
{
    private readonly TaskCompletionSource _readyTcs = 
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    public bool IsReady { get; private set; }
    
    public Task WaitForReadyAsync(CancellationToken cancellationToken = default)
        => _readyTcs.Task.WaitAsync(cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start PostgreSQL process
        _postgresProcess = StartPostgresProcess();
        
        // Poll until ready
        var timeout = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < timeout && !stoppingToken.IsCancellationRequested)
        {
            if (await TryConnectAsync())
            {
                IsReady = true;
                _readyTcs.TrySetResult();
                break;
            }
            await Task.Delay(500, stoppingToken);
        }
        
        if (!IsReady)
            _readyTcs.TrySetException(new TimeoutException("PostgreSQL failed to start"));
        
        // Keep running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

### 7. `src/Aero.AppServer/Startup/AeroCacheService.cs`

**Changes**: Implement `IEmbeddedServiceHealth` (same pattern as above)

### 8. `appsettings.Development.json` and `appsettings.Production.json`

**Add Section**:
```json
{
  "AeroCms": {
    "DataProtection": {
      "KeyStoragePath": "keys",
      "Certificate": {
        "Path": "certs/aero-cms.pfx",
        "Password": null
      }
    }
  }
}
```

---

## Docker Configuration

### `docker-compose.yml`

```yaml
services:
  aero-cms:
    image: aero-cms:latest
    volumes:
      - aero-keys:/app/keys        # Data Protection key ring
      - aero-certs:/app/certs      # X.509 certificate
      - aero-postgres:/app/embedded/postgres/data
      - aero-garnet:/app/embedded/garnet/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    ports:
      - "5000:8080"

volumes:
  aero-keys:
  aero-certs:
  aero-postgres:
  aero-garnet:
```

### Certificate Management

**Auto-Generation** (if certificate doesn't exist):
- Algorithm: ECDSA with nistP256 curve
- Validity: 10 years
- Subject: `CN=Aero CMS Data Protection`
- Password: Random 32-byte Base64, saved to `{certPath}.key`

**BYOK (Bring Your Own Key)**:
User can provide their own `.pfx` file at configured path

---

## Error Handling Strategy

### Setup App Errors

| Scenario | Behavior |
|----------|----------|
| Configuration validation fails | Show inline errors, do not call StopApplication() |
| File write fails (permissions) | Log error, show error message, stay in setup |
| Exception during RunAsync | Catch, log fatal, exit process with code 1 |

### Main App Errors

| Scenario | Behavior |
|----------|----------|
| Setup claims complete but config missing | Throw InvalidOperationException, exit |
| Embedded service timeout | Throw TimeoutException, exit |
| Certificate cannot be loaded | Throw exception, exit |

### Recovery Procedures

**Reset to Fresh Install**:
1. Delete `appsettings.{Environment}.json`
2. Delete Data Protection keys directory
3. Restart application

**Retry Setup**:
- If setup fails, user can retry without restart
- Setup app stays running until successful completion

---

## Testing Strategy

### Unit Tests

- **SetupVerifier**: Returns correct state based on configuration
- **SetupCompletionService**: Saves configuration correctly
- **Embedded Services**: WaitForReadyAsync completes when healthy

### Integration Tests

- **Two-App Flow**: Setup app runs, completes, main app starts
- **Data Protection**: Encrypted values from setup readable by main app
- **Embedded Services**: PostgreSQL accepts connections after WaitForReadyAsync

### Manual Testing Checklist

- [ ] Fresh install → Setup UI appears
- [ ] Embedded mode → Setup completes, main app starts, services ready
- [ ] Server mode → Setup completes, main app starts, external DB connects
- [ ] Invalid configuration → Setup shows validation errors
- [ ] Certificate rotation → New setup required (old encrypted values fail)
- [ ] Docker restart → Application starts without setup (config persisted)

---

## Implementation Order

1. **Configuration**: Add DataProtection section to appsettings files
2. **Interface**: Create `IEmbeddedServiceHealth`
3. **Setup Factory**: Create `SetupWebApplication`
4. **Completion Service**: Create `SetupCompletionService`
5. **Embedded Services**: Implement `IEmbeddedServiceHealth`
6. **Program.cs**: Rewrite for two-app orchestration
7. **Setup Page**: Modify to use completion service
8. **Docker**: Update compose file with volumes

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Data Protection misconfiguration | High - cannot decrypt values | Use identical config, validate on startup |
| File locking on appsettings.json | Medium - setup cannot save | Use file locking with retry, or temp file swap |
| Embedded service startup timeout | Medium - app fails to start | Configurable timeout, clear error messages |
| Certificate expiration | Low - values become unreadable | 10-year validity, monitoring alerts |

---

## References

- Microsoft Learn: [.NET Generic Host](https://learn.microsoft.com/aspnet/core/fundamentals/host/generic-host)
- Microsoft Learn: [Configure ASP.NET Core Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
- Microsoft Learn: [Key Storage Providers](https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers)
- Spec: `aero-cms-startup-spec.md`
- Spec: `startup-signal-pattern.md`
- Spec: `startup-logic.md`

---

## Approval

This plan has been reviewed and approved for implementation.

Date: 2026-04-15
