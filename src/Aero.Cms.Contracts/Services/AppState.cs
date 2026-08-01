using Microsoft.Extensions.Logging;

namespace Aero.Cms.Contracts.Services;

/// <summary>
/// Holds mutable in-memory state for a Blazor manager service scope.
/// </summary>
/// <remarks>
/// Lifetime is determined by the host registration; server bootstrap registers this type as
/// scoped. Consumers must not assume a process-wide singleton. Components that need reactive
/// rendering may subscribe to <see cref="StateChanged"/> and should unsubscribe when disposed.
/// The type does not synchronize concurrent access.
/// </remarks>
public sealed class AppState
{
    private readonly ILogger<AppState> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppState"/> class.
    /// </summary>
    /// <param name="logger">The logger used to record state transitions.</param>
    public AppState(ILogger<AppState> logger)
    {
        _logger = logger;
    }

    // ── Site Context ──────────────────────────────────────────────

    /// <summary>Gets the currently selected site identifier, or <see langword="null"/> when none is selected.</summary>
    public long? CurrentSiteId { get; private set; }

    /// <summary>Gets the currently selected site name, or <see langword="null"/> when none is selected.</summary>
    public string? CurrentSiteName { get; private set; }

    /// <summary>
    /// Gets the tenant identifier supplied by <see cref="SetSite"/> or restored state; the
    /// default value is zero.
    /// </summary>
    public long CurrentTenantId { get; private set; }

    // ── User / Auth ───────────────────────────────────────────────

    /// <summary>Gets the authenticated user's identifier, or <see langword="null"/> when no user is authenticated.</summary>
    public long? UserId { get; private set; }

    /// <summary>Gets the authenticated user's username.</summary>
    public string? UserName { get; private set; }

    /// <summary>Gets the authenticated user's email address.</summary>
    public string? UserEmail { get; private set; }

    /// <summary>Gets the display name supplied by <see cref="SetUser"/>, if any.</summary>
    public string? UserNickname { get; private set; }

    /// <summary>Gets the role values supplied by <see cref="SetUser"/>.</summary>
    public IReadOnlyList<string> Roles { get; private set; } = [];

    /// <summary>Gets the administrator flag supplied by <see cref="SetUser"/>.</summary>
    public bool IsAdmin { get; private set; }

    /// <summary>Gets a value indicating whether authentication resolution has completed, regardless of its outcome.</summary>
    public bool IsAuthResolved { get; private set; }

    /// <summary>Gets a value indicating whether a positive user identifier is currently available.</summary>
    public bool IsAuthenticated => UserId.HasValue && UserId.Value > 0;

    // ── Lifecycle ─────────────────────────────────────────────────

    /// <summary>
    /// Gets the caller-controlled site-context readiness flag.
    /// </summary>
    public bool IsSiteContextReady { get; private set; }

    // ── Permissions ───────────────────────────────────────────────

    // Key: "{siteId}:{domain}", Value: permission string ("R", "RW", "CRUD", etc.)
    private Dictionary<string, string> _permissions = new();

    /// <summary>
    /// Replaces the loaded permission map using claim values in the
    /// <c>siteId|domain|operations</c> format, then raises <see cref="StateChanged"/>.
    /// </summary>
    /// <param name="permissionClaimValues">
    /// The claim values to parse. Only values containing exactly three pipe-delimited segments
    /// are loaded; later entries replace earlier entries with the same site/domain key.
    /// </param>
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
    /// Determines whether the current user has an operation permission for a domain on the selected site.
    /// </summary>
    /// <param name="domain">The permission domain to check.</param>
    /// <param name="operation">The single-character operation to require.</param>
    /// <returns>
    /// <see langword="true"/> when a site is selected and either the administrator flag is set
    /// or the selected site's case-sensitive permission string contains
    /// <paramref name="operation"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool HasPermission(string domain, char operation)
    {
        if (CurrentSiteId is null) return false;
        if (IsAdmin) return true; // A selected site lets administrators bypass the permission map.

        var key = $"{CurrentSiteId}:{domain}";
        return _permissions.TryGetValue(key, out var perm) && perm.Contains(operation);
    }

    /// <summary>Determines whether the current user can read within a domain on the selected site.</summary>
    /// <param name="domain">The permission domain to check.</param>
    /// <returns><see langword="true"/> when the read operation is permitted; otherwise, <see langword="false"/>.</returns>
    public bool CanRead(string domain) => HasPermission(domain, 'R');

    /// <summary>Determines whether the current user can write or create within a domain on the selected site.</summary>
    /// <param name="domain">The permission domain to check.</param>
    /// <returns><see langword="true"/> when write or create is permitted; otherwise, <see langword="false"/>.</returns>
    public bool CanWrite(string domain) => HasPermission(domain, 'W') || HasPermission(domain, 'C');

    /// <summary>Determines whether the current user can delete within a domain on the selected site.</summary>
    /// <param name="domain">The permission domain to check.</param>
    /// <returns><see langword="true"/> when delete is permitted; otherwise, <see langword="false"/>.</returns>
    public bool CanDelete(string domain) => HasPermission(domain, 'D');

    // ── Notification ──────────────────────────────────────────────

    /// <summary>
    /// Raised synchronously by permission loading, user set/clear, changed site selection, and
    /// restored-state updates.
    /// </summary>
    /// <remarks>
    /// <see cref="SetSiteContextReady"/> does not raise this event. Exceptions thrown by
    /// subscribers propagate to the mutating method.
    /// </remarks>
    public event Action? StateChanged;

    // ── Setters ───────────────────────────────────────────────────

    /// <summary>
    /// Replaces the supplied user state, marks authentication as resolved, and raises
    /// <see cref="StateChanged"/>.
    /// </summary>
    /// <param name="userId">The authenticated user's identifier.</param>
    /// <param name="userName">The authenticated user's username.</param>
    /// <param name="userEmail">The authenticated user's email address, if available.</param>
    /// <param name="userNickname">The authenticated user's display name, if available.</param>
    /// <param name="roles">The authenticated user's assigned roles.</param>
    /// <param name="isAdmin">The administrator flag to store.</param>
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
    /// Clears user and permission state, marks authentication as resolved, and raises <see cref="StateChanged"/>.
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
    /// Updates the in-memory site context and raises <see cref="StateChanged"/> when the values differ.
    /// This method does not persist the selection.
    /// </summary>
    /// <param name="siteId">The selected site's identifier.</param>
    /// <param name="siteName">The selected site's display name, if available.</param>
    /// <param name="tenantId">The selected site's tenant identifier. It must not be zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tenantId"/> is zero.</exception>
    /// <remarks>
    /// When all three identity values already match, the method returns without changing
    /// <see cref="IsSiteContextReady"/> or raising <see cref="StateChanged"/>.
    /// </remarks>
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
    /// Marks site-context resolution complete without changing the selected site or raising
    /// <see cref="StateChanged"/>.
    /// </summary>
    public void SetSiteContextReady() => IsSiteContextReady = true;

    // ── Persistence (for prerendering) ────────────────────────────

    /// <summary>
    /// Restores site context obtained from prerendered state and raises <see cref="StateChanged"/>.
    /// </summary>
    /// <param name="siteId">The restored site identifier, if one was selected.</param>
    /// <param name="siteName">The restored site name, if available.</param>
    /// <param name="tenantId">The restored tenant identifier.</param>
    /// <param name="isSiteContextReady">Whether site-context resolution had completed.</param>
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
    /// Returns the site-context snapshot used to persist state during prerendering.
    /// </summary>
    /// <returns>A tuple containing the current site identity, tenant identifier, and readiness state.</returns>
    public (long? SiteId, string? SiteName, long TenantId, bool IsReady) GetStateForPersistence()
        => (CurrentSiteId, CurrentSiteName, CurrentTenantId, IsSiteContextReady);
}
