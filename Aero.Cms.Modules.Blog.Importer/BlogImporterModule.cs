using Aero.Cms.Core;

namespace Aero.Cms.Modules.Blog.Importer;

public class BlogImporterModule : AeroModuleBase
{
    public override string Name => nameof(BlogImporterModule);

    public override string Version => "1.0.0";

    public override string Author => "Microbian Systems";

    public override IReadOnlyList<string> Dependencies => [];

    public override string Description => "Imports markdown / html content into the aero cms blog";
}
