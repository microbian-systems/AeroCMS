using System.Text.Json.Serialization;

namespace Aero.Cms.Modules.Navigation.Domain;

public sealed record NavMenuSnapshot
{
    public NavMenuLayout Layout { get; init; } = NavMenuLayout.Default;
    public NavMenuResponsiveSettings Responsive { get; init; } = NavMenuResponsiveSettings.Default;
    public NavMenuStyleSettings Style { get; init; } = NavMenuStyleSettings.Default;
    public string? SiteLogoUrl { get; init; }
    public List<INavMenuComponent> Left { get; init; } = [];
    public List<INavMenuComponent> Center { get; init; } = [];
    public List<INavMenuComponent> Right { get; init; } = [];

    public static NavMenuSnapshot Empty { get; } = new();

    public NavMenuSnapshot()
    {
    }

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

        foreach (var component in components)
        {
            BucketFor(component.Alignment).Add(component);
        }
    }

    [JsonIgnore]
    public IEnumerable<INavMenuComponent> Components => Left.Concat(Center).Concat(Right);

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

    private static void ValidateLinkTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || target is "_self" or "_blank" or "_parent" or "_top")
        {
            return;
        }

        throw new InvalidOperationException("Navigation link target must be _self, _blank, _parent, or _top.");
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NavLink), "link")]
[JsonDerivedType(typeof(NavMenu), "menu")]
[JsonDerivedType(typeof(NavHtml), "html")]
[JsonDerivedType(typeof(NavSearch), "search")]
public interface INavMenuComponent
{
    string Key { get; }
    NavAlignment Alignment { get; }
}

public enum NavAlignment
{
    Left,
    Center,
    Right
}

public sealed record NavLink : INavMenuComponent
{
    public string Key { get; init; } = string.Empty;
    public NavAlignment Alignment { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public bool OpenInNewTab { get; init; }
    public bool IsExternal { get; init; }
    public string? Target { get; init; }
    public long? PageId { get; init; }
    public string? AltText { get; init; }
}

public sealed record NavMenu : INavMenuComponent
{
    public string Key { get; init; } = string.Empty;
    public NavAlignment Alignment { get; init; }
    public string Label { get; init; } = string.Empty;
    public List<INavMenuComponent> Children { get; init; } = [];
}

public sealed record NavHtml : INavMenuComponent
{
    public string Key { get; init; } = string.Empty;
    public NavAlignment Alignment { get; init; }
    public string Html { get; init; } = string.Empty;
}

public sealed record NavSearch : INavMenuComponent
{
    public string Key { get; init; } = string.Empty;
    public NavAlignment Alignment { get; init; }
    public string Placeholder { get; init; } = "Search...";
    public string SearchAction { get; init; } = string.Empty;
}

public sealed record NavMenuLayout(IReadOnlyList<NavLayoutSlot> Slots)
{
    public static NavMenuLayout Default { get; } = new(
        [
            new NavLayoutSlot(NavLayoutSlots.Left, "Left", 0),
            new NavLayoutSlot(NavLayoutSlots.Center, "Center", 1),
            new NavLayoutSlot(NavLayoutSlots.Right, "Right", 2)
        ]);
}

public sealed record NavLayoutSlot(string Key, string Label, int Order);

public static class NavLayoutSlots
{
    public const string Left = "left";
    public const string Center = "center";
    public const string Right = "right";

    public static NavAlignment ToAlignment(string? slotKey)
        => slotKey?.ToLowerInvariant() switch
        {
            Center => NavAlignment.Center,
            Right => NavAlignment.Right,
            _ => NavAlignment.Left
        };
}

public sealed record NavMenuResponsiveSettings(string MobileBreakpoint)
{
    public static NavMenuResponsiveSettings Default { get; } = new("md");
}

public sealed record NavMenuStyleSettings(bool IsSticky)
{
    public static NavMenuStyleSettings Default { get; } = new(false);
}

public sealed record NavItemVisibility(
    bool HideOnMobile,
    bool HideOnDesktop,
    IReadOnlyList<string> AllowedRoles,
    RoleVisibilityMode RoleMode)
{
    public static NavItemVisibility Default { get; } = new(false, false, [], RoleVisibilityMode.Any);
    public bool HasRoleRules => AllowedRoles.Count > 0;
}

public enum NavLinkTarget
{
    InternalUrl,
    InternalPage,
    ExternalUrl
}

public enum SearchDisplayMode
{
    IconPopup,
    InlineTextbox
}

public enum SearchInputStyle
{
    Rounded,
    Square
}

public enum RoleVisibilityMode
{
    Any,
    All
}

public enum NavMenuLifecycleState
{
    Draft,
    Published,
    PublishedWithDraft,
    Archived
}
