namespace Aero.Cms.Abstractions.Blocks;

public sealed record CmsBlockModelRegistration(string BlockType, Type ModelType);

public interface ICmsBlockModelProvider
{
    IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels();
}
