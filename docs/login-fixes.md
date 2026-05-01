# Manager Login Authentication Fixes

## Problem Summary

1. **Dashboard shows before Login**: Visiting `/manager` displays `Dashboard.razor` first, then redirects to `Login.razor` — causing a visual flash.
2. **Redirect loop after login**: Successful login navigates to `/manager`, but after a short delay, the page bounces back to `/manager/login`.

## Root Cause Analysis

### Issue 1: Dashboard renders before redirect

`ManagerShellLayout.razor` unconditionally redirects to `/manager/login` on every first render **without checking auth state**. The `Dashboard.razor` page also lacks any `[Authorize]` attribute, so it renders immediately — then the layout fires `Navigation.NavigateTo("/manager/login")` after `OnAfterRenderAsync`.

### Issue 2: Cookie never set during login

`Login.razor.cs` calls `AuthClient.LoginAsync()` which exchanges credentials for a JWT token stored in `InMemoryTokenProvider` (server memory). It **never calls the ASP.NET Core Identity cookie endpoint**. After `forceLoad: true` navigation to `/manager`:

1. Fresh HTTP request arrives at server
2. `UseAuthentication()` middleware checks `.AeroCms.Auth` cookie → **not found** (never set)
3. User appears unauthenticated
4. `ManagerShellLayout` fires its unconditional redirect → back to login

### Additional Gaps

- `Routes.razor` uses `RouteView` instead of `AuthorizeRouteView` — no router-level auth enforcement
- `Aero.Cms.Web.Client/Program.cs` missing `AddAuthenticationStateDeserialization()` — WASM client can't receive serialized auth state
- Prerendering (`InteractiveWebAssembly` with default `prerender: true`) causes auth state to be unavailable during initial render, triggering premature redirects

## Solution

All changes are in the `src/` folder of this project.

### Fix 1: Disable prerendering in App.razor
- **File**: `src/Aero.Cms.Web/Components/App.razor`
- **Change**: `new InteractiveWebAssemblyRenderMode(prerender: false)` on Routes
- **Why**: Prerendering runs before Blazor circuit is established. Auth state from `AuthenticationStateProvider` isn't available during prerender, causing `[Authorize]` checks to fail.

### Fix 2: Add auth state deserialization in Web.Client
- **File**: `src/Aero.Cms.Web.Client/Program.cs`
- **Change**: Add `AddAuthorizationCore()`, `AddCascadingAuthenticationState()`, `AddAuthenticationStateDeserialization()`
- **Why**: Server serializes auth state into HTML. Without deserialization, the WASM client can't read it back and always sees unauthenticated.

### Fix 3: Upgrade Routes.razor to AuthorizeRouteView
- **File**: `src/Aero.Cms.Shared/Components/Routes.razor`
- **Change**: Replace `RouteView` with `AuthorizeRouteView` + `<NotAuthorized>` + `<Authorizing>`
- **Why**: `AuthorizeRouteView` respects `[Authorize]` on pages — it shows `<NotAuthorized>` or `<Authorizing>` content. `RouteView` ignores auth entirely.

### Fix 4: Dual auth in Login.razor.cs
- **File**: `src/Aero.Cms.Shared/Pages/Manager/Login.razor.cs`
- **Change**: After JWT login succeeds, also POST to `/api/v1/admin/auth/local/login` (the Identity cookie endpoint)
- **Why**: This calls `SignInManager.PasswordSignInAsync()` server-side, which writes the `.AeroCms.Auth` cookie. On the next request, `UseAuthentication()` middleware sees the cookie and populates `HttpContext.User`.

### Fix 5: Auth check in ManagerShellLayout.razor
- **File**: `src/Aero.Cms.Shared/Layout/ManagerShellLayout.razor`
- **Change**: Check `AuthenticationState` cascading parameter before redirecting
- **Why**: Currently redirects unconditionally. After Fix 4, the cookie is present and `AuthenticationState` will show authenticated — so the layout should skip the redirect.

### Fix 6: Add [Authorize] to Dashboard.razor
- **File**: `src/Aero.Cms.Shared/Pages/Manager/Dashboard.razor`
- **Change**: Add `@attribute [Authorize]`
- **Why**: Defense-in-depth. Prevents rendering if user bypasses layout redirect.

### Fix 7: Add [Authorize] to all other manager pages
- **Files**: All `.razor` pages under `src/Aero.Cms.Shared/Pages/Manager/`
- **Change**: Add `@attribute [Authorize]` where missing
- **Why**: Consistent auth enforcement across all protected pages.

## Auth Flow After Fixes

```
User visits /manager
  → App.razor (no prerender)
  → Routes.razor (AuthorizeRouteView)
    → Is cookie present?
      → NO: <NotAuthorized> → redirect to /manager/login
      → YES: AuthenticationState.IsAuthenticated = true
        → Show Dashboard normally

User submits login form
  → POST /api/v1/admin/auth/local/login (sets .AeroCms.Auth cookie) ✅ NEW
  → POST /api/v1/auth/login (gets JWT token, stores in memory)
  → Navigate to /manager (forceLoad: true)
  
User arrives at /manager (after login)
  → Cookie present in HTTP request
  → UseAuthentication() middleware extracts identity
  → ManagerShellLayout checks AuthenticationState → authenticated → skip redirect ✅ FIXED
  → Dashboard renders normally with [Authorize] passing
```

## MAUI Hybrid Client Notes

For the MAUI Blazor Hybrid client, the dual-auth approach works as follows:
- The cookie endpoint call (`/api/v1/admin/auth/local/login`) won't persist a cookie (MAUI WebView has no cookie jar)
- The JWT token will still be obtained and stored in `InMemoryTokenProvider` for API calls
- Future MAUI auth will need a custom `AuthenticationStateProvider` backed by the stored JWT token
