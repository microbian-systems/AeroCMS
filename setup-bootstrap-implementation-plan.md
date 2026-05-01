# Setup Bootstrap Implementation Plan

## Goal

Extend first-run setup to support:

- Database mode selection: `Embedded` or `Server`
- Cache mode selection: `Memory`, `Embedded`, or `Server`
- Secret storage provider selection: `Local Certificate (Recommended)` or `Infisical`
- Secure storage of server connection strings using either:
  - ASP.NET Core Data Protection + X509 certificate
  - Infisical
- Embedded readiness checks for:
  - embedded PostgreSQL
  - embedded Garnet cache

## Confirmed Decisions

- `Server` connection settings are stored securely in `appsettings.{Environment}.json`
- Local encryption means **ASP.NET Core Data Protection + X509 certificate**, not DPAPI
- `secrets.cs` is aligned with the Local Certificate approach
- If `Infisical` is selected:
  - the secret value itself is stored in Infisical
  - appsettings stores encrypted Infisical auth material plus secret reference metadata
- Embedded readiness should be based on **real connectivity**, not just process startup
- Garnet should be optional for smaller embedded installs unless required by the selected cache mode
- Cache UI should mirror database mode selection

---

## Phase 1 - Bootstrap / Runtime Split

### Modify

1. `src/Aero.Cms.Web/Program.cs`
   - Split minimal bootstrap startup from full runtime startup
   - Avoid unconditional full DB/module init before bootstrap state is known

2. `src/Aero.Cms.Web.Core/.../AeroWebAppExtensions.cs`
   - Separate minimal app mapping from full runtime init
   - Move migrations/full module init behind resolved bootstrap state

3. `src/Aero.Cms.Modules.Setup/SetupGateMiddleware.cs`
   - Gate on bootstrap state first
   - Allow `/setup` before DB/cache infra is fully initialized

4. `src/Aero.Cms.Modules.Setup/SetupModule.cs`
   - Register bootstrap services and setup status endpoint
   - Guard DB-dependent setup logic

5. `src/Aero.Cms.Modules.Setup/SetupStateStore.cs`
   - Stop making DB-backed setup completion the only first-run truth

### Add

6. `src/Aero.Cms.Modules.Setup/Bootstrap/BootstrapState.cs`
   - Fields for:
     - database mode
     - cache mode
     - secret provider
     - bootstrap config existence
     - seed completion

7. `src/Aero.Cms.Modules.Setup/Bootstrap/IBootstrapStateProvider.cs`

8. `src/Aero.Cms.Modules.Setup/Bootstrap/AppSettingsBootstrapStateProvider.cs`

---

## Phase 2 - Secret Storage Abstractions

### Add

9. `src/Aero.Secrets/ISecretManager.cs`

10. `src/Aero.Secrets/SecretManagerBase.cs`

11. `src/Aero.Secrets/DataProtectionCertificateSecretManager.cs`
   - Local certificate-based secret protection using ASP.NET Core Data Protection + X509

12. `src/Aero.Secrets/InfisicalSecretManager.cs`
   - Store DB/cache connection strings in Infisical
   - Use machine id + client secret
   - Development target: `http://localhost:3000`

13. `src/Aero.Secrets/Models/SecretProviderType.cs`

14. `src/Aero.Secrets/Models/StoredSecretReference.cs`

### Verify

15. `src/Aero.Secrets/Aero.Secrets.csproj`

16. `secrets.cs`
   - Keep as companion utility for creating protected payloads

---

## Phase 3 - Environment AppSettings Persistence

### Add

17. `src/Aero.Cms.Modules.Setup/Configuration/IEnvironmentAppSettingsWriter.cs`

18. `src/Aero.Cms.Modules.Setup/Configuration/EnvironmentAppSettingsWriter.cs`
   - Atomic writes to `appsettings.{Environment}.json`

### Modify

19. `src/Aero.Cms/appsettings.json`
   - Add base schema if needed for bootstrap/settings structure

20. `src/Aero.Cms/appsettings.Development.json`
21. `src/Aero.Cms/appsettings.Staging.json`
22. `src/Aero.Cms/appsettings.Production.json`
   - Create/update as needed

### Storage Rules

- `DB = Server` + `Local Certificate`
  - store encrypted DB connection string in `ConnectionStrings`
- `DB = Server` + `Infisical`
  - store encrypted Infisical auth material + secret reference in appsettings
  - actual DB connection string lives in Infisical
- `Cache = Server` follows the same rule as DB server mode
- Embedded or memory modes do not require storing external connection strings

---

## Phase 4 - AppServer Multi-Service Readiness

### Add

23. `src/Aero.AppServer/Startup/IMultiStartupSignal.cs`

24. `src/Aero.AppServer/Startup/MultiStartupSignal.cs`

25. `src/Aero.AppServer/Startup/StartupServiceNames.cs`
   - `EmbeddedPostgres`
   - `EmbeddedGarnet`

26. `src/Aero.AppServer/Startup/IInfrastructureReadinessSnapshot.cs`

27. `src/Aero.AppServer/Startup/InfrastructureReadinessSnapshot.cs`

### Modify

28. `src/Aero.AppServer/AeroEmbeddedDbService.cs`
   - Mark ready only after:
     - process starts
     - real DB connection succeeds

29. `src/Aero.AppServer/AeroCacheService.cs`
   - Mark ready only after:
     - Garnet starts
     - real connection/ping succeeds

30. `src/Aero.AppServer/AeroAppServerExtensions.cs`
   - Register startup barrier and readiness snapshot
   - Make expected services mode-aware

### Rule

- Multi-service startup barrier is for **runtime readiness**
- Setup UI still uses a status endpoint for browser polling
- Embedded mode readiness can require Postgres only, or Postgres + Garnet based on chosen cache mode

---

## Phase 5 - Setup Status Endpoint

### Add

31. `src/Aero.Cms.Modules.Setup/Endpoints/SetupStatusEndpoints.cs`
   - Returns:
     - database mode
     - cache mode
     - embedded postgres ready
     - embedded garnet ready
     - combined readiness flags

### Modify

32. `src/Aero.Cms.Modules.Setup/SetupModule.cs`
   - Map endpoint
   - Ensure allowlist for setup access

---

## Phase 6 - Setup UI

### Modify

33. `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.cshtml`
   - Add new section before `Admin Identity`:
     - Database dropdown: `Embedded`, `Server`
     - Cache dropdown: `Memory`, `Embedded`, `Server`
     - Secret provider dropdown: `Local Certificate (Recommended)`, `Infisical`
     - Conditional DB server connection string field
     - Conditional cache server connection string field
     - Conditional Infisical machine id + client secret fields
     - Embedded readiness card for Postgres/Garnet
   - Disable submit when required embedded services are not ready
   - Preserve existing styling/colors

---

## Phase 7 - Setup Page Model / Orchestration

### Modify

34. `src/Aero.Cms.Modules.Setup/Areas/MyFeature/Pages/Setup.cshtml.cs`
   - Add input fields:
     - `DatabaseMode`
     - `CacheMode`
     - `SecretProvider`
     - `ConnectionString`
     - `CacheConnectionString`
     - `InfisicalMachineId`
     - `InfisicalClientSecret`
   - Persist bootstrap config
   - Continue setup seed flow using selected bootstrap configuration

### Add

35. `src/Aero.Cms.Modules.Setup/Bootstrap/IDatabaseBootstrapService.cs`

36. `src/Aero.Cms.Modules.Setup/Bootstrap/DatabaseBootstrapService.cs`

37. `src/Aero.Cms.Modules.Setup/Bootstrap/ICacheBootstrapService.cs`

38. `src/Aero.Cms.Modules.Setup/Bootstrap/CacheBootstrapService.cs`

39. `src/Aero.Cms.Modules.Setup/Bootstrap/IConnectionStringResolver.cs`

40. `src/Aero.Cms.Modules.Setup/Bootstrap/ConnectionStringResolver.cs`

---

## Phase 8 - TypeScript / Client Behavior

### Modify

41. `src/Aero.Cms.Web/ts/app.ts`
   - Add setup-specific logic for:
     - DB mode toggle
     - cache mode toggle
     - secret provider toggle
     - readiness polling
     - submit enable/disable
     - conditional form sections

---

## Phase 9 - Existing Seeding Integration

### Modify

42. `src/Aero.Cms.Modules.Setup/SeedDataService.cs`
   - Keep focused on admin/site seed
   - Remove responsibility for DB/cache/bootstrap secret logic

---

## Phase 10 - Tests

### Add / Update

43. Setup page model tests
   - DB mode validation
   - cache mode validation
   - provider validation
   - embedded readiness gating

44. Appsettings writer tests
   - env-specific writes
   - atomic updates

45. Local certificate secret manager tests

46. Infisical secret manager tests

47. AppServer readiness tests
   - Postgres ready
   - Garnet ready
   - combined readiness

48. Bootstrap flow tests
   - first run with no infra
   - embedded DB + memory cache
   - embedded DB + embedded cache
   - server DB + memory cache
   - server DB + server cache

---

## Recommended Execution Order

1. Bootstrap state/provider
2. Program/startup split
3. Secret abstractions
4. Appsettings writer
5. AppServer readiness
6. Setup status endpoint
7. Setup page model
8. Setup UI + TS
9. DB/cache bootstrap services
10. Seed integration
11. Tests

---

## Additional Confirmed Assumptions

- Secret provider UI label is `Local Certificate (Recommended)`
- Local secret protection uses ASP.NET Core Data Protection + X509 certificate
- Both DB and cache server connection strings use the selected secret provider
- `Memory` cache mode requires no readiness wait
- `Embedded` cache mode uses Garnet readiness
- `Server` cache mode stores external connection settings securely like server DB mode
