using Aero.Cms.Core.Modules;

namespace Aero.Cms.Modules.Blog.Importer;

public class BlogImporterModule : AeroModuleBase
{
    public override string Name => nameof(BlogImporterModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Content", "Migration"];
    public override IReadOnlyList<string> Tags => ["blog", "import", "rss", "content"];
}
