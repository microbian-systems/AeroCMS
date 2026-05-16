using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Ui.Neo.Blocks.Hero;

namespace Aero.Cms.Ui.Neo;

public sealed class NeoPageEditorBlockProvider : IPageEditorBlockProvider, ICmsBlockModelProvider
{
    private static readonly IReadOnlyCollection<IPageEditorBlockDefinition> Definitions =
    [
        new Hero01EditorBlockDefinition()
    ];

    private static readonly IReadOnlyCollection<CmsBlockModelRegistration> BlockModels =
    [
        new(Hero01Block.BlockTypeId, typeof(Hero01Block))
    ];

    public IReadOnlyCollection<IPageEditorBlockDefinition> GetDefinitions() => Definitions;
    public IReadOnlyCollection<CmsBlockModelRegistration> GetBlockModels() => BlockModels;
}
