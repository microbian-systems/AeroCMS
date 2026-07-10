using Aero.Cms.Abstractions.Blocks;

namespace Aero.Cms.Abstractions.Blocks.Editing;

/// <summary>
/// Creates block instances by source-generated type discriminator switches when available.
/// </summary>
public static partial class GeneratedBlockFactory
{
        /// <summary>
    /// CreateByTypeName method.
    /// </summary>
public static BlockBase? CreateByTypeName(string typeName)
    {
        BlockBase? block = null;
        CreateGeneratedByTypeName(typeName, ref block);
        return block;
    }

    static partial void CreateGeneratedByTypeName(string typeName, ref BlockBase? block);
}
