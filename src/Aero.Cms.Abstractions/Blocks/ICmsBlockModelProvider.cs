namespace Aero.Cms.Abstractions.Blocks;

/// <summary>
/// Represents a record for CmsBlockModelRegistration.
/// </summary>
public sealed record CmsBlockModelRegistration(string BlockType, Type ModelType);

/// <summary>
/// Defines an interface for ICmsBlockModelProvider.
/// </summary>
public interface ICmsBlockModelProvider
{
        /// <summary>
    /// GetBlockModels method.
    /// </summary>
IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels();
}
