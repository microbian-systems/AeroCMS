using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.Modules.ContentCreator;

public class AeroContentCreator : AeroModuleBase
{
    public override string Name => nameof(AeroContentCreator);

    public override string Version => AeroVersion.Version;

    public override string Author => AeroConstants.Author;

    public override string Description => """
                                          An AI content creation module. Feed it a URL or text or simply 
                                          just ask it to create content based on a topic you provide
                                          """;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["ai", "content-generation", "content"];

    public override IReadOnlyList<string> Tags => ["ai", "ai-content-creator", "content-generation"];
}