using Aero.Cms.Core;
using Aero.Cms.Web.Core.Modules;

namespace Aero.Cms.Modules.Tcp;


/// <summary>
/// todo - make use of supersocket and supersocket.kestrel to make custom tcp (not-http) calls
/// </summary>
public class CustomTcpListener : AeroModuleBase
{
    public override string Name => nameof(CustomTcpListener);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => [];

    public override IReadOnlyList<string> Tags => [];
}