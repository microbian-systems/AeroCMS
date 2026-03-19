using Aero.Cms.Core.Modules;

namespace Aero.Cms.Modules.Blog.Importer;

public class BlogImporterModule : AeroModuleBase
{
    public override string Name => "Blog Importer";
    public override string Version => "1.0.0";
    public override string Author => "Microbian Systems";
    public override IReadOnlyList<string> Dependencies => Array.Empty<string>();
}
