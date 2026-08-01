
> [!IMPORTANT]
> **STORAGE SUPERSEDED — MARTEN IS NO LONGER USED.** The backend database is now
> **SurrealDB via AeroDB.Sable** (embedded SurrealKV or remote server). Marten
> was migrated out in [`surrealdb-marten-port.md`](surrealdb-marten-port.md).
> This document is a historical implementation record; its Marten/PostgreSQL
> persistence details do not reflect the current stack.

# Manager UI Refactor Plan

## Problem Statement

1. **Dashboard flash on login**. When visiting `/manager`, the Dashboard briefly renders before flipping to the login screen. This is a race condition between auth state resolution, the layout's redirect logic, and how `@Body` is guarded.
2. **Slow WASM load**. The WASM payload is 120 MB uncompressed / 32.8 MB gzipped (800 files) due to no trimming, heavy NuGet packages in the compile graph (Radzen, BlazorMonaco, Marten/Orleans/Wolverine transitives), and 22 typed HTTP clients loaded eagerly.
3. **No prerendering**. `prerender: false` means users see a blank page until WASM fully initializes. Prerendering was attempted but crashed because server-side DI is missing `AdminStateContainer`, `ISitesHttpClient`, etc.
4. **Bloated dependency graph**. The entire server stack (Marten, Orleans, Wolverine, EF Core) flows into the WASM client compile output through `Aero.Cms.Abstractions` → `Aero.Cms.Core` → `Aero.Cms.Shared`.

---

## Status

| Item | Status |
|------|--------|
| WASM trimming + optimization (`PublishTrimmed`, `InvariantGlobalization`, etc.) | ✅ Done |
| `firstRender` redirect guard removed from `ManagerShellLayout` | ✅ Done |
| Layout state machine (`ShellState` enum) | ✅ Done |
| `Aero.Cms.Contracts` project created | ✅ Done |
| `AppState` created with user info + site context + cascading params | ✅ Done |
| Extended `/auth/me` with `UserId` + `Nickname` | ✅ Done |
| Extended `ServerAuthenticationStateProvider` with `user_id` + `nickname` claims | ✅ Done |
| Wired `AppState.SetUser()` / `AppState.SetSite()` in layout | ✅ Done |
| Per-site permission model in API + `AppState.HasPermission()` | 🟡 In progress |
| Contracts split (`SiteInfo` DTO, `ISitesHttpClient`, `ICurrentSiteAccessor`) | 🟡 In progress |
| Hybrid auth provider (snapshot + HTTP fallback) | 🟡 In progress |
| Prerendering enabled | ❌ Reverted (crashed — missing server-side DI) |
| Dashboard flash eliminated | ⏳ Needs verification |
| Lazy loading | ❌ Not started |

---

## Phase 0: Root Cause — Why the Dashboard Flashes

### Bug A: Layout redirect only fires on `firstRender=true` ✅ FIXED

`ManagerShellLayout.OnAfterRenderAsync` (line 114-122):

```csharp
// BEFORE (broken):
if (firstRender && !string.IsNullOrWhiteSpace(redirectTarget))
    Navigation.NavigateTo(redirectTarget, forceLoad: true);

// AFTER (fixed — 2026-05-07):
if (!string.IsNullOrWhiteSpace(redirectTarget))
    Navigation.NavigateTo(redirectTarget, forceLoad: true);
```

**Why this was enough:** The `redirectTarget` is set asynchronously (after `await AuthenticationState` completes in `OnInitializedAsync`), which happens AFTER the first render. With the old `firstRender` guard, the redirect never fired because `firstRender` was already `false` by the time `redirectTarget` was populated. Now the redirect fires on the render cycle immediately after auth resolves.

### Bug B: `@Body` is guarded behind `siteContextReady`

```razor
@if (siteContextReady) { ... @Body ... } else { "Loading manager..." }
```

The `AuthorizeRouteView`'s `NotAuthorized` and `Authorizing` templates are rendered **inside** `@Body`. When `siteContextReady` is `false`, `@Body` is never invoked.

**However**, with Bug A fixed, this is no longer the active failure path. When auth resolves to unauthenticated:
1. `redirectTarget` is set to `/manager/login?...` (siteContextReady stays `false`)
2. On the next `OnAfterRenderAsync` (any render), the redirect fires
3. `@Body` never needs to render because the redirect happens before another layout cycle

The `@Body` guard becomes a problem only if we add a prerendering state machine later.

### Bug C: Dual auth sources (affects prerendering only)

The server registers `AddAuthenticationStateSerialization()` (line 265 in `Program.cs`), which serializes `HttpContext.User` into the initial page. The client registers `ServerAuthenticationStateProvider`, which makes its own HTTP call to `/api/v1/admin/auth/me`. With `prerender: false`, this isn't currently an issue — but it will block prerendering.

**Critical finding:** The `ServerAuthenticationStateProvider` doc comment explicitly says it *"replaces `AddAuthenticationStateDeserialization()`"*. There is no `AddAuthenticationStateDeserialization()` on the client. When prerendering is enabled, the serialized auth state from the server has **no consumer** on the client — making it dead data and creating a brief auth mismatch window.

---

## Phase 1: Layout State Machine (Replace Auth/Site Gating)

### Problem

The current `bool siteContextReady` flag conflates multiple states:
- Auth still loading (HTTP call to `/api/v1/admin/auth/me` in flight)
- Auth resolved → unauthenticated (redirect needed)
- Auth resolved → authenticated, site context loading
- Auth resolved → authenticated, no site found (redirect to /manager/select-site)
- Ready

A single boolean can't distinguish "waiting for auth" from "waiting for site." If `@Body` is rendered in any non-ready state, protected content may flash.

### Solution: Explicit state enum

**File:** `src/Aero.Cms.Shared/Layout/ManagerShellLayout.razor`

```csharp
private enum ShellState { AuthPending, Unauthenticated, SiteResolving, Ready }

private ShellState shellState = ShellState.AuthPending;
```

The template renders based on explicit state:

```razor
@switch (shellState)
{
    case ShellState.AuthPending:
        @* Show Authorizing template — @Body renders so AuthorizeRouteView can display it *@
        @Body
        break;

    case ShellState.Unauthenticated:
        @* Redirect handled in OnAfterRenderAsync, show nothing *@
        break;

    case ShellState.SiteResolving:
        <div class="pe-editor-area flex items-center justify-center min-h-screen">
            <span class="text-sm text-gray-500">Loading site context...</span>
        </div>
        @* @Body NOT rendered — prevent Dashboard flash before site resolves *@
        break;

    case ShellState.Ready:
        <div class="pe-main-layout">
            <AeroSidebar ...>...</AeroSidebar>
            <div class="pe-editor-area">@Body</div>
            <SectionOutlet SectionName="RightSidebar" />
        </div>
        break;
}
```

**Key difference from old code:** `@Body` is rendered in `AuthPending` state so the `AuthorizeRouteView`'s `Authorizing` template appears (instead of a duplicate "Loading manager..." message). `@Body` is NOT rendered in `SiteResolving` — preventing the Dashboard from flashing before site context resolves.

### Updated `OnInitializedAsync` logic

```csharp
protected override async Task OnInitializedAsync()
{
    var relative = Navigation.ToBaseRelativePath(Navigation.Uri);
    if (relative.StartsWith("manager/login", ...) ||
        relative.StartsWith("manager/select-site", ...))
    {
        shellState = ShellState.Ready;
        return;
    }

    if (!AdminState.IsInitialized)
        AdminState.LoadFromStorage();

    shellState = ShellState.AuthPending;

    if (AuthenticationState is not null)
    {
        var authState = await AuthenticationState;
        if (authState.User.Identity?.IsAuthenticated != true)
        {
            shellState = ShellState.Unauthenticated;
            redirectTarget = BuildLoginRedirectUrl(relative);
            return;
        }

        // Auth confirmed — resolve site context
        shellState = ShellState.SiteResolving;
        var siteId = AdminState.CurrentSiteId
            ?? await CurrentSiteAccessor.GetCurrentSiteIdAsync();

        if (AdminState.CurrentSiteId.HasValue)
            await CurrentSiteAccessor.SetCurrentSiteAsync(AdminState.CurrentSiteId.Value);

        if (siteId is null)
            siteId = await TryGetDefaultSiteAsync();

        if (siteId is null)
        {
            redirectTarget = "/manager/select-site";
            return;
        }

        shellState = ShellState.Ready;
        return;
    }

    // No cascading auth state available (shouldn't happen with CascadingAuthenticationState)
    shellState = ShellState.Unauthenticated;
    redirectTarget = BuildLoginRedirectUrl(relative);
}
```

### `OnAfterRenderAsync` stays as-is

```csharp
protected override Task OnAfterRenderAsync(bool firstRender)
{
    if (!string.IsNullOrWhiteSpace(redirectTarget))
        Navigation.NavigateTo(redirectTarget, forceLoad: true);
    return Task.CompletedTask;
}
```

No `firstRender` guard — already fixed.

---

## Phase 1.5: Unified Application State (`AppState`)

### Design Goals

1. **Single source of truth** for app-wide state that replaces the scattered `AdminStateContainer` + `ICurrentSiteAccessor` + `IAdminStorage` orchestration in `ManagerShellLayout`.
2. **Accessible from any component** via both DI injection (`@inject`) and cascading parameters (`[CascadingParameter]`).
3. **Singleton lifetime** — per Microsoft's state management docs, client-side state containers use `AddSingleton` (only one WASM app instance per browser tab, so Singleton = Scoped in practice).
4. **Persistent state** across prerendering using .NET 10's `PersistentComponentState` imperative API (the declarative `RegisterPersistentService<T>` is scoped-only, so we use the direct API for our Singleton).
5. **Auto-notification** — components subscribe to `StateChanged` and call `StateHasChanged` automatically.
6. **Site verification on reload** — validates stored site ID against the server API, falls back to default if the site was deleted.

### Why Not `RegisterPersistentService<T>`

Microsoft's docs state: *"Only persisting scoped services is supported"* for `RegisterPersistentService<T>`. Our `AppState` is Singleton (per Microsoft's client-side state container guidance). We use the **imperative API** instead:
- `PersistentComponentState.TryTakeFromJson<T>()` — restore state during component init
- `PersistentComponentState.RegisterOnPersisting()` — persist state during prerendering
- `PersistentComponentState.PersistAsJson()` — write state

### Class Design

**File:** `src/Aero.Cms.Contracts/Services/AppState.cs`

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Contracts.Services;

/// <summary>
/// Singleton application state container for the Blazor manager client.
/// Replaces the ad-hoc AdminStateContainer + ICurrentSiteAccessor + localStorage
/// orchestration with a single unified state service.
///
/// Accessible via:
///   - DI: @inject AppState AppState
///   - Cascading parameter: [CascadingParameter] public AppState? AppState { get; set; }
///
/// Components that consume AppState MUST subscribe to StateChanged and
/// call StateHasChanged, and unsubscribe in Dispose().
/// </summary>
public sealed class AppState
{
    private readonly ILogger<AppState> _logger;

    public AppState(ILogger<AppState> logger)
    {
        _logger = logger;
    }

    // ── Site Context ──────────────────────────────────────────────

    public long? CurrentSiteId { get; private set; }
    public string? CurrentSiteName { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────

    /// <summary>
    /// True after VerifySiteOnStartupAsync completes (whether or not a site was found).
    /// The manager shell layout uses this to gate the sidebar.
    /// </summary>
    public bool IsSiteContextReady { get; private set; }

    // ── Notification ──────────────────────────────────────────────

    /// <summary>
    /// Fired whenever state changes. Components subscribe via
    /// StateChanged += StateHasChanged and unsubscribe in Dispose().
    /// </summary>
    public event Action? StateChanged;

    // ── Setters ───────────────────────────────────────────────────

    /// <summary>
    /// Sets the current site. Updates in-memory state and fires StateChanged.
    /// Does NOT persist to localStorage or server cookie — callers must handle that.
    /// </summary>
    public void SetSite(long siteId, string? siteName)
    {
        if (CurrentSiteId == siteId && CurrentSiteName == siteName)
            return;

        var oldId = CurrentSiteId;
        CurrentSiteId = siteId;
        CurrentSiteName = siteName;
        IsSiteContextReady = true;

        _logger.LogInformation("AppState: Site changed {OldId} → {NewId} ({SiteName})",
            oldId, siteId, siteName);

        StateChanged?.Invoke();
    }

    public void SetSiteContextReady() => IsSiteContextReady = true;

    // ── Site Verification on Startup ──────────────────────────────

    /// <summary>
    /// Verifies the cached site ID against the server and resolves the active site.
    /// Called once on app startup by the manager shell or bootstrap component.
    ///
    /// Flow:
    /// 1. Read cached site ID from localStorage via IAdminStorage
    /// 2. Call ISitesHttpClient.GetByIdAsync(cachedId) to verify it still exists
    /// 3. If it exists → use it, update server cookie
    /// 4. If not → call GetDefaultAsync() to get the default site
    /// 5. Compare localStorage ID with server-returned ID
    ///    - Same → continue (no-op)
    ///    - Different → update localStorage, update AppState, update server cookie
    /// 6. Log all transitions
    /// </summary>
    public async Task VerifySiteOnStartupAsync(
        IAdminStorage storage,
        ISitesHttpClient sitesClient,
        ICurrentSiteAccessor siteAccessor)
    {
        var storedSiteId = storage.GetItem<long?>("aero-admin-state.siteId");
        var storedSiteName = storage.GetItem<string>("aero-admin-state.siteName");

        _logger.LogDebug("AppState: Startup verification — cached site {SiteId} ({SiteName})",
            storedSiteId, storedSiteName);

        SiteViewModel? resolvedSite = null;

        // Step 1: Try to verify the cached site still exists
        if (storedSiteId.HasValue && storedSiteId.Value > 0)
        {
            var result = await sitesClient.GetByIdAsync(storedSiteId.Value);
            if (result is Result<SiteViewModel, AeroError>.Ok { Value: not null } ok)
            {
                resolvedSite = ok.Value;
                _logger.LogInformation(
                    "AppState: Cached site {SiteId} verified — still exists on server",
                    storedSiteId.Value);
            }
            else
            {
                _logger.LogWarning(
                    "AppState: Cached site {SiteId} no longer exists. " +
                    "Switching to default site.",
                    storedSiteId.Value);
            }
        }

        // Step 2: If cached site is gone (or never existed), get the default
        if (resolvedSite is null)
        {
            var defaultResult = await sitesClient.GetDefaultAsync();
            if (defaultResult is Result<SiteViewModel, AeroError>.Ok { Value: not null } defaultOk)
            {
                resolvedSite = defaultOk.Value;
                _logger.LogInformation(
                    "AppState: Using default site {SiteId} ({SiteName})",
                    resolvedSite.Id, resolvedSite.Name);
            }
        }

        // Step 3: No site available at all
        if (resolvedSite is null)
        {
            _logger.LogWarning("AppState: No site available. Site selection required.");
            IsSiteContextReady = true;
            StateChanged?.Invoke();
            return;
        }

        // Step 4: Compare and update
        if (storedSiteId != resolvedSite.Id)
        {
            _logger.LogInformation(
                "AppState: Site ID mismatch — localStorage {StoredId} → server {ServerId} ({SiteName}). " +
                "Updating localStorage.",
                storedSiteId, resolvedSite.Id, resolvedSite.Name);

            storage.SetItem("aero-admin-state.siteId", resolvedSite.Id);
            storage.SetItem("aero-admin-state.siteName", resolvedSite.Name);
        }

        // Step 5: Set server cookie + update AppState
        await siteAccessor.SetCurrentSiteAsync(resolvedSite.Id);
        SetSite(resolvedSite.Id, resolvedSite.Name);
    }

    // ── PersistentComponentState Integration ──────────────────────

    /// <summary>
    /// Persists state during prerendering. Called from RegisterOnPersisting callback.
    /// </summary>
    public Task PersistStateAsync(PersistentComponentState state)
    {
        state.PersistAsJson(nameof(CurrentSiteId), CurrentSiteId);
        state.PersistAsJson(nameof(CurrentSiteName), CurrentSiteName);
        state.PersistAsJson(nameof(IsSiteContextReady), IsSiteContextReady);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Restores state after prerendering. Returns true if any state was restored.
    /// </summary>
    public bool TryRestoreState(PersistentComponentState state)
    {
        var restored = false;

        if (state.TryTakeFromJson<long?>(nameof(CurrentSiteId), out var id))
        {
            CurrentSiteId = id;
            restored = true;
        }
        if (state.TryTakeFromJson<string>(nameof(CurrentSiteName), out var name))
        {
            CurrentSiteName = name;
            restored = true;
        }
        if (state.TryTakeFromJson<bool>(nameof(IsSiteContextReady), out var ready))
        {
            IsSiteContextReady = ready;
            restored = true;
        }

        if (restored)
        {
            _logger.LogDebug("AppState: Restored from persistent state — site {SiteId} ({SiteName})",
                CurrentSiteId, CurrentSiteName);
        }

        return restored;
    }
}
```

### Dual Access: DI Injection + Cascading Parameter

Components access `AppState` through **both** mechanisms:

**DI injection** (`@inject`) — for code-behind and services:
```razor
@inject AppState AppState
```

```csharp
// In a non-component service or code-behind
public class SomeService(AppState appState)
{
    // Use appState.CurrentSiteId, subscribe to appState.StateChanged
}
```

**Cascading parameter** — for component-level reactivity:
```razor
@code {
    [CascadingParameter]
    public AppState? AppState { get; set; }

    protected override void OnInitialized()
    {
        AppState!.StateChanged += StateHasChanged;
    }

    public void Dispose()
    {
        AppState!.StateChanged -= StateHasChanged;
    }
}
```

The cascading value is set at the root level in `App.razor`:

```razor
@* App.razor *@
<CascadingValue Value="@AppState">
    <Routes @rendermode="..." />
</CascadingValue>

@code {
    [Inject] public AppState AppState { get; set; } = default!;
}
```

### Service Registration

```csharp
// Aero.Cms.Web.Client/Program.cs (WASM client)
builder.Services.AddSingleton<AppState>();

// Aero.Cms.Web/Program.cs (Server host — for prerendering)
builder.Services.AddScoped<AppState>();  // Scoped for prerendering context
```

**Note on Singleton vs Scoped:** The client uses `AddSingleton` per Microsoft's client-side guidance. The server uses `AddScoped` because prerendering creates a new scope per request. `RegisterPersistentService<T>` is NOT used — instead, `PersistentComponentState` is used imperatively in the root component.

### Migrating ManagerShellLayout to Use AppState

The manager shell layout currently orchestrates `AdminStateContainer`, `ICurrentSiteAccessor`, `IAdminStorage`, and `ISitesHttpClient` in `OnInitializedAsync`. After AppState is introduced, the layout simplifies significantly:

```csharp
// ManagerShellLayout.razor — simplified OnInitializedAsync
protected override async Task OnInitializedAsync()
{
    var relative = Navigation.ToBaseRelativePath(Navigation.Uri);

    // Login and site-selection pages skip all verification
    if (relative.StartsWith("manager/login", ...) ||
        relative.StartsWith("manager/select-site", ...))
    {
        shellState = ShellState.Ready;
        return;
    }

    shellState = ShellState.AuthPending;

    if (AuthenticationState is not null)
    {
        var authState = await AuthenticationState;
        if (authState.User.Identity?.IsAuthenticated != true)
        {
            shellState = ShellState.Unauthenticated;
            redirectTarget = BuildLoginRedirectUrl(relative);
            return;
        }

        // Auth confirmed — delegate site verification to AppState
        shellState = ShellState.SiteResolving;
        await AppState.VerifySiteOnStartupAsync(Storage, SitesClient, SiteAccessor);

        if (AppState.CurrentSiteId is null)
        {
            redirectTarget = "/manager/select-site";
            return;
        }

        shellState = ShellState.Ready;
        return;
    }

    shellState = ShellState.Unauthenticated;
    redirectTarget = BuildLoginRedirectUrl(relative);
}
```

The old `AdminStateContainer.LoadFromStorage()`, `TryGetDefaultSiteAsync()`, and manual `redirectTarget` logic for `select-site` are all replaced by `AppState.VerifySiteOnStartupAsync()`.

---

## Phase 1.6: Council Findings + Permission Model

### Council Verdict (2026-05-07)

The council reviewed the auth architecture and gave these recommendations:

| Question | Consensus | Confidence |
|----------|-----------|------------|
| **Q1: When to migrate auth provider?** | **Act now** — hybrid provider that first tries `PersistentComponentState`, falls back to HTTP | Strong majority |
| **Q2: Profile extras pattern?** | **Two-phase auth**: instant from snapshot + lazy HTTP enrichment for rich profile | Unanimous |
| **Q3: Claims vs AppState for permissions?** | **Dual ownership**: claims are authoritative for `[Authorize]` policies; AppState derives from claims for UI convenience | Unanimous |
| **Q4: Orleans SDK in Contracts?** | **No** — create WASM-safe `SiteInfo` DTO, map at module boundary (do NOT reference `Orleans.Sdk` in Contracts) | Unanimous |
| **Q5: Implementation order?** | **Permissions before prerendering** — define permission claims taxonony before flipping `prerender: true` | Strong majority |

### Per-User + Per-Site Permission Model

Permissions are **per-user AND per-site specific**. A user may have `CRUD` on Site A but `Read-only` on Site B.

#### Traditional ASP.NET Core Pattern: Resource-Based Authorization

Microsoft's [resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased) uses `AuthorizationHandler<TRequirement, TResource>` where `TResource` is the thing being protected. In our case, the **resource is the site ID**.

This means:

| Layer | Mechanism | Authority |
|-------|-----------|-----------|
| **Server API** | `IAuthorizationService.AuthorizeAsync(User, siteId, operation)` | **Claims are authoritative** |
| **Blazor UI** | `AppState.HasPermission("content", "W")` | Derives from claims |
| **Auth cookie / `/auth/me`** | Claims like `perm_123_content: "CRUD"` | Set at login, refreshed lazily |

#### Claims Structure

Permissions stored as claims with structured values:

```
Type: "permission", Value: "123|content|CRUD"   // site 123, content: full
Type: "permission", Value: "123|media|RW"        // site 123, media: read+write
Type: "permission", Value: "456|content|R"       // site 456, content: read-only
```

Or as individual typed claims:
```
Type: "perm_123_content", Value: "CRUD"
Type: "perm_123_media", Value: "RW"
```

#### Custom AuthorizationHandler for API Enforcement

```csharp
public class SitePermissionHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, long>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        long siteId)  // resource = site ID
    {
        // Admin overrides all per-site perms
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check structured permission claim
        var claimValue = context.User.FindFirstValue(
            $"perm_{siteId}_{requirement.Name}");

        if (claimValue is not null &&
            (claimValue == requirement.Name || claimValue == "CRUD"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
```

#### AppState Permission API (Derives from Claims)

```csharp
public sealed class AppState
{
    private Dictionary<string, string> _permissions = new();

    /// <summary>
    /// Loads permissions from the ClaimsPrincipal after auth resolves.
    /// Called by ManagerShellLayout once per session.
    /// </summary>
    public void LoadPermissions(ClaimsPrincipal user)
    {
        _permissions.Clear();
        foreach (var claim in user.FindAll("permission"))
        {
            var parts = claim.Value.Split('|');
            if (parts.Length == 3) // "123|content|CRUD"
                _permissions[$"{parts[0]}:{parts[1]}"] = parts[2];
        }
    }

    /// <summary>
    /// Checks if the current user has the specified operation on the given domain
    /// for the CURRENTLY SELECTED SITE (AppState.CurrentSiteId).
    /// </summary>
    public bool HasPermission(string domain, char operation)
    {
        if (CurrentSiteId is null) return false;
        var key = $"{CurrentSiteId}:{domain}";
        return _permissions.TryGetValue(key, out var perm) && perm.Contains(operation);
    }

    public bool CanRead(string domain) => HasPermission(domain, 'R');
    public bool CanWrite(string domain) => HasPermission(domain, 'W') || HasPermission(domain, 'C');
    public bool CanDelete(string domain) => HasPermission(domain, 'D');
}
```

#### Why This Pattern

- **Claims are the authoritative source** for authorization decisions (standard ASP.NET Core)
- **AppState derives permissions from claims** for Blazor UI gating (claims are not easily queried in templates)
- **Resource-based handler** makes the site ID part of the authorization decision (standard `AuthorizationHandler<TReq, TResource>`)
- **`AddAuthenticationStateSerialization()`** serializes role claims; per-site perm claims need lazy fetching via `/auth/me`

### Hybrid Auth Provider

Instead of swapping `ServerAuthenticationStateProvider` for `PersistentAuthenticationStateProvider`, evolve the existing provider to try both sources:

```csharp
internal sealed class ServerAuthenticationStateProvider(
    HttpClient httpClient,
    PersistentComponentState persistentState)  // NEW: inject for prerender support
    : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Phase 1: Try deserialized auth state (from prerendering)
        if (persistentState.TryTakeFromJson<AuthStateSnapshot>(
            "AuthState", out var snapshot) && snapshot is not null)
        {
            return BuildAuthState(snapshot);
        }

        // Phase 2: Fall back to HTTP call (current behavior)
        try
        {
            var response = await httpClient.GetAsync($"/{HttpConstants.ApiPrefix}admin/auth/me");
            // ... existing logic ...
        }
        catch { return Unauthenticated; }
    }
}
```

This works with BOTH `prerender: false` (falls through to HTTP since no snapshot exists) and `prerender: true` (instant from snapshot).

The rich profile data (userId, nickname, permissions) is still fetched asynchronously and cached in `AppState` via the existing `SetUser()` path.

### Revised Implementation Order

| Order | Phase | Risk | Impact | Dependencies |
|-------|-------|------|--------|-------------|
| **1** | Phase 1: State machine + firstRender fix | Low | Fixes the flash | ✅ Done |
| **2** | Phase 1.5: AppState + Contracts project | Low | Unified state container | ✅ Done |
| **3** | **Phase 2a: Add per-site permissions to `/auth/me` + AppState** | Medium | Permission gating foundation | None (additive) |
| **4** | **Phase 2b: Contracts split** — `SiteInfo`, `ISitesHttpClient`, `ICurrentSiteAccessor` in Contracts | **High** | Removes Orleans/Marten/Polly from WASM | Step 3 (Contracts exists) |
| **5** | **Phase 2c: Hybrid auth provider** — `PersistentComponentState` + HTTP fallback | Medium | Works with both prerender modes | Step 4 |
| **6** | Phase 3: Server DI stubs + enable prerendering | Medium | Instant page render | Steps 4, 5 |
| **7** | Phase 4.1: Monaco JS deferral | Low | ~5 MB JS deferred | None |
| **8** | Phase 5.1: Cache boot resources | Low | Faster subsequent loads | None |
| **9** | Phase 4.2: Radzen architectural split | High | 2.75 MB WASM deferred | Deferred |

### Corrected Dependency Map

The problem is deeper than the original plan showed. The full chain:

```
Aero.Cms.Web.Client (WASM host)
  └─ Aero.Cms.Shared (Razor components)
       └─ Aero.Cms.Core (business logic)
            ├─ Marten (5+ MB)
            ├─ WolverineFx
            ├─ Serilog
            ├─ Scriban
            ├─ Aero.EfCore
            └─ Aero.Cms.Abstractions  ← THE HUB
                 ├─ Microsoft.Orleans.Sdk
                 ├─ Aero.Actors
                 ├─ Aero.Marten
                 ├─ Aero.Core
                 ├─ FluentValidation
                 ├─ Mapster
                 └─ Microsoft.Extensions.Http.Resilience (Polly)
```

**`Aero.Cms.Abstractions` is the root problem** — it pulls Orleans, Marten, and resilience packages into a project that both server and client depend on transitively.

### Solution: Split into `Aero.Cms.Contracts` + keep current `Aero.Cms.Abstractions`

| Project | Contents | Dependencies | Consumers |
|---------|----------|-------------|-----------|
| **`Aero.Cms.Contracts`** (NEW) | Pure DTOs, interfaces, enums. `IAdminStorage`, `AdminStateContainer`, `ManagerThemeService`, `ICurrentSiteAccessor`, `IFormFactor`, HTTP client interfaces, view models. | `Aero.Core` only (for `Result<T>`, `Option<T>`, `IEntity<long>`) | WASM client, Shared, Core, Web, Abstractions |
| **`Aero.Cms.Abstractions`** (KEEP) | HTTP client implementations, handler registrations, Orleans grain interfaces, Marten repository abstractions. | Orleans, Marten, Aero.Actors, Polly | Server, Core, Web only |
| **`Aero.Cms.Shared`** (MODIFY) | Razor components (`ManagerShellLayout`, `Dashboard`, `PostEditor`, etc.). | `Aero.Cms.Contracts` instead of `Aero.Cms.Core` where possible. Still → `Aero.Cms.Core` for server-side rendering only. | WASM client + Server |
| **`Aero.Cms.Web.Client`** (MODIFY) | WASM host + `ServerAuthenticationStateProvider` + `LocalStorageAdminStorage`. | `Aero.Cms.Contracts` instead of `Aero.Cms.Shared` where possible. Still → `Aero.Cms.Shared` for shared Razor components. | Browser only |

### What Moves Where

**To `Aero.Cms.Contracts`:**
- **`AppState`** (NEW — unified application state, see Phase 1.5)
- `IAdminStorage` (interface from `Aero.Cms.Shared/Services/`)
- `ManagerThemeService` (from `Aero.Cms.Shared/Services/`)
- `ICurrentSiteAccessor` (from `Aero.Cms.Abstractions/Interfaces/`)
- `IFormFactor` (if used by WASM)
- HTTP client *interfaces* (`IAuthClient`, `ISitesHttpClient`, etc.) — the contract, not the implementation
- `SiteViewModel`, `BlogAuthor`, `JwtTokenResponse`, `CurrentUserResponse` — all DTOs/view models used by WASM

**Note:** `AdminStateContainer` (from `Aero.Cms.Shared/Services/`) is obsoleted by `AppState`. It should be kept during migration but marked `[Obsolete]` once all consumers switch to `AppState`.

**Stay in `Aero.Cms.Abstractions`:**
- HTTP client *implementations* (`AuthClient`, `SitesHttpClient`, etc.)
- HTTP message handlers (`CorrelationIdHandler`, `JwtTokenHandler`, etc.)
- `AeroHttpClientRegistrations` — the service collection extension that registers all 22 clients
- Orleans grain interfaces
- Marten repository abstractions

### Service Registration Split

```csharp
// Aero.Cms.Web.Client/Program.cs (WASM host)
// Contracts-based services only — no Orleans, no Marten
builder.Services.AddSingleton<IAdminStorage, LocalStorageAdminStorage>();
builder.Services.AddSingleton<AdminStateContainer>();  // Will change to Scoped for prerendering
builder.Services.AddScoped<ManagerThemeService>();
// HTTP clients via typed HttpClient — client implementations from Aero.Cms.Abstractions

// Aero.Cms.Web/Program.cs (Server host)
builder.Services.AddScoped<IAdminStorage, NoopAdminStorage>();  // For prerendering
builder.Services.AddScoped<AdminStateContainer>();               // For prerendering
builder.Services.AddScoped<ManagerThemeService>();
// Full Aero.Cms.Abstractions HTTP client registrations
```

---

## Phase 3: Enable Prerendering (Corrected)

### 3.1 Service lifetime: Singleton → Scoped

`RegisterPersistentService<T>()` only supports **scoped** services (per Microsoft docs). `AdminStateContainer` is currently registered as `Singleton`.

**Fix:** Change to `Scoped` in both WASM client and server. Verify no state-leak issues. Given that Blazor WASM is single-user per app instance, `Scoped` is effectively equivalent to `Singleton` in practice.

```csharp
// Aero.Cms.Web.Client/Program.cs
- builder.Services.AddSingleton<AdminStateContainer>();
+ builder.Services.AddScoped<AdminStateContainer>();

// Aero.Cms.Web/Program.cs
+ builder.Services.AddScoped<AdminStateContainer>();
```

### 3.2 Property shapes for `[PersistentState]`

`[PersistentState]` requires:
- `public` properties
- `public` setters (for JSON deserialization during restore)

Current state:

| Property | Access | Fix needed |
|----------|--------|------------|
| `AdminStateContainer.CurrentSiteId` | `public long? { get; private set; }` | Change to `public long? { get; set; }` |
| `AdminStateContainer.CurrentSiteName` | `public string? { get; private set; }` | Change to `public string? { get; set; }` |
| `AdminStateContainer.IsInitialized` | `public bool { get; private set; }` | Change to `public bool { get; set; }` |
| `AdminStateContainer.CurrentView` | `public string? { get; set; }` | ✅ Already correct |
| `ManagerThemeService.IsDarkMode` | `public bool => _isDarkMode` (computed) | Refactor to stored property |
| `ManagerThemeService.Theme` | `public string => ...` (computed, no setter) | Refactor to stored property |
| `ManagerThemeService.IsSidebarCollapsed` | `public bool { get; private set; }` | Change to `public bool { get; set; }` |

Alternatively, use the **imperative API** (`RegisterOnPersisting` / `TryTakeFromJson`) which doesn't require public setters:

```csharp
// AdminStateContainer.cs — alternative approach
public void RegisterForPersistence(PersistentComponentState state)
{
    state.RegisterOnPersisting(() =>
    {
        state.PersistAsJson(nameof(CurrentSiteId), CurrentSiteId);
        state.PersistAsJson(nameof(CurrentSiteName), CurrentSiteName);
        state.PersistAsJson(nameof(IsInitialized), IsInitialized);
        return Task.CompletedTask;
    });
}

public void RestoreFromPersistence(PersistentComponentState state)
{
    if (state.TryTakeFromJson<long?>(nameof(CurrentSiteId), out var id))
        CurrentSiteId = id;
    if (state.TryTakeFromJson<string>(nameof(CurrentSiteName), out var name))
        CurrentSiteName = name;
    if (state.TryTakeFromJson<bool>(nameof(IsInitialized), out var init))
        IsInitialized = init;
}
```

This preserves the existing encapsulation (private setters) at the cost of more boilerplate.

### 3.3 Fix auth state serialization for prerendering

**The gap:** `ServerAuthenticationStateProvider` explicitly replaces `AddAuthenticationStateDeserialization()`. When prerendering produces serialized auth state (from `AddAuthenticationStateSerialization()` on the server), there is no consumer on the client.

**Fix — Option A (preferred):** Adopt `AddAuthenticationStateDeserialization()` if available in .NET 10:

```csharp
// Aero.Cms.Web.Client/Program.cs
builder.Services.AddAuthenticationStateDeserialization();  // Replaces custom provider
```

Then remove `ServerAuthenticationStateProvider` registration. If the built-in deserialization doesn't meet needs, proceed to Option B.

**Fix — Option B:** Extend `ServerAuthenticationStateProvider` to consume serialized state first:

```csharp
internal sealed class ServerAuthenticationStateProvider(
    HttpClient httpClient,
    PersistentComponentState persistentState)  // NEW dependency
    : AuthenticationStateProvider
{
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // NEW: Try deserialized auth state first (from prerendering)
        if (persistentState.TryTakeFromJson<PersistedUserState>(
            "AuthState", out var persisted) && persisted is not null)
        {
            return BuildAuthState(persisted);
        }

        // EXISTING: fall back to HTTP call
        try
        {
            // ... existing HTTP logic ...
        }
        catch { return Unauthenticated; }
    }
}
```

**Fix — Option C (simplest):** Don't enable prerendering for auth-protected pages. Use `prerender: false` for `/manager/*` routes and `prerender: true` for public pages only. This defers the auth serialization problem.

### 3.4 Server-side DI stubs

**File:** `src/Aero.Cms.Web/Program.cs`

```csharp
// Server-side stubs for WASM services needed during prerendering
// Note: services must be Scoped to work with RegisterPersistentService
builder.Services.AddScoped<IAdminStorage, NoopAdminStorage>();
builder.Services.AddScoped<AdminStateContainer>();
builder.Services.AddScoped<ManagerThemeService>();
// ISitesHttpClient, ICurrentSiteAccessor — already registered server-side
```

**File:** `src/Aero.Cms.Web/Services/NoopAdminStorage.cs`

```csharp
/// <summary>
/// Server-side no-op IAdminStorage. During prerendering, localStorage
/// doesn't exist; all reads return default, writes are silently discarded.
/// </summary>
internal sealed class NoopAdminStorage : IAdminStorage
{
    public T? GetItem<T>(string key) => default;
    public void SetItem<T>(string key, T value) { }
}
```

### 3.5 Register persistent services + enable prerendering

```csharp
// Aero.Cms.Web/Program.cs
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization()
    // Persistent services — state computed server-side, restored on WASM
    .RegisterPersistentService<AdminStateContainer>(RenderMode.WebAssembly)
    .RegisterPersistentService<ManagerThemeService>(RenderMode.WebAssembly);
```

```razor
@* Aero.Cms.Web/Components/App.razor *@
- <HeadOutlet @rendermode="@(new InteractiveWebAssemblyRenderMode(prerender: false))" />
+ <HeadOutlet @rendermode="@(new InteractiveWebAssemblyRenderMode(prerender: true))" />
- <Routes @rendermode="@(new InteractiveWebAssemblyRenderMode(prerender: false))" />
+ <Routes @rendermode="@(new InteractiveWebAssemblyRenderMode(prerender: true))" />
```

**Prerequisite:** Phase 2 (contracts split) and Phase 3.3 (auth fix) must be complete first.

---

## Phase 4: Lazy Loading (Corrected)

### 4.1 BlazorMonaco — lazy-load the .NET assembly + JS

**The assembly must be removed from the boot graph, not just the JS scripts.**

**Step 1: Mark for lazy loading in `.csproj`**

```xml
<!-- Aero.Cms.Web.Client.csproj -->
<ItemGroup>
    <BlazorWebAssemblyLazyLoad Include="BlazorMonaco.dll" />
    <BlazorWebAssemblyLazyLoad Include="BlazorMonaco.wasm" />
</ItemGroup>
```

**Step 2: Remove static references from `Aero.Cms.Shared.csproj`**

```diff
- <PackageReference Include="BlazorMonaco" />
```

Instead, move BlazorMonaco to a separate project that is lazy-loaded, or accept the .NET assembly load (only 256 KB gzipped to 83 KB) and just defer the JS.

**Step 3: Load JS on-demand in PostEditor**

```razor
@* PostEditor.razor — load when code tab is first opened *@
@inject IJSRuntime JS

@code {
    private bool _monacoScriptsLoaded;

    private async Task EnsureMonacoLoadedAsync()
    {
        if (_monacoScriptsLoaded) return;

        await JS.InvokeVoidAsync("eval", @"
            if (!window.monaco) {
                var s = document.createElement('script');
                s.src = '_content/BlazorMonaco/jsInterop.js';
                document.head.appendChild(s);
            }
        ");
        _monacoScriptsLoaded = true;
    }
}
```

**Step 4: Remove from `App.razor`**
```diff
- <script src="_content/BlazorMonaco/jsInterop.js"></script>
- <script src="_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js"></script>
- <script src="_content/BlazorMonaco/lib/monaco-editor/min/vs/editor/editor.main.js"></script>
```

**Realistic assessment:** The JS assets (~5 MB) are the real win. The .NET assembly (83 KB gzipped) is negligible. Deferring just the JS scripts is a practical P1 win. Proper assembly lazy loading is P2.

### 4.2 Radzen.Blazor — NOT a lazy-load candidate

| Reason | Detail |
|--------|--------|
| Shell-critical | `<RadzenComponents />` is in `ManagerShellLayout.razor` — it renders on every manager page |
| Global imports | `_Imports.razor` references Radzen namespaces; 30+ `.razor` files statically reference Radzen types |
| Largest assembly | 2.75 MB WASM / 899 KB gzipped |

**Deferred to architectural split.** If Radzen is ever separated, it would require:
1. Move all Radzen-dependent pages to a separate assembly
2. Mark that assembly with `BlazorWebAssemblyLazyLoad` in the `.csproj`
3. Keep the manager shell (without Radzen components) in the boot graph

This is a significant multi-day refactor, not a quick optimization.

### 4.3 HTTP clients — review but don't block

22 typed HTTP clients with Polly resilience chains are registered on startup. Each HTTP client registration is lightweight at startup (no HTTP calls happen until first use). The real cost is the `Microsoft.Extensions.Http.Resilience` package pulling Polly into the WASM compile graph — this is fixed in Phase 2 (contracts split).

---

## Phase 5: Additional Optimizations

### 5.1 `BlazorCacheBootResources`

```xml
<!-- Aero.Cms.Web.Client.csproj -->
<BlazorCacheBootResources>true</BlazorCacheBootResources>
```

### 5.2 Verify trimming on .NET 10

```xml
<!-- Already applied -->
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>full</TrimMode>
```

If reflection-based serialization breaks:
- Add `[DynamicallyAccessedMembers]` to types used in `ReadFromJsonAsync<T>()`
- Or add a `JsonSerializerContext` for source-generated serialization
- Or add trimmer root descriptors in a `TrimmerRoots.xml`

### 5.3 Runtime auth state caching

`ServerAuthenticationStateProvider` already caches `_cachedUser` — keep this.

---

## ⚠️ Risk Notes

1. **`TrimMode=full` on .NET 10 prerelease** — May remove types needed by `System.Text.Json` deserialization. Add trimmer directives if issues arise.

2. **`AppState` Singleton + `PersistentComponentState` imperative API** — `RegisterPersistentService<T>` only supports scoped services. Since `AppState` is Singleton (per Microsoft's client-side state guidance), we use the imperative `PersistentComponentState.TryTakeFromJson()` / `PersistAsJson()` API directly. The root component must inject `PersistentComponentState` and call `AppState.TryRestoreState()` in `OnInitialized` and `RegisterOnPersisting(() => AppState.PersistStateAsync(state))` at the end of initialization.

3. **Auth serialization bridging** — The `ServerAuthenticationStateProvider` + `AddAuthenticationStateSerialization()` gap is the highest-risk part of prerendering. Test thoroughly with: fresh browser, expired cookie, valid cookie, no cookie.

4. **`Aero.Cms.Contracts` project creation** — Creating a new project and moving types will cause widespread `using` namespace changes. Use IDE refactoring tools. Affects: `Aero.Cms.Shared` (30+ files), `Aero.Cms.Web.Client`, `Aero.Cms.Web`.

5. **`LazyAssemblyLoader` requires `.csproj` entries** — Not `AddLazyAssemblyLoader`. The correct pattern is `<BlazorWebAssemblyLazyLoad>` items in the client `.csproj`.

6. **Per-site permissions bypass ASP.NET Core authorization policies** — Standard `[Authorize(Policy="...")]` is user-scoped, not user+site-scoped. Permission gating uses `AppState.HasPermission()` which knows the current site context. API enforcement must use a custom middleware that reads the site cookie + perm claims, not standard policy-based auth. Don't rely on `AuthorizeView Policy="..."` for site-scoped permissions.
