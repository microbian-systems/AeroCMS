using Aero.Modular;

namespace Aero.Cms.Modules.Mail;

[Module(nameof(MailTrapModule))]
public class MailTrapModule : AeroModuleBase
{
    public override string Name => nameof(MailTrapModule);
    public override string Version => "0.0.5-alpha";
    public override string Author => "Microbians";
    public override IReadOnlyList<string> Dependencies => [];
    public override IReadOnlyList<string> Category => ["Communication", "Email"];
    public override IReadOnlyList<string> Tags => ["email", "mailtrap", "testing", "smtp"];
}