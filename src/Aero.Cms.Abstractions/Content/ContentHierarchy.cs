namespace Aero.Cms.Abstractions.Content;

/// <summary>Defines how many items a content type may contain.</summary>
public enum ContentCardinality
{
    /// <summary>The type may contain at most one item per site and culture.</summary>
    Singleton = 0,

    /// <summary>The type may contain multiple items.</summary>
    Collection = 1
}

/// <summary>Defines whether content items are flat or parent/child structured.</summary>
public enum ContentStructure
{
    /// <summary>Items cannot have a parent.</summary>
    Flat = 0,

    /// <summary>Items may form a validated hierarchy.</summary>
    Hierarchical = 1
}

/// <summary>Validation and ordering rules for a hierarchical content collection.</summary>
[GenerateSerializer]
[Alias("ContentHierarchyRules")]
public sealed record ContentHierarchyRules
{
    /// <summary>Gets whether an item may be placed at the hierarchy root.</summary>
    [Id(0)]
    public bool AllowRootItems { get; init; } = true;

    /// <summary>Gets whether a parent must use the same content type as its child.</summary>
    [Id(1)]
    public bool RequireSameTypeParent { get; init; } = true;

    /// <summary>Gets the maximum number of parent edges from a root to an item.</summary>
    [Id(2)]
    public int MaximumDepth { get; init; } = 8;

    /// <summary>
    /// Gets the explicitly allowed parent content-type IDs when same-type parents are
    /// not required. An empty collection denies cross-type parents.
    /// </summary>
    [Id(3)]
    public IReadOnlyList<long> AllowedParentContentTypeIds { get; init; } = [];

    /// <summary>Gets the stable default ordering descriptor.</summary>
    [Id(4)]
    public string DefaultOrdering { get; init; } = "sortOrder,title";
}
