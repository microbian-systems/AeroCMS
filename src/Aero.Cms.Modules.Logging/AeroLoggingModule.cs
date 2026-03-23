using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.Modules.Logging;

public class AeroLoggingModule : AeroModuleBase
{
    public override string Name => nameof(AeroLoggingModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Infrastructure", "Diagnostics"];
    public override IReadOnlyList<string> Tags => ["logging", "diagnostics", "tracing", "serilog"];
}
