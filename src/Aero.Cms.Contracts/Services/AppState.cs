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

        /// <summary>
    /// Initializes a new instance of the <see cref="AppState"/> class.
    /// </summary>
public AppState(ILogger<AppState> logger)
    {
        _logger = logger;
    }

    // ── Site Context ──────────────────────────────────────────────

    /// <summary>Currently selected site ID, or null if none selected.</summary>
    public long? CurrentSiteId { get; private set; }

    /// <summary>Currently selected site name, or null if none selected.</summary>
    public string? CurrentSiteName { get; private set; }

    /// <summary>Tenant ID for the currently selected site. Never zero once a site is set.</summary>
    public long CurrentTenantId { get; private set; }

    // ── User / Auth ───────────────────────────────────────────────

    /// <summary>Authenticated user's ID, or null if not authenticated.</summary>
    public long? UserId { get; private set; }

    /// <summary>Authenticated user's username.</summary>
    public string? UserName { get; private set; }

    /// <summary>Authenticated user's email address.</summary>
    public string? UserEmail { get; private set; }

    /// <summary>Authenticated user's display name (FirstName + LastName), falls back to UserName.</summary>
    public string? UserNickname { get; private set; }

    /// <summary>Authenticated user's role assignments.</summary>
    public IReadOnlyList<string> Roles { get; private set; } = [];

    /// <summary>True if the authenticated user has the Admin role.</summary>
    public bool IsAdmin { get; private set; }

    /// <summary>True if user authentication has been resolved (whether authenticated or not).</summary>
    public bool IsAuthResolved { get; private set; }

    /// <summary>True when the user is authenticated and the session is valid.</summary>
    public bool IsAuthenticated => UserId.HasValue && UserId.Value > 0;

    // ── Lifecycle ─────────────────────────────────────────────────

    /// <summary>
    /// True after site context has been resolved (whether or not a site was found).
    /// The manager shell layout uses this to gate the sidebar.
    /// </summary>
    public bool IsSiteContextReady { get; private set; }

    // ── Permissions ───────────────────────────────────────────────

    // Key: "{siteId}:{domain}", Value: permission string ("R", "RW", "CRUD", etc.)
    private Dictionary<string, string> _permissions = new();

    /// <summary>
    /// Loads per-site permissions from claim values obtained from the
    /// ClaimsPrincipal after auth resolves.
    /// Claim format: "123|content|CRUD" (siteId|domain|value)
    /// Called by ManagerShellLayout once per session.
    /// </summary>
    public void LoadPermissions(IReadOnlyList<string> permissionClaimValues)
    {
        _permissions.Clear();
        foreach (var value in permissionClaimValues)
        {
            var parts = value.Split('|');
            if (parts.Length == 3)
                _permissions[$"{parts[0]}:{parts[1]}"] = parts[2];
        }

        _logger.LogDebug("AppState: Loaded {Count} permissions", _permissions.Count);
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Checks if the current user has the specified operation on the given domain
    /// for the CURRENTLY SELECTED SITE (CurrentSiteId).
    /// </summary>
    public bool HasPermission(string domain, char operation)
    {
        if (CurrentSiteId is null) return false;
        if (IsAdmin) return true; // Admin inherits all perms

        var key = $"{CurrentSiteId}:{domain}";
        return _permissions.TryGetValue(key, out var perm) && perm.Contains(operation);
    }

        /// <summary>
    /// CanRead method.
    /// </summary>
public bool CanRead(string domain) => HasPermission(domain, 'R');
        /// <summary>
    /// CanWrite method.
    /// </summary>
public bool CanWrite(string domain) => HasPermission(domain, 'W') || HasPermission(domain, 'C');
        /// <summary>
    /// CanDelete method.
    /// </summary>
public bool CanDelete(string domain) => HasPermission(domain, 'D');

    // ── Notification ──────────────────────────────────────────────

    /// <summary>
    /// Fired whenever state changes. Components subscribe via
    /// <c>StateChanged += StateHasChanged</c> and unsubscribe in <c>Dispose()</c>.
    /// </summary>
    public event Action? StateChanged;

    // ── Setters ───────────────────────────────────────────────────

    /// <summary>
    /// Sets the authenticated user info. Updates in-memory state and fires StateChanged.
    /// Called by ManagerShellLayout after auth resolves via ServerAuthenticationStateProvider.
    /// </summary>
    public void SetUser(
        long userId,
        string userName,
        string? userEmail,
        string? userNickname,
        IReadOnlyList<string> roles,
        bool isAdmin)
    {
        UserId = userId;
        UserName = userName;
        UserEmail = userEmail;
        UserNickname = userNickname;
        Roles = roles;
        IsAdmin = isAdmin;
        IsAuthResolved = true;

        _logger.LogInformation(
            "AppState: User set — {UserId} ({UserNickname})",
            userId, userNickname ?? userName);

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Clears the authenticated user. Used on logout or auth failure.
    /// </summary>
    public void ClearUser()
    {
        UserId = null;
        UserName = null;
        UserEmail = null;
        UserNickname = null;
        Roles = [];
        IsAdmin = false;
        IsAuthResolved = true;
        _permissions.Clear();

        _logger.LogInformation("AppState: User cleared");

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Sets the current site. Updates in-memory state and fires StateChanged.
    /// Does NOT persist to localStorage or server cookie — callers must handle that.
    /// </summary>
    public void SetSite(long siteId, string? siteName, long tenantId)
    {
        ArgumentOutOfRangeException.ThrowIfZero(tenantId, nameof(tenantId));

        if (CurrentSiteId == siteId && CurrentSiteName == siteName && CurrentTenantId == tenantId)
            return;

        var oldId = CurrentSiteId;
        CurrentSiteId = siteId;
        CurrentSiteName = siteName;
        CurrentTenantId = tenantId;
        IsSiteContextReady = true;

        _logger.LogInformation(
            "AppState: Site changed {OldId} → {NewId} ({SiteName}) tenant {TenantId}",
            oldId, siteId, siteName, tenantId);

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Sets IsSiteContextReady without modifying site identity.
    /// Used by login and select-site pages to skip verification.
    /// </summary>
    public void SetSiteContextReady() => IsSiteContextReady = true;

    // ── Persistence (for prerendering) ────────────────────────────

    /// <summary>
    /// Restores state deserialized from prerendered HTML.
    /// Called by the root component's OnInitialized via
    /// PersistentComponentState.TryTakeFromJson.
    /// </summary>
    public void SetSiteFromRestoredState(
        long? siteId,
        string? siteName,
        long tenantId,
        bool isSiteContextReady)
    {
        CurrentSiteId = siteId;
        CurrentSiteName = siteName;
        CurrentTenantId = tenantId;
        IsSiteContextReady = isSiteContextReady;

        _logger.LogDebug(
            "AppState: State restored from prerendering — site {SiteId} ({SiteName}) tenant {TenantId}",
            siteId, siteName, tenantId);

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Returns state to be persisted during prerendering.
    /// Caller (root component) uses PersistentComponentState.PersistAsJson
    /// in a RegisterOnPersisting callback.
    /// </summary>
    public (long? SiteId, string? SiteName, long TenantId, bool IsReady) GetStateForPersistence()
        => (CurrentSiteId, CurrentSiteName, CurrentTenantId, IsSiteContextReady);
}
