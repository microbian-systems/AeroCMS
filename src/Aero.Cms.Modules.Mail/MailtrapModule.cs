using Aero.Cms.Core;
using Aero.Cms.Core.Modules;

namespace Aero.Cms.Modules.Mail;

public class MailTrapModule : AeroModuleBase
{
    public override string Name => "MailTransport";
    public override string Version => AeroVersion.Version;
    public override string Author => AeroConstants.Author;
    public override IReadOnlyList<string> Dependencies => [];
}