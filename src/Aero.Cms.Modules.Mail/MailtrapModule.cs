using Aero.Modular;

namespace Aero.Cms.Modules.Mail;

/// <summary>
/// Supplies discovery metadata for the Mailtrap module.
/// </summary>
/// <remarks>
/// This class does not override service-configuration or runtime hooks. It
/// therefore does not configure MailKit, MimeKit, SMTP, credentials, a provider,
/// message composition, recipients, templates, queueing, persistence, retries,
/// idempotency, logging, redaction, or delivery.
/// </remarks>
[Module(nameof(MailTrapModule))]
public class MailTrapModule : AeroModuleBase
{
        /// <summary>
    /// The stable module identifier, <c>MailTrapModule</c>.
    /// </summary>
public override string Name => nameof(MailTrapModule);
        /// <summary>
    /// The fixed module version, <c>0.0.5-alpha</c>.
    /// </summary>
public override string Version => "0.0.5-alpha";
        /// <summary>
    /// The module author, <c>Microbians</c>.
    /// </summary>
public override string Author => "Microbians";
        /// <summary>
    /// An empty collection because the module declares no module-ordering dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// The communication and email categories used to classify this module.
    /// </summary>
public override IReadOnlyList<string> Category => ["Communication", "Email"];
        /// <summary>
    /// The email, Mailtrap, testing, and SMTP discovery tags assigned to this module.
    /// </summary>
    /// <remarks>
    /// These values are metadata only and do not select or configure a mail
    /// provider.
    /// </remarks>
public override IReadOnlyList<string> Tags => ["email", "mailtrap", "testing", "smtp"];
}
