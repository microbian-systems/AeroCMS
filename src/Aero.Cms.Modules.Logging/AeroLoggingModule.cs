using Aero.Modular;

namespace Aero.Cms.Modules.Logging;


/// <summary>
/// Supplies discovery metadata for the Aero CMS logging module.
/// </summary>
/// <remarks>
/// This class does not override any service-configuration or runtime hooks.
/// Consequently, it does not register a logging provider or configure the
/// referenced OpenObserve sink, enrichment, filtering, request correlation,
/// buffering, delivery, failure handling, or redaction.
/// </remarks>
[Module(nameof(AeroLoggingModule))]
public class AeroLoggingModule : AeroModuleBase
{
        /// <summary>
    /// The stable module identifier, <c>AeroLoggingModule</c>.
    /// </summary>
public override string Name => nameof(AeroLoggingModule);
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
    /// The infrastructure and diagnostics categories used to classify this module.
    /// </summary>
public override IReadOnlyList<string> Category => ["Infrastructure", "Diagnostics"];
        /// <summary>
    /// The discovery tags assigned to this module.
    /// </summary>
    /// <remarks>
    /// These values are metadata only; the <c>tracing</c> and <c>serilog</c>
    /// tags do not enable tracing or configure Serilog.
    /// </remarks>
public override IReadOnlyList<string> Tags => ["logging", "diagnostics", "tracing", "serilog"];
}
