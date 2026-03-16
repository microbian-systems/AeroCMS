using Aero.Cms.Core;

namespace Aero.Cms.Modules.Logging;

public class AeroLoggingModule : AeroModuleBase
{
    public override string Name => nameof(AeroLoggingModule);

    public override string Version => "";

    public override string Author => "";

    public override IReadOnlyList<string> Dependencies => [];

    public override string Description => "Aero built-in logging module";

    public override bool Enabled { get => ""; set => ""; }
    public override bool AllowInProduction { get => ""; set => ""; }

    public override IReadOnlyList<string> Categories => "";

    public override IReadOnlyList<string> Tags => "";
}