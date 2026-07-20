using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Navigation.Domain;

/// <summary>
/// Captures a complete editable or published navigation layout and its polymorphic components.
/// </summary>
/// <remarks>
/// New snapshots can use the row/column/block canvas. The left, center, and right collections
/// remain as a legacy flat representation; <see cref="Components"/> prefers rows whenever any
/// row exists.
/// </remarks>
public sealed record NavMenuSnapshot
{
    /// <summary>
    /// Gets the named legacy layout slots.
    /// </summary>
public NavMenuLayout Layout { get; init; } = NavMenuLayout.Default;
    /// <summary>
    /// Gets the responsive breakpoint settings.
    /// </summary>
public NavMenuResponsiveSettings Responsive { get; init; } = NavMenuResponsiveSettings.Default;
    /// <summary>
    /// Gets the menu-wide style settings.
    /// </summary>
public NavMenuStyleSettings Style { get; init; } = NavMenuStyleSettings.Default;
    /// <summary>
    /// Gets the optional site logo URL rendered with the menu.
    /// </summary>
public string? SiteLogoUrl { get; init; }
    /// <summary>
    /// Gets the row-based editor canvas.
    /// </summary>
public List<NavCanvasRow> Rows { get; init; } = [];
    /// <summary>
    /// Gets legacy left-aligned components.
    /// </summary>
public List<INavMenuComponent> Left { get; init; } = [];
    /// <summary>
    /// Gets legacy center-aligned components.
    /// </summary>
public List<INavMenuComponent> Center { get; init; } = [];
    /// <summary>
    /// Gets legacy right-aligned components.
    /// </summary>
public List<INavMenuComponent> Right { get; init; } = [];

    /// <summary>
    /// Gets a shared empty snapshot.
    /// </summary>
    /// <remarks>
    /// The record contains mutable lists. Consumers must treat this shared instance as read-only.
    /// </remarks>
public static NavMenuSnapshot Empty { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NavMenuSnapshot"/> class.
    /// </summary>
public NavMenuSnapshot()
    {
    }

    /// <summary>
    /// Creates a snapshot from a flat component list and populates both canvas and alignment buckets.
    /// </summary>
    /// <param name="layout">The legacy slot layout metadata.</param>
    /// <param name="responsive">The responsive settings.</param>
    /// <param name="style">The menu-wide style settings.</param>
    /// <param name="components">The components to group by alignment while preserving their input order.</param>
    /// <param name="siteLogoUrl">The optional logo URL, trimmed or stored as <see langword="null"/> when blank.</param>
public NavMenuSnapshot(
        NavMenuLayout layout,
        NavMenuResponsiveSettings responsive,
        NavMenuStyleSettings style,
        IReadOnlyList<INavMenuComponent> components,
        string? siteLogoUrl = null)
    {
        Layout = layout;
        Responsive = responsive;
        Style = style;
        SiteLogoUrl = string.IsNullOrWhiteSpace(siteLogoUrl) ? null : siteLogoUrl.Trim();
        Rows = BuildRowsFromComponents(components);

        foreach (var component in components)
        {
            BucketFor(component.Alignment).Add(component);
        }
    }

    /// <summary>
    /// Enumerates the effective components in visual row, column, and block order.
    /// </summary>
    /// <remarks>
    /// If <see cref="Rows"/> is non-empty, legacy alignment buckets are ignored.
    /// </remarks>
[JsonIgnore]
    public IEnumerable<INavMenuComponent> Components => Rows.Count > 0
        ? Rows.OrderBy(row => row.Order)
            .SelectMany(row => row.Columns.OrderBy(column => column.Order))
            .SelectMany(column => column.Blocks.OrderBy(block => block.Order).Select(block => block.Component))
        : Left.Concat(Center).Concat(Right);

    /// <summary>
    /// Validates component keys and the supported URL, target, and required-content rules.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A key is missing or duplicated, a required component value is blank, or a URL or target
    /// violates the navigation safety rules.
    /// </exception>
public void Validate()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in Components)
        {
            ValidateComponent(component, keys);
        }
    }

    /// <summary>
    /// Selects the legacy component bucket for an alignment.
    /// </summary>
    /// <param name="alignment">The component alignment.</param>
    /// <returns>The mutable bucket owned by this snapshot.</returns>
    private List<INavMenuComponent> BucketFor(NavAlignment alignment)
        => alignment switch
        {
            NavAlignment.Center => Center,
            NavAlignment.Right => Right,
            _ => Left
        };

    /// <summary>
    /// Validates one component and recursively validates menu children against a global key set.
    /// </summary>
    /// <param name="component">The component to validate.</param>
    /// <param name="keys">The case-insensitive key set shared by the entire snapshot.</param>
    /// <exception cref="InvalidOperationException">A component constraint is violated.</exception>
    private static void ValidateComponent(INavMenuComponent component, HashSet<string> keys)
    {
        if (string.IsNullOrWhiteSpace(component.Key))
        {
            throw new InvalidOperationException("Navigation component key is required.");
        }

        if (!keys.Add(component.Key))
        {
            throw new InvalidOperationException($"Duplicate navigation component key '{component.Key}'.");
        }

        switch (component)
        {
            case NavLink link:
                if (string.IsNullOrWhiteSpace(link.Label))
                    throw new InvalidOperationException("Link label is required.");

                if (string.IsNullOrWhiteSpace(link.Href))
                    throw new InvalidOperationException("Link href is required.");

                ValidateLinkUrl(link);
                ValidateLinkTarget(link.Target);
                break;

            case NavMenu menu:
                if (string.IsNullOrWhiteSpace(menu.Label))
                    throw new InvalidOperationException("Menu label is required.");

                foreach (var child in menu.Children)
                {
                    ValidateComponent(child, keys);
                }
                break;

            case NavHtml html:
                if (string.IsNullOrWhiteSpace(html.Html))
                    throw new InvalidOperationException("HTML navigation component requires markup.");
                break;

            case NavSearch search:
                if (string.IsNullOrWhiteSpace(search.SearchAction))
                    throw new InvalidOperationException("Search navigation component requires an action route.");
                break;

            case NavAuthButton authButton:
                if (string.IsNullOrWhiteSpace(authButton.Label))
                    throw new InvalidOperationException("Auth button label is required.");

                if (string.IsNullOrWhiteSpace(authButton.Href))
                    throw new InvalidOperationException("Auth button href is required.");

                ValidateRelativeUrl(authButton.Href, "Auth button href must be a relative URL.");
                break;
        }
    }

    /// <summary>
    /// Enforces absolute HTTP(S) URLs for external links and root-relative URLs or page identifiers for internal links.
    /// </summary>
    /// <param name="link">The link to validate.</param>
    /// <exception cref="InvalidOperationException">The link target does not match its external/internal classification.</exception>
    private static void ValidateLinkUrl(NavLink link)
    {
        var href = link.Href.Trim();
        if (link.IsExternal)
        {
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return;
            }

            throw new InvalidOperationException("External navigation links must use an absolute http or https URL.");
        }

        if (href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal))
        {
            return;
        }

        if (link.PageId is > 0)
        {
            return;
        }

        throw new InvalidOperationException("Internal navigation links must use a relative URL or selected page.");
    }

    /// <summary>
    /// Requires an application-root-relative URL while rejecting protocol-relative values.
    /// </summary>
    /// <param name="href">The candidate URL.</param>
    /// <param name="message">The exception message used for invalid input.</param>
    /// <exception cref="InvalidOperationException"><paramref name="href"/> is not a safe root-relative URL.</exception>
    private static void ValidateRelativeUrl(string href, string message)
    {
        var trimmed = href.Trim();
        if (trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Restricts link targets to standard browsing-context keywords.
    /// </summary>
    /// <param name="target">The optional target value.</param>
    /// <exception cref="InvalidOperationException">A nonblank target is not one of the supported keywords.</exception>
    private static void ValidateLinkTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || target is "_self" or "_blank" or "_parent" or "_top")
        {
            return;
        }

        throw new InvalidOperationException("Navigation link target must be _self, _blank, _parent, or _top.");
    }

    /// <summary>
    /// Adapts flat aligned components into the default header row and three responsive columns.
    /// </summary>
    /// <param name="components">The components to group by alignment.</param>
    /// <returns>An empty list for no components; otherwise one header row.</returns>
    private static List<NavCanvasRow> BuildRowsFromComponents(IReadOnlyList<INavMenuComponent> components)
    {
        if (components.Count == 0)
        {
            return [];
        }

        return
        [
            new()
            {
                Key = "header-row",
                Order = 0,
                Label = "Header",
                Columns =
                [
                    new()
                    {
                        Key = NavLayoutSlots.Left,
                        Order = 0,
                        DesktopSpan = 4,
                        TabletSpan = 6,
                        MobileSpan = 12,
                        Blocks = components.Where(x => x.Alignment == NavAlignment.Left).Select((x, index) => new NavCanvasBlock { Key = x.Key, Order = index, Component = x }).ToList()
                    },
                    new()
                    {
                        Key = NavLayoutSlots.Center,
                        Order = 1,
                        DesktopSpan = 4,
                        TabletSpan = 6,
                        MobileSpan = 12,
                        Blocks = components.Where(x => x.Alignment == NavAlignment.Center).Select((x, index) => new NavCanvasBlock { Key = x.Key, Order = index, Component = x }).ToList()
                    },
                    new()
                    {
                        Key = NavLayoutSlots.Right,
                        Order = 2,
                        DesktopSpan = 4,
                        TabletSpan = 12,
                        MobileSpan = 12,
                        Blocks = components.Where(x => x.Alignment == NavAlignment.Right).Select((x, index) => new NavCanvasBlock { Key = x.Key, Order = index, Component = x }).ToList()
                    }
                ]
            }
        ];
    }
}

/// <summary>
/// Defines one ordered, device-responsive row in the navigation canvas.
/// </summary>
public sealed record NavCanvasRow
{
    /// <summary>
    /// Gets the stable editor key for the row.
    /// </summary>
public string Key { get; init; } = string.Empty;
    /// <summary>
    /// Gets the row's visual order.
    /// </summary>
public int Order { get; init; }
    /// <summary>
    /// Gets the optional editor-facing row label.
    /// </summary>
public string? Label { get; init; }
    /// <summary>
    /// Gets the desktop display-mode token interpreted by the navigation view.
    /// </summary>
public string DesktopDisplay { get; init; } = "Flex";
    /// <summary>
    /// Gets the tablet display-mode token interpreted by the navigation view.
    /// </summary>
public string TabletDisplay { get; init; } = "Flex";
    /// <summary>
    /// Gets the mobile display-mode token interpreted by the navigation view.
    /// </summary>
public string MobileDisplay { get; init; } = "Stack";
    /// <summary>
    /// Gets the row's columns.
    /// </summary>
public List<NavCanvasColumn> Columns { get; init; } = [];
}

/// <summary>
/// Defines one ordered responsive column in a navigation canvas row.
/// </summary>
public sealed record NavCanvasColumn
{
    /// <summary>
    /// Gets the stable editor key for the column.
    /// </summary>
public string Key { get; init; } = string.Empty;
    /// <summary>
    /// Gets the column's order within its row.
    /// </summary>
public int Order { get; init; }
    /// <summary>
    /// Gets the desktop span in the twelve-column grid.
    /// </summary>
public int DesktopSpan { get; init; } = 4;
    /// <summary>
    /// Gets the tablet span in the twelve-column grid.
    /// </summary>
public int TabletSpan { get; init; } = 6;
    /// <summary>
    /// Gets the mobile span in the twelve-column grid.
    /// </summary>
public int MobileSpan { get; init; } = 12;
    /// <summary>
    /// Gets the component blocks placed in this column.
    /// </summary>
public List<NavCanvasBlock> Blocks { get; init; } = [];
}

/// <summary>
/// Places a navigation component at an ordered position inside a canvas column.
/// </summary>
public sealed record NavCanvasBlock
{
    /// <summary>
    /// Gets the stable editor key for the block.
    /// </summary>
public string Key { get; init; } = string.Empty;
    /// <summary>
    /// Gets the block's order within its column.
    /// </summary>
public int Order { get; init; }
    /// <summary>
    /// Gets the polymorphic component rendered by this block.
    /// </summary>
public INavMenuComponent Component { get; init; } = new NavLink();
}

/// <summary>
/// Defines the shared identity, alignment, and authentication visibility of renderable navigation components.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NavLink), "link")]
[JsonDerivedType(typeof(NavMenu), "menu")]
[JsonDerivedType(typeof(NavHtml), "html")]
[JsonDerivedType(typeof(NavSearch), "search")]
[JsonDerivedType(typeof(NavLanguageSelect), "language")]
[JsonDerivedType(typeof(NavAuthButton), "authButton")]
public interface INavMenuComponent
{
    /// <summary>
    /// Gets the key unique across the complete snapshot, including nested menu children.
    /// </summary>
string Key { get; }
    /// <summary>
    /// Gets the legacy alignment bucket used by flat snapshots.
    /// </summary>
NavAlignment Alignment { get; }
    /// <summary>
    /// Gets the authentication state required for rendering.
    /// </summary>
NavAuthVisibility Visibility { get; }
}

/// <summary>
/// Identifies a component's legacy header alignment bucket.
/// </summary>
public enum NavAlignment
{
    /// <summary>Places the component in the left bucket.</summary>
    Left,
    /// <summary>Places the component in the center bucket.</summary>
    Center,
    /// <summary>Places the component in the right bucket.</summary>
    Right
}

/// <summary>
/// Renders a labeled internal page/route or external HTTP(S) navigation link.
/// </summary>
public sealed record NavLink : INavMenuComponent
{
    /// <inheritdoc />
public string Key { get; init; } = string.Empty;
    /// <inheritdoc />
public NavAlignment Alignment { get; init; }
    /// <inheritdoc />
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
    /// <summary>
    /// Gets the visible link label.
    /// </summary>
public string Label { get; init; } = string.Empty;
    /// <summary>
    /// Gets the rendered URL.
    /// </summary>
public string Href { get; init; } = string.Empty;
    /// <summary>
    /// Gets whether rendering falls back to a <c>_blank</c> target when <see cref="Target"/> is blank.
    /// </summary>
public bool OpenInNewTab { get; init; }
    /// <summary>
    /// Gets whether validation requires an absolute HTTP or HTTPS URL.
    /// </summary>
public bool IsExternal { get; init; }
    /// <summary>
    /// Gets the optional HTML browsing-context target keyword.
    /// </summary>
public string? Target { get; init; }
    /// <summary>
    /// Gets the optional internal page identifier associated with the link.
    /// </summary>
public long? PageId { get; init; }
    /// <summary>
    /// Gets optional descriptive text retained in editor contracts; the current HTML renderer does not emit it.
    /// </summary>
public string? AltText { get; init; }
}

/// <summary>
/// Renders a labeled dropdown containing nested navigation components.
/// </summary>
public sealed record NavMenu : INavMenuComponent
{
    /// <inheritdoc />
public string Key { get; init; } = string.Empty;
    /// <inheritdoc />
public NavAlignment Alignment { get; init; }
    /// <inheritdoc />
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
    /// <summary>
    /// Gets the dropdown trigger label.
    /// </summary>
public string Label { get; init; } = string.Empty;
    /// <summary>
    /// Gets the ordered child components; validation enforces key uniqueness recursively.
    /// </summary>
public List<INavMenuComponent> Children { get; init; } = [];
}

/// <summary>
/// Renders trusted custom markup inside a navigation slot.
/// </summary>
/// <remarks>
/// The renderer writes <see cref="Html"/> as raw HTML without sanitization. Only trusted
/// administrative input may populate this component.
/// </remarks>
public sealed record NavHtml : INavMenuComponent
{
    /// <inheritdoc />
public string Key { get; init; } = string.Empty;
    /// <inheritdoc />
public NavAlignment Alignment { get; init; }
    /// <inheritdoc />
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
    /// <summary>
    /// Gets the nonblank markup written verbatim by the HTML renderer.
    /// </summary>
public string Html { get; init; } = string.Empty;
}

/// <summary>
/// Renders a GET search form in the navigation surface.
/// </summary>
public sealed record NavSearch : INavMenuComponent
{
    /// <inheritdoc />
public string Key { get; init; } = string.Empty;
    /// <inheritdoc />
public NavAlignment Alignment { get; init; }
    /// <inheritdoc />
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
    /// <summary>
    /// Gets the encoded search-input placeholder.
    /// </summary>
public string Placeholder { get; init; } = "Search...";
    /// <summary>
    /// Gets the encoded form action; validation requires a nonblank value.
    /// </summary>
public string SearchAction { get; init; } = string.Empty;
    /// <summary>
    /// Gets the encoded submit-button label.
    /// </summary>
public string ButtonLabel { get; init; } = "Search";
}

/// <summary>
/// Renders links for switching among the current site's supported cultures.
/// </summary>
public sealed record NavLanguageSelect : INavMenuComponent
{
    /// <inheritdoc />
public string Key { get; init; } = string.Empty;
    /// <inheritdoc />
public NavAlignment Alignment { get; init; } = NavAlignment.Right;
    /// <inheritdoc />
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
    /// <summary>
    /// Gets the encoded control label and accessible navigation label.
    /// </summary>
public string Label { get; init; } = "Language";
}

/// <summary>
/// Renders an application-relative login, registration, or other authentication action.
/// </summary>
public sealed record NavAuthButton : INavMenuComponent
{
    /// <inheritdoc />
public string Key { get; init; } = string.Empty;
    /// <inheritdoc />
public NavAlignment Alignment { get; init; } = NavAlignment.Right;
    /// <inheritdoc />
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
    /// <summary>
    /// Gets the encoded button label.
    /// </summary>
public string Label { get; init; } = string.Empty;
    /// <summary>
    /// Gets the application-root-relative URL validated by <see cref="NavMenuSnapshot.Validate"/>.
    /// </summary>
public string Href { get; init; } = string.Empty;
    /// <summary>
    /// Gets the presentation token used by editor contracts; the current renderer does not branch on it.
    /// </summary>
public string ButtonStyle { get; init; } = "Primary";
}

/// <summary>
/// Controls whether a component is rendered for the current authentication state.
/// </summary>
public enum NavAuthVisibility
{
    /// <summary>Renders for anonymous and authenticated requests.</summary>
    Always,
    /// <summary>Renders only when no authenticated principal is present.</summary>
    AnonymousOnly,
    /// <summary>Renders only for an authenticated principal.</summary>
    AuthenticatedOnly
}

/// <summary>
/// Describes the ordered named slots used by legacy flat navigation layouts.
/// </summary>
/// <param name="Slots">The available layout slots.</param>
public sealed record NavMenuLayout(IReadOnlyList<NavLayoutSlot> Slots)
{
    /// <summary>
    /// Gets the left, center, and right slot layout.
    /// </summary>
public static NavMenuLayout Default { get; } = new(
        [
            new NavLayoutSlot(NavLayoutSlots.Left, "Left", 0),
            new NavLayoutSlot(NavLayoutSlots.Center, "Center", 1),
            new NavLayoutSlot(NavLayoutSlots.Right, "Right", 2)
        ]);
}

/// <summary>
/// Describes one named legacy navigation alignment slot.
/// </summary>
/// <param name="Key">The persisted slot key.</param>
/// <param name="Label">The editor-facing label.</param>
/// <param name="Order">The slot's visual order.</param>
public sealed record NavLayoutSlot(string Key, string Label, int Order);

/// <summary>
/// Defines stable persisted keys for the built-in legacy layout slots.
/// </summary>
public static class NavLayoutSlots
{
    /// <summary>
    /// Identifies the left slot.
    /// </summary>
public const string Left = "left";
    /// <summary>
    /// Identifies the center slot.
    /// </summary>
public const string Center = "center";
    /// <summary>
    /// Identifies the right slot.
    /// </summary>
public const string Right = "right";

    /// <summary>
    /// Maps a persisted slot key to a component alignment.
    /// </summary>
    /// <param name="slotKey">The case-insensitive slot key.</param>
    /// <returns>The center or right alignment when matched; otherwise left.</returns>
public static NavAlignment ToAlignment(string? slotKey)
        => slotKey?.ToLowerInvariant() switch
        {
            Center => NavAlignment.Center,
            Right => NavAlignment.Right,
            _ => NavAlignment.Left
        };
}

/// <summary>
/// Describes the CSS breakpoint at which the navigation switches to its mobile presentation.
/// </summary>
/// <param name="MobileBreakpoint">The presentation-layer breakpoint token.</param>
public sealed record NavMenuResponsiveSettings(string MobileBreakpoint)
{
    /// <summary>
    /// Gets settings using the <c>md</c> mobile breakpoint.
    /// </summary>
public static NavMenuResponsiveSettings Default { get; } = new("md");
}

/// <summary>
/// Describes menu-wide presentation flags.
/// </summary>
/// <param name="IsSticky">Whether the navigation should remain fixed during scrolling.</param>
public sealed record NavMenuStyleSettings(bool IsSticky)
{
    /// <summary>
    /// Gets the non-sticky default style.
    /// </summary>
public static NavMenuStyleSettings Default { get; } = new(false);
}

/// <summary>
/// Describes device and role-based visibility rules for a navigation item.
/// </summary>
/// <param name="HideOnMobile">Whether to hide the item in mobile presentation.</param>
/// <param name="HideOnDesktop">Whether to hide the item in desktop presentation.</param>
/// <param name="AllowedRoles">The role names used by <paramref name="RoleMode"/>.</param>
/// <param name="RoleMode">Whether any or all listed roles are required.</param>
public sealed record NavItemVisibility(
    bool HideOnMobile,
    bool HideOnDesktop,
    IReadOnlyList<string> AllowedRoles,
    RoleVisibilityMode RoleMode)
{
    /// <summary>
    /// Gets visibility with no device or role restrictions.
    /// </summary>
public static NavItemVisibility Default { get; } = new(false, false, [], RoleVisibilityMode.Any);
    /// <summary>
    /// Gets whether at least one allowed role is configured.
    /// </summary>
public bool HasRoleRules => AllowedRoles.Count > 0;
}

/// <summary>
/// Identifies the destination category selected by navigation editing tools.
/// </summary>
public enum NavLinkTarget
{
    /// <summary>Targets an application-relative URL.</summary>
    InternalUrl,
    /// <summary>Targets a CMS page identifier.</summary>
    InternalPage,
    /// <summary>Targets an absolute external URL.</summary>
    ExternalUrl
}

/// <summary>
/// Identifies the navigation search control presentation.
/// </summary>
public enum SearchDisplayMode
{
    /// <summary>Uses an icon that opens a search surface.</summary>
    IconPopup,
    /// <summary>Displays the search text box inline.</summary>
    InlineTextbox
}

/// <summary>
/// Identifies the search input's corner treatment.
/// </summary>
public enum SearchInputStyle
{
    /// <summary>Uses rounded corners.</summary>
    Rounded,
    /// <summary>Uses square corners.</summary>
    Square
}

/// <summary>
/// Controls how multiple allowed-role rules are combined.
/// </summary>
public enum RoleVisibilityMode
{
    /// <summary>Requires the user to have at least one listed role.</summary>
    Any,
    /// <summary>Requires the user to have every listed role.</summary>
    All
}

/// <summary>
/// Represents the event-projected editing and publication state of a navigation menu.
/// </summary>
public enum NavMenuLifecycleState
{
    /// <summary>The menu has an editable draft but has never been published.</summary>
    Draft,
    /// <summary>The current editor view matches the latest published snapshot.</summary>
    Published,
    /// <summary>A newer draft exists after the latest published snapshot.</summary>
    PublishedWithDraft,
    /// <summary>The menu is excluded from active listings and publication reads.</summary>
    Archived
}
