using Aero.Cms.Abstractions.Blocks.Editor;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Definitions;

public sealed partial class CannedBlockDefinitionProvider
{
    static partial void AddAeroUxDefinitions(List<IPageEditorBlockDefinition> definitions)
    {
        definitions.Add(new AeroHeroDefinition());
        definitions.Add(new AeroFeaturesDefinition());
        definitions.Add(new AeroCtaDefinition());
        definitions.Add(new AeroBlogDefinition());
        definitions.Add(new AeroPricingDefinition());
        definitions.Add(new AeroTeamsDefinition());
        definitions.Add(new AeroTestimonialsDefinition());
        definitions.Add(new AeroFaqDefinition());
        definitions.Add(new AeroPortfolioDefinition());
        definitions.Add(new AeroContactDefinition());
        definitions.Add(new AeroTableDefinition());
        definitions.Add(new AeroAuthDefinition());
    }
}
