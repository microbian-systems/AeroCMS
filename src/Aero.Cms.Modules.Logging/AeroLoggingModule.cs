using Aero.Modular;

namespace Aero.Cms.Modules.Logging;


/// <summary>
/// Design to trap errors in the manager application (errors only)
/// </summary>
[Module(nameof(AeroLoggingModule))]
public class AeroLoggingModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(AeroLoggingModule);
        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => "0.0.5-alpha";
        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => "Microbians";
        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => ["Infrastructure", "Diagnostics"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["logging", "diagnostics", "tracing", "serilog"];
}
