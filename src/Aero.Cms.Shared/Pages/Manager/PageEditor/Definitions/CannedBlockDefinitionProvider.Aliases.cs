using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed partial class CannedBlockDefinitionProvider
{
    static partial void AddAliasDefinitions(List<IPageEditorBlockDefinition> definitions)
    {
        definitions.Add(new LegacyImageBlockDefinition());
        definitions.Add(new LegacyVideoBlockDefinition());
        definitions.Add(new LegacyAudioBlockDefinition());
        definitions.Add(new LegacyGalleryBlockDefinition());
        definitions.Add(new LegacyCarouselBlockDefinition());
        definitions.Add(new LegacyRawHtmlBlockDefinition());
        definitions.Add(new LegacySeparatorBlockDefinition());
        definitions.Add(new LegacyDynamicTemplateBlockDefinition());
        definitions.Add(new LegacyColumnsBlockDefinition());
    }
}
