namespace Aero.Cms.Abstractions.Blocks;

/// <summary>
/// Marks a block type for registration in the CMS block system.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BlockMetadataAttribute : Attribute
{
    /// <summary>
    /// Gets the unique name of the block type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the display name of the block type.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the description of the block type.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets the category of the block type.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets the icon identifier for the block type.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Gets the sort order for the block type in UI listings.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockMetadataAttribute"/> class.
    /// </summary>
    /// <param name="name">The unique name of the block type.</param>
    /// <param name="displayName">The display name of the block type.</param>
    public BlockMetadataAttribute(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }
}
