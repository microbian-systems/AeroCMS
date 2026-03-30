namespace Aero.Cms.Core.Blocks;

/// <summary>
/// Defines the type discriminators for block types in the CMS.
/// </summary>
public enum BlockKind
{
    /// <summary>
    /// Rich text content block.
    /// </summary>
    RichText = 0,

    /// <summary>
    /// Heading content block.
    /// </summary>
    Heading = 1,

    /// <summary>
    /// Image content block.
    /// </summary>
    Image = 2,

    /// <summary>
    /// Call-to-action content block.
    /// </summary>
    Cta = 3,

    /// <summary>
    /// Quote content block.
    /// </summary>
    Quote = 4,

    /// <summary>
    /// Embed content block for external content.
    /// </summary>
    Embed = 5
}
