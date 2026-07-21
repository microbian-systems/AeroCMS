namespace Aero.Cms.Abstractions.Pages.Composition;

/// <summary>Controls public output when a content-list query is empty.</summary>
public enum PageContentEmptyStateBehavior
{
    /// <summary>Remove the scope output.</summary>
    RenderNothing,

    /// <summary>Render the saved template once as an author-defined placeholder.</summary>
    RenderTemplate
}

/// <summary>Controls content-list ordering.</summary>
public enum PageContentSortDirection
{
    /// <summary>Sort from the lowest value to the highest value.</summary>
    Ascending,

    /// <summary>Sort from the highest value to the lowest value.</summary>
    Descending
}

/// <summary>Defines the allowlisted comparisons available to a content-list query.</summary>
public enum PageContentFilterOperator
{
    /// <summary>The field must equal the supplied value.</summary>
    Equals,

    /// <summary>The field must not equal the supplied value.</summary>
    NotEquals,

    /// <summary>The field must contain the supplied value.</summary>
    Contains,

    /// <summary>The field must start with the supplied value.</summary>
    StartsWith,

    /// <summary>The field must end with the supplied value.</summary>
    EndsWith,

    /// <summary>The field must be greater than the supplied value.</summary>
    GreaterThan,

    /// <summary>The field must be greater than or equal to the supplied value.</summary>
    GreaterThanOrEqual,

    /// <summary>The field must be less than the supplied value.</summary>
    LessThan,

    /// <summary>The field must be less than or equal to the supplied value.</summary>
    LessThanOrEqual,

    /// <summary>The field must be empty.</summary>
    IsEmpty,

    /// <summary>The field must not be empty.</summary>
    IsNotEmpty
}

/// <summary>Controls how a single content item is resolved.</summary>
public enum PageContentItemLookupMode
{
    /// <summary>Resolve by the stable content-item identifier.</summary>
    StableId,

    /// <summary>Resolve by an explicitly configured routing slug.</summary>
    Slug
}

/// <summary>Defines the allowlisted HTML targets for a projected field value.</summary>
public enum PageFieldBindingTarget
{
    /// <summary>Replace the target element's text content.</summary>
    TextContent,

    /// <summary>Set the target anchor's <c>href</c> attribute.</summary>
    Hyperlink,

    /// <summary>Set the target media element's <c>src</c> attribute.</summary>
    Source,

    /// <summary>Set the target media element's <c>alt</c> attribute.</summary>
    AlternativeText,

    /// <summary>Set the target element's <c>title</c> attribute.</summary>
    Title,

    /// <summary>Set the target form element's <c>value</c> attribute.</summary>
    Value
}
