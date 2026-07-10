using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Tcp;


/// <summary>
/// todo - make use of supersocket and supersocket.kestrel to make custom tcp (not-http) calls
/// </summary>
public class CustomTcpListener : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(CustomTcpListener);

        /// <summary>
    /// Gets or sets the Version.
    /// </summary>
public override string Version => AeroConstants.Version;

        /// <summary>
    /// Gets or sets the Author.
    /// </summary>
public override string Author => AeroConstants.Author;

        /// <summary>
    /// Gets or sets the Dependencies.
    /// </summary>
public override IReadOnlyList<string> Dependencies => [];

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public override IReadOnlyList<string> Category => [];

        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => [];
}