using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed partial class CannedBlockDefinitionProvider
{
    static partial void AddLegacyDefinitions(List<IPageEditorBlockDefinition> definitions)
    {
        definitions.Add(new BoringHeroEditorDefinition());
        definitions.Add(new HeroEditorDefinition());
        definitions.Add(new TextEditorDefinition());
        definitions.Add(new ContentEditorDefinition());
        definitions.Add(new MarkdownEditorDefinition());
        definitions.Add(new QuoteEditorDefinition());
    }
}
