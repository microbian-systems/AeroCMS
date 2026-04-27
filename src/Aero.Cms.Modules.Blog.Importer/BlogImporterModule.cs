using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;
using Aero.Modular;

namespace Aero.Cms.Modules.Blog.Importer;

public class BlogImporterModule : AeroModuleBase
{
    public override string Name => nameof(BlogImporterModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Content", "Migration"];
    public override IReadOnlyList<string> Tags => ["blog", "import", "rss", "content"];
}
