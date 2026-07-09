using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Rewrite;

[Module(nameof(RewriteModule))]
public class RewriteModule : AeroModuleBase
{
    public override string Name => nameof(RewriteModule);
    public override string Version => AeroConstants.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Routing"];
    public override IReadOnlyList<string> Tags => ["rewrite", "redirect", "routing", "url"];



}
