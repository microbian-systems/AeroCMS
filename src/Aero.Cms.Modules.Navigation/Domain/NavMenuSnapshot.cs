using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Navigation.Domain;

/// <summary>
/// Represents a record for NavMenuSnapshot.
/// </summary>
public sealed record NavMenuSnapshot
{
        /// <summary>
    /// Gets or sets the Layout.
    /// </summary>
public NavMenuLayout Layout { get; init; } = NavMenuLayout.Default;
        /// <summary>
    /// Gets or sets the Responsive.
    /// </summary>
public NavMenuResponsiveSettings Responsive { get; init; } = NavMenuResponsiveSettings.Default;
        /// <summary>
    /// Gets or sets the Style.
    /// </summary>
public NavMenuStyleSettings Style { get; init; } = NavMenuStyleSettings.Default;
        /// <summary>
    /// Gets or sets the Site Logo Url.
    /// </summary>
public string? SiteLogoUrl { get; init; }
        /// <summary>
    /// Gets or sets the Rows.
    /// </summary>
public List<NavCanvasRow> Rows { get; init; } = [];
        /// <summary>
    /// Gets or sets the Left.
    /// </summary>
public List<INavMenuComponent> Left { get; init; } = [];
        /// <summary>
    /// Gets or sets the Center.
    /// </summary>
public List<INavMenuComponent> Center { get; init; } = [];
        /// <summary>
    /// Gets or sets the Right.
    /// </summary>
public List<INavMenuComponent> Right { get; init; } = [];

        /// <summary>
    /// Gets or sets the Empty.
    /// </summary>
public static NavMenuSnapshot Empty { get; } = new();

        /// <summary>
    /// Initializes a new instance of the <see cref="NavMenuSnapshot"/> class.
    /// </summary>
public NavMenuSnapshot()
    {
    }

        /// <summary>
    /// Initializes a new instance of the <see cref="NavMenuSnapshot"/> class.
    /// </summary>
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
    /// Gets or sets the Components.
    /// </summary>
[JsonIgnore]
    public IEnumerable<INavMenuComponent> Components => Rows.Count > 0
        ? Rows.OrderBy(row => row.Order)
            .SelectMany(row => row.Columns.OrderBy(column => column.Order))
            .SelectMany(column => column.Blocks.OrderBy(block => block.Order).Select(block => block.Component))
        : Left.Concat(Center).Concat(Right);

        /// <summary>
    /// Validate method.
    /// </summary>
public void Validate()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in Components)
        {
            ValidateComponent(component, keys);
        }
    }

    private List<INavMenuComponent> BucketFor(NavAlignment alignment)
        => alignment switch
        {
            NavAlignment.Center => Center,
            NavAlignment.Right => Right,
            _ => Left
        };

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

    private static void ValidateRelativeUrl(string href, string message)
    {
        var trimmed = href.Trim();
        if (trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void ValidateLinkTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || target is "_self" or "_blank" or "_parent" or "_top")
        {
            return;
        }

        throw new InvalidOperationException("Navigation link target must be _self, _blank, _parent, or _top.");
    }

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
/// Represents a record for NavCanvasRow.
/// </summary>
public sealed record NavCanvasRow
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string? Label { get; init; }
        /// <summary>
    /// Gets or sets the Desktop Display.
    /// </summary>
public string DesktopDisplay { get; init; } = "Flex";
        /// <summary>
    /// Gets or sets the Tablet Display.
    /// </summary>
public string TabletDisplay { get; init; } = "Flex";
        /// <summary>
    /// Gets or sets the Mobile Display.
    /// </summary>
public string MobileDisplay { get; init; } = "Stack";
        /// <summary>
    /// Gets or sets the Columns.
    /// </summary>
public List<NavCanvasColumn> Columns { get; init; } = [];
}

/// <summary>
/// Represents a record for NavCanvasColumn.
/// </summary>
public sealed record NavCanvasColumn
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Desktop Span.
    /// </summary>
public int DesktopSpan { get; init; } = 4;
        /// <summary>
    /// Gets or sets the Tablet Span.
    /// </summary>
public int TabletSpan { get; init; } = 6;
        /// <summary>
    /// Gets or sets the Mobile Span.
    /// </summary>
public int MobileSpan { get; init; } = 12;
        /// <summary>
    /// Gets or sets the Blocks.
    /// </summary>
public List<NavCanvasBlock> Blocks { get; init; } = [];
}

/// <summary>
/// Represents a record for NavCanvasBlock.
/// </summary>
public sealed record NavCanvasBlock
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
public int Order { get; init; }
        /// <summary>
    /// Gets or sets the Component.
    /// </summary>
public INavMenuComponent Component { get; init; } = new NavLink();
}

/// <summary>
/// Defines an interface for INavMenuComponent.
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
    /// Gets or sets the Key.
    /// </summary>
string Key { get; }
        /// <summary>
    /// Gets or sets the Alignment.
    /// </summary>
NavAlignment Alignment { get; }
        /// <summary>
    /// Gets or sets the Visibility.
    /// </summary>
NavAuthVisibility Visibility { get; }
}

/// <summary>
/// Defines an enumeration for NavAlignment.
/// </summary>
public enum NavAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Represents a record for NavLink.
/// </summary>
public sealed record NavLink : INavMenuComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alignment.
    /// </summary>
public NavAlignment Alignment { get; init; }
        /// <summary>
    /// Gets or sets the Visibility.
    /// </summary>
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string Label { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Href.
    /// </summary>
public string Href { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Open In New Tab.
    /// </summary>
public bool OpenInNewTab { get; init; }
        /// <summary>
    /// Gets or sets the Is External.
    /// </summary>
public bool IsExternal { get; init; }
        /// <summary>
    /// Gets or sets the Target.
    /// </summary>
public string? Target { get; init; }
        /// <summary>
    /// Gets or sets the Page Id.
    /// </summary>
public long? PageId { get; init; }
        /// <summary>
    /// Gets or sets the Alt Text.
    /// </summary>
public string? AltText { get; init; }
}

/// <summary>
/// Represents a record for NavMenu.
/// </summary>
public sealed record NavMenu : INavMenuComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alignment.
    /// </summary>
public NavAlignment Alignment { get; init; }
        /// <summary>
    /// Gets or sets the Visibility.
    /// </summary>
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string Label { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Children.
    /// </summary>
public List<INavMenuComponent> Children { get; init; } = [];
}

/// <summary>
/// Represents a record for NavHtml.
/// </summary>
public sealed record NavHtml : INavMenuComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alignment.
    /// </summary>
public NavAlignment Alignment { get; init; }
        /// <summary>
    /// Gets or sets the Visibility.
    /// </summary>
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
        /// <summary>
    /// Gets or sets the Html.
    /// </summary>
public string Html { get; init; } = string.Empty;
}

/// <summary>
/// Represents a record for NavSearch.
/// </summary>
public sealed record NavSearch : INavMenuComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alignment.
    /// </summary>
public NavAlignment Alignment { get; init; }
        /// <summary>
    /// Gets or sets the Visibility.
    /// </summary>
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
        /// <summary>
    /// Gets or sets the Placeholder.
    /// </summary>
public string Placeholder { get; init; } = "Search...";
        /// <summary>
    /// Gets or sets the Search Action.
    /// </summary>
public string SearchAction { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Button Label.
    /// </summary>
public string ButtonLabel { get; init; } = "Search";
}

/// <summary>
/// Represents a record for NavLanguageSelect.
/// </summary>
public sealed record NavLanguageSelect : INavMenuComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alignment.
    /// </summary>
public NavAlignment Alignment { get; init; } = NavAlignment.Right;
        /// <summary>
    /// Gets or sets the Visibility.
    /// </summary>
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string Label { get; init; } = "Language";
}

/// <summary>
/// Represents a record for NavAuthButton.
/// </summary>
public sealed record NavAuthButton : INavMenuComponent
{
        /// <summary>
    /// Gets or sets the Key.
    /// </summary>
public string Key { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Alignment.
    /// </summary>
public NavAlignment Alignment { get; init; } = NavAlignment.Right;
        /// <summary>
    /// Gets or sets the Visibility.
    /// </summary>
public NavAuthVisibility Visibility { get; init; } = NavAuthVisibility.Always;
        /// <summary>
    /// Gets or sets the Label.
    /// </summary>
public string Label { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Href.
    /// </summary>
public string Href { get; init; } = string.Empty;
        /// <summary>
    /// Gets or sets the Button Style.
    /// </summary>
public string ButtonStyle { get; init; } = "Primary";
}

/// <summary>
/// Defines an enumeration for NavAuthVisibility.
/// </summary>
public enum NavAuthVisibility
{
    Always,
    AnonymousOnly,
    AuthenticatedOnly
}

/// <summary>
/// Represents a record for NavMenuLayout.
/// </summary>
public sealed record NavMenuLayout(IReadOnlyList<NavLayoutSlot> Slots)
{
        /// <summary>
    /// Gets or sets the Default.
    /// </summary>
public static NavMenuLayout Default { get; } = new(
        [
            new NavLayoutSlot(NavLayoutSlots.Left, "Left", 0),
            new NavLayoutSlot(NavLayoutSlots.Center, "Center", 1),
            new NavLayoutSlot(NavLayoutSlots.Right, "Right", 2)
        ]);
}

/// <summary>
/// Represents a record for NavLayoutSlot.
/// </summary>
public sealed record NavLayoutSlot(string Key, string Label, int Order);

/// <summary>
/// Represents a class for NavLayoutSlots.
/// </summary>
public static class NavLayoutSlots
{
        /// <summary>
    /// Left.
    /// </summary>
public const string Left = "left";
        /// <summary>
    /// Center.
    /// </summary>
public const string Center = "center";
        /// <summary>
    /// Right.
    /// </summary>
public const string Right = "right";

        /// <summary>
    /// ToAlignment method.
    /// </summary>
public static NavAlignment ToAlignment(string? slotKey)
        => slotKey?.ToLowerInvariant() switch
        {
            Center => NavAlignment.Center,
            Right => NavAlignment.Right,
            _ => NavAlignment.Left
        };
}

/// <summary>
/// Represents a record for NavMenuResponsiveSettings.
/// </summary>
public sealed record NavMenuResponsiveSettings(string MobileBreakpoint)
{
        /// <summary>
    /// Gets or sets the Default.
    /// </summary>
public static NavMenuResponsiveSettings Default { get; } = new("md");
}

/// <summary>
/// Represents a record for NavMenuStyleSettings.
/// </summary>
public sealed record NavMenuStyleSettings(bool IsSticky)
{
        /// <summary>
    /// Gets or sets the Default.
    /// </summary>
public static NavMenuStyleSettings Default { get; } = new(false);
}

/// <summary>
/// Represents a record for NavItemVisibility.
/// </summary>
public sealed record NavItemVisibility(
    bool HideOnMobile,
    bool HideOnDesktop,
    IReadOnlyList<string> AllowedRoles,
    RoleVisibilityMode RoleMode)
{
        /// <summary>
    /// Gets or sets the Default.
    /// </summary>
public static NavItemVisibility Default { get; } = new(false, false, [], RoleVisibilityMode.Any);
        /// <summary>
    /// Gets or sets the Has Role Rules.
    /// </summary>
public bool HasRoleRules => AllowedRoles.Count > 0;
}

/// <summary>
/// Defines an enumeration for NavLinkTarget.
/// </summary>
public enum NavLinkTarget
{
    InternalUrl,
    InternalPage,
    ExternalUrl
}

/// <summary>
/// Defines an enumeration for SearchDisplayMode.
/// </summary>
public enum SearchDisplayMode
{
    IconPopup,
    InlineTextbox
}

/// <summary>
/// Defines an enumeration for SearchInputStyle.
/// </summary>
public enum SearchInputStyle
{
    Rounded,
    Square
}

/// <summary>
/// Defines an enumeration for RoleVisibilityMode.
/// </summary>
public enum RoleVisibilityMode
{
    Any,
    All
}

/// <summary>
/// Defines an enumeration for NavMenuLifecycleState.
/// </summary>
public enum NavMenuLifecycleState
{
    Draft,
    Published,
    PublishedWithDraft,
    Archived
}
