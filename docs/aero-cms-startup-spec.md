# Aero CMS Startup & Configuration Specification

## Overview

Aero CMS uses a two-stage startup architecture to handle initial configuration without requiring application restarts. The system supports multiple deployment modes (embedded, local secrets, remote secrets) and uses X.509 certificates with ASP.NET Core Data Protection for secrets encryption.

## Core Principles

1. **No restart required** - User completes setup, main application starts immediately with saved configuration
2. **Container sealing** - Once `IServiceCollection` is built, it cannot be modified; solution is two sequential `WebApplication` instances
3. **Embedded service coordination** - Background services signal readiness via `TaskCompletionSource` before main app accepts requests
4. **Secrets encryption** - X.509 certificate + Data Protection for all sensitive configuration data
5. **Environment-aware** - Configuration uses standard ASP.NET Core environment-based settings

---

## Deployment Modes

### Mode 1: Embedded
- **Description**: Self-contained deployment with embedded PostgreSQL and Garnet (Redis-compatible)
- **Target**: Single-server deployments, development, demo environments
- **Components**: 
  - Embedded PostgreSQL (port 5433)
  - Embedded Garnet (port 6380)
- **Connection String**: Auto-configured to `localhost:5433`
- **Encryption**: Connection string encrypted with X.509 + Data Protection, stored in `appsettings.{Environment}.json`
- **Background Services**: `EmbeddedPostgresService`, `EmbeddedGarnetService` registered as `IHostedService`

### Mode 2: Server - Local Secrets
- **Description**: External database with secrets stored locally in appsettings
- **Target**: Small production deployments, environments without secrets management infrastructure
- **Components**: External PostgreSQL/SQL Server, external Redis/Garnet
- **Connection String**: User-provided
- **Encryption**: Connection string encrypted with X.509 + Data Protection, stored in `appsettings.{Environment}.json`
- **Background Services**: None

### Mode 3: Server - Infisical Secrets
- **Description**: External database with secrets managed by Infisical
- **Target**: Production deployments with centralized secrets management
- **Components**: External PostgreSQL/SQL Server, external Redis/Garnet, Infisical secrets manager
- **Connection String**: Retrieved from Infisical at startup
- **Encryption**: Infisical API key encrypted with X.509 + Data Protection, stored in `appsettings.{Environment}.json`
- **Background Services**: None

---

## Startup Flow

### Phase 1: Early Verification

**Goal**: Determine if setup is needed before building any DI containers.

```
Program.cs Entry
    ↓
Load X.509 Certificate (GetOrCreateCertificate)
    ↓
Build Early Configuration
    - appsettings.json
    - appsettings.{Environment}.json (Production, Development, etc.)
    ↓
Create Minimal Encryption Service (temp DI container)
    - ISecretsEncryption with X.509 + Data Protection
    ↓
Call SetupVerifier.VerifySetup(config, encryption)
    ↓
Determine: Setup needed? Which mode configured?
```

**SetupVerifier Logic**:

1. **Check for Infisical** (`SecretsKey` in appsettings)
   - If present: Decrypt SecretsKey → Fetch connection string from Infisical
   - If connection succeeds: Check database for `SetupStatus` document
   - Return `SetupResult` (complete/incomplete, connection string, mode)
   - If any failure: Throw `SetupException` (manual intervention required)

2. **Check for Local Secrets** (`ConnectionStrings:Aero` in appsettings)
   - If `EmbeddedMode=true`: Decrypt connection string with Data Protection
   - If `EmbeddedMode=false`: Use connection string as-is (external database)
   - Test connection with `NpgsqlConnection`
   - If connection succeeds: Check database for `SetupStatus` document
   - Return `SetupResult`

3. **No Configuration Found**
   - Return `SetupResult.NeedsSetup(null, SecretsMode.NotConfigured)`

### Phase 2: Setup Application (Conditional)

**Condition**: `!setupResult.IsSetupComplete`

**Services Registered**:
- `AddRazorComponents().AddInteractiveServerComponents()`
- `AddSecretsEncryption(certificate)` - X.509 + Data Protection
- `AddMemoryCache()` - For `ISecretsCache`
- `AddSingleton<ISecretsCache, SecretsCache>`
- `AddSingleton<ISetupService, SetupService>`
- `AddSingleton(setupResult)` - Pass verification result to UI

**Note**: Background services (PostgreSQL, Garnet) are **not** registered in setup app. They only run after setup completes.

**Flow**:
```
Create WebApplicationBuilder (setup mode)
    ↓
Register minimal services
    ↓
Build setup WebApplication
    ↓
Map Setup.razor component
    ↓
await setupApp.RunAsync()  [BLOCKS until user completes setup]
    ↓
User fills form → ISetupService.CompleteSetup()
    ↓
Save configuration to appsettings.{Environment}.json
    ↓
Create SetupStatus document in database
    ↓
Call IHostApplicationLifetime.StopApplication()
    ↓
setupApp.RunAsync() completes, execution continues
```

**Setup UI Options**:

The `Setup.razor` page presents three options:

1. **Embedded** - Auto-configured localhost services
2. **Server - Local Secrets** - User provides connection string, encrypted locally
3. **Server - Infisical** - User provides Infisical API key, connection string fetched remotely

### Phase 3: Main Application

**Always executes** after Phase 2 (or immediately if setup not needed).

```
Create WebApplicationBuilder (main mode)
    ↓
Re-run SetupVerifier.VerifySetup() with fresh config
    ↓
Validate setup is complete (throw if not)
    ↓
Conditional: If SecretsMode.Embedded
    ↓   Register EmbeddedPostgresService
    ↓   Register EmbeddedGarnetService
    ↓
Cache connection string in ISecretsCache
    ↓
Register Aero services:
    - AddSecretsEncryption(certificate)
    - AddMarten(connString) [registration only, no connection yet]
    - AddOrleansServer()
    - AddWolverine()
    - AddAeroApplicationServer()
    ↓
Build main WebApplication
    ↓
Conditional: If SecretsMode.Embedded
    ↓   await app.StartAsync() [starts background services]
    ↓   Get EmbeddedPostgresService from DI
    ↓   Get EmbeddedGarnetService from DI
    ↓   await Task.WhenAll(
    ↓       postgres.WaitForReadyAsync(timeout),
    ↓       garnet.WaitForReadyAsync(timeout)
    ↓   )
    ↓   Log: "Embedded services ready"
    ↓
Map routes (Blazor, MVC, APIs)
    ↓
await app.RunAsync() [starts if not already started]
```

---

## Background Service Coordination

### Problem

Marten and Orleans are registered in the DI container **before** embedded PostgreSQL/Garnet are ready. If Blazor pages immediately query Marten, connections will fail.

### Solution: TaskCompletionSource Pattern

Each background service implements `IEmbeddedServiceHealth`:

```csharp
public interface IEmbeddedServiceHealth
{
    bool IsReady { get; }
    Task WaitForReadyAsync(CancellationToken cancellationToken = default);
}
```

**Implementation Pattern**:

```csharp
public class EmbeddedPostgresService : BackgroundService, IEmbeddedServiceHealth
{
    private readonly TaskCompletionSource _readyTcs = new();
    
    public bool IsReady { get; private set; }
    
    public Task WaitForReadyAsync(CancellationToken ct) 
        => _readyTcs.Task.WaitAsync(ct);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start postgres process
        _postgresProcess = Process.Start(/* ... */);
        
        // Poll until connection succeeds
        var timeout = TimeSpan.FromSeconds(30);
        var started = DateTime.UtcNow;
        
        while (DateTime.UtcNow - started < timeout)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync(stoppingToken);
                
                IsReady = true;
                _readyTcs.SetResult(); // Signal ready!
                break;
            }
            catch
            {
                await Task.Delay(500, stoppingToken);
            }
        }
        
        if (!IsReady)
            _readyTcs.SetException(new TimeoutException("PostgreSQL failed to start"));
        
        // Keep running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

**Coordination in Program.cs**:

```csharp
if (finalSetupResult.Mode == SecretsMode.Embedded)
{
    await app.StartAsync(); // Triggers ExecuteAsync() for both services
    
    var postgres = app.Services.GetServices<IHostedService>()
        .OfType<EmbeddedPostgresService>().First();
    var garnet = app.Services.GetServices<IHostedService>()
        .OfType<EmbeddedGarnetService>().First();
    
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    
    // This blocks until BOTH call SetResult()
    await Task.WhenAll(
        postgres.WaitForReadyAsync(cts.Token),
        garnet.WaitForReadyAsync(cts.Token)
    );
    
    // Now safe - PostgreSQL accepting connections on 5433
}
```

**Key Properties**:
- No shared state (each service owns its promise)
- No race conditions
- Built-in timeout via `CancellationToken`
- Clear failure signaling via `SetException()`

---

## Secrets Encryption

### X.509 Certificate Management

**Location**: `/app/certs/aero-cms.pfx` (Docker volume mount)

**Auto-Generation**: If certificate doesn't exist, generate self-signed:
- Algorithm: ECDSA with `nistP256` curve
- Validity: 10 years
- Subject: `CN=Aero CMS Data Protection`
- Password: Random 32-byte Base64 string, saved to `/app/certs/cert.key`

**BYOK (Bring Your Own Key)**: User can provide their own certificate by:
1. Placing `.pfx` file at `/app/certs/aero-cms.pfx`
2. Setting `AERO_CERT_PASSWORD` environment variable

### Data Protection Configuration

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/keys")) // Docker volume
    .ProtectKeysWithCertificate(certificate)
    .SetApplicationName("AeroCMS"); // Critical: same across setup + main app
```

**Key Points**:
- Keys persist to `/app/keys` (Docker volume) - shared between setup and main app
- `SetApplicationName("AeroCMS")` ensures both apps use same key ring
- Without matching app name, main app cannot decrypt setup app's encrypted values

### Encryption Service

```csharp
public interface ISecretsEncryption
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}

public class X509DataProtectionEncryption : ISecretsEncryption
{
    private readonly IDataProtector _protector;
    
    public X509DataProtectionEncryption(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("AeroCMS.Secrets.v1");
    }
    
    public string Encrypt(string plaintext) 
        => _protector.Protect(plaintext);
    
    public string Decrypt(string ciphertext) 
        => _protector.Unprotect(ciphertext);
}
```

### Configuration Storage

**Embedded Mode** - `appsettings.Production.json`:
```json
{
  "EmbeddedMode": true,
  "ConnectionStrings": {
    "Aero": "CfDJ8A... [encrypted connection string]"
  }
}
```

**Server - Local Secrets** - `appsettings.Production.json`:
```json
{
  "EmbeddedMode": false,
  "ConnectionStrings": {
    "Aero": "CfDJ8A... [encrypted connection string]"
  }
}
```

**Server - Infisical** - `appsettings.Production.json`:
```json
{
  "EmbeddedMode": false,
  "SecretsKey": "CfDJ8A... [encrypted Infisical API key]"
}
```

---

## Database Schema

### SetupStatus Document (Marten)

```csharp
public class SetupStatus
{
    public string Id { get; set; } = "setup";
    public bool IsComplete { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CompletedByUser { get; set; } = string.Empty;
    public SecretsMode SecretsMode { get; set; }
}
```

**Usage**:
- Single document with `Id = "setup"`
- Checked during `SetupVerifier.VerifySetup()`
- Created by `SetupService.CompleteSetup()`

**Schema Creation**:
```csharp
var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);
    opts.DatabaseSchemaName = "aero";
    opts.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
});
```

Marten auto-creates tables on first use - no migrations required.

---

## Key Services

### ISecretsCache

In-memory cache for connection strings (avoids repeated decryption or Infisical calls).

```csharp
public interface ISecretsCache
{
    void SetConnectionString(string connectionString);
    string? GetConnectionString();
    void SetInfisicalKey(string key);
    string? GetInfisicalKey();
    void Clear();
}
```

**Registration**: `AddSingleton<ISecretsCache, SecretsCache>`

**Implementation**: Wraps `IMemoryCache` with `CacheItemPriority.NeverRemove`

### ISetupService

Handles saving user configuration from setup UI.

```csharp
public interface ISetupService
{
    Task CompleteSetup(SetupConfiguration config);
}

public record SetupConfiguration(
    string ConnectionString,
    SecretsMode Mode,
    string? InfisicalApiKey,
    string AdminEmail
);
```

**Responsibilities**:
1. Encrypt connection string or Infisical key
2. Save to `appsettings.{Environment}.json`
3. Create `SetupStatus` document in database
4. Cache connection string in `ISecretsCache`
5. Call `IHostApplicationLifetime.StopApplication()`

### SetupVerifier (Static Utility)

```csharp
public static class SetupVerifier
{
    public static async Task<SetupResult> VerifySetup(
        IConfiguration config, 
        ISecretsEncryption? encryption = null);
}

public record SetupResult(
    bool IsSetupComplete,
    string? ConnectionString,
    SecretsMode Mode
);

public enum SecretsMode
{
    NotConfigured,
    PlainText,      // Dev only - no encryption
    Embedded,
    LocalSecrets,   // Server mode with local encrypted secrets
    Infisical       // Server mode with Infisical
}
```

---

## Docker Configuration

### Volume Mounts

```yaml
version: '3.8'

services:
  aero-cms:
    image: aero-cms:latest
    volumes:
      - aero-certs:/app/certs      # X509 certificate + password
      - aero-keys:/app/keys        # Data Protection key ring
      - aero-postgres:/app/embedded/postgres/data  # Embedded PostgreSQL data
      - aero-garnet:/app/embedded/garnet/data      # Embedded Garnet data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - AERO_CERT_PASSWORD=${CERT_PASSWORD}  # Optional BYOK password
    ports:
      - "5000:8080"

volumes:
  aero-certs:
  aero-keys:
  aero-postgres:
  aero-garnet:
```

### Environment Variables

| Variable | Required | Purpose |
|----------|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | Yes | Determines which `appsettings.{Environment}.json` to load |
| `AERO_CERT_PASSWORD` | No | Password for BYOK certificate (omit for auto-generated cert) |

---

## Security Considerations

### Certificate Storage
- Auto-generated certificates use strong random passwords (32-byte Base64)
- Password stored at `/app/certs/cert.key` with restricted permissions (600)
- BYOK certificates allow organizations to use HSM-backed keys

### Data Protection
- Keys are encrypted with X.509 certificate (asymmetric encryption)
- Key ring persists to volume (survives container restarts)
- Application name isolation prevents cross-application key leakage

### Secrets in Memory
- Connection strings cached in `IMemoryCache` with `NeverRemove` priority
- Never logged or exposed in error messages
- Cleared on application shutdown via `ISecretsCache.Clear()`

### Infisical Integration
- API key encrypted at rest
- Retrieved connection strings cached (reduces API calls)
- Failed Infisical calls throw exceptions (fail-closed, not fail-open)

---

## Error Handling

### Setup Phase Errors

| Scenario | Behavior |
|----------|----------|
| Infisical configured but unreachable | Throw `SetupException` - manual intervention required |
| Encrypted connection string cannot decrypt | Return `SetupResult.NeedsSetup` - user reconfigures |
| Database connection fails | Return `SetupResult.NeedsSetup` - user fixes connection string |
| Setup UI validation fails | Display inline errors - do not save configuration |

### Main Application Errors

| Scenario | Behavior |
|----------|----------|
| Setup claims complete but config missing | Throw `InvalidOperationException` - critical failure |
| Embedded PostgreSQL fails to start | Throw `TimeoutException` after 2 minutes |
| Embedded Garnet fails to start | Throw `TimeoutException` after 2 minutes |
| Marten query before services ready | Should not occur (coordinated by `WaitForReadyAsync`) |

### Recovery Procedures

**Reset to Fresh Install**:
1. Delete `appsettings.{Environment}.json`
2. Restart application
3. Setup UI appears

**Switch from Embedded to Server**:
1. Manually edit `appsettings.{Environment}.json`
2. Change `EmbeddedMode: false`
3. Add external `ConnectionStrings:Aero`
4. Restart application

**Rotate Encryption Certificate**:
1. Stop application
2. Replace `/app/certs/aero-cms.pfx`
3. Delete `/app/keys/*` (force key re-generation)
4. Re-run setup (existing encrypted values unusable)

---

## Testing Strategy

### Unit Tests

**SetupVerifier**:
- [x] Returns `NeedsSetup` when no configuration exists
- [x] Returns `Complete` when valid embedded configuration exists
- [x] Returns `Complete` when valid Infisical configuration exists
- [x] Throws `SetupException` when Infisical key exists but API fails
- [x] Returns `NeedsSetup` when encrypted connection string cannot decrypt

**Background Services**:
- [x] `WaitForReadyAsync()` completes when service starts successfully
- [x] `WaitForReadyAsync()` throws on timeout
- [x] `SetException()` propagates to all awaiting tasks

### Integration Tests

**Two-App Flow**:
- [x] Setup app starts, user completes form, main app starts without restart
- [x] Configuration saved during setup is readable by main app
- [x] Data Protection keys are shared between setup and main app

**Embedded Services**:
- [x] PostgreSQL accepts connections after `WaitForReadyAsync()` completes
- [x] Garnet accepts PING after `WaitForReadyAsync()` completes
- [x] Marten queries succeed after embedded services signal ready

### Manual Testing Checklist

- [ ] Fresh install → Setup UI appears
- [ ] Embedded mode → PostgreSQL/Garnet start, main app serves requests
- [ ] Server mode (local secrets) → External database connects
- [ ] Server mode (Infisical) → Secrets fetched, database connects
- [ ] Invalid connection string → Setup UI shows error
- [ ] Certificate rotation → Setup runs again (old encrypted values fail)
- [ ] Docker container restart → Application starts without setup (config persisted)

---

## Future Enhancements

### Phase 2 Features
- [ ] Setup wizard for database schema migration (detect existing data)
- [ ] Health check endpoints for embedded services
- [ ] Graceful shutdown for embedded PostgreSQL (pg_ctl stop vs kill)
- [ ] Support for PostgreSQL authentication (not just trust localhost)
- [ ] Embedded service logs exposed via structured logging

### Phase 3 Features
- [ ] Multi-node embedded clustering (embedded PostgreSQL replication)
- [ ] Hot-reload configuration changes without restart
- [ ] Admin UI for rotating encryption certificates
- [ ] Backup/restore for embedded database data
- [ ] Prometheus metrics for embedded service health

---

## Appendix: Decision Log

### Why Two WebApplications Instead of One?

**Rejected**: Single app with conditional service registration based on runtime flags.

**Problem**: Once `IServiceCollection.Build()` is called, the container is sealed. You cannot add Marten with a connection string that doesn't exist yet.

**Solution**: Setup app collects configuration, saves to disk, stops. Main app reads configuration from disk, builds container with full knowledge.

### Why TaskCompletionSource Instead of Polling?

**Rejected**: Main app polls `IsReady` flags in a loop until both true.

**Problem**: Wastes CPU cycles, introduces arbitrary sleep durations, unclear timeout semantics.

**Solution**: TaskCompletionSource provides a first-class async coordination primitive with built-in cancellation and exception propagation.

### Why Not Run Embedded Services in Setup App?

**Rejected**: Start embedded PostgreSQL/Garnet during setup so user can test connection.

**Problem**: Adds complexity to setup app (needs background service coordination), and user might change their mind and select server mode.

**Solution**: Embedded services only run after setup completes and user commits to embedded mode.

### Why Environment-Based appsettings Instead of appsettings.Configured.json?

**Original Design**: Separate `appsettings.Configured.json` file.

**Problem**: Non-standard ASP.NET Core pattern, requires custom configuration loading logic.

**Solution**: Use standard `appsettings.{Environment}.json` (Production, Development, Staging) - familiar to all .NET developers, works with existing tooling.

---

## Glossary

| Term | Definition |
|------|------------|
| **DI Container** | Dependency Injection container (`IServiceCollection`) that holds service registrations |
| **Sealed Container** | After `Build()`, no more services can be added or modified |
| **Background Service** | `IHostedService` implementation that runs long-lived tasks |
| **TaskCompletionSource** | Promise-like primitive for coordinating async operations without polling |
| **Data Protection** | ASP.NET Core's encryption API for protecting sensitive data at rest |
| **X.509 Certificate** | Asymmetric key pair used to encrypt Data Protection keys |
| **Marten** | Document database library for PostgreSQL with event sourcing support |
| **Orleans** | Actor model framework for distributed systems |
| **Wolverine** | Messaging and background job framework |
| **Infisical** | Open-source secrets management platform |
| **Garnet** | Microsoft's Redis-compatible in-memory cache |

---

**Document Version**: 1.0  
**Last Updated**: 2026-04-12  
**Author**: T-roy & Claude  
**Review Status**: Draft
