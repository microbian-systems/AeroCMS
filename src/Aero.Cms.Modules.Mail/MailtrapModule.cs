using Aero.Cms.Core;
using MailKit;
using Microsoft.AspNetCore.Identity;

namespace Aero.Cms.Modules.Mail;

public class MailTrapModule : AeroModuleBase
{
    public override string Name { get; } = nameof(MailTransport);
    public override string Version { get; } = AeroVersion.Version;
    public override string Author { get; } = AeroConstants.Author;
    public override string Description { get; } = "Mailtrap email integration";
    public override bool Enabled { get; set; } = true;
    public override bool AllowInProduction { get; set; } = true;
    public override IReadOnlyList<string> Categories { get; } = ["email", "messaging", "communication"];
    public override IReadOnlyList<string> Tags { get; } = ["mail", "email"];
    public override IReadOnlyList<string> Dependencies { get; } = [];
}