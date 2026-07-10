namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Resolves immutable composition metadata for a catalog definition.
/// </summary>
public interface ICompositionCapabilityResolver
{
        /// <summary>
    /// TryGet method.
    /// </summary>
bool TryGet(string catalogId, out ICompositionCapabilities capabilities);
}
