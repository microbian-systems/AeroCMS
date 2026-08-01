using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Tcp;


/// <summary>
/// Provides metadata for a planned custom TCP module.
/// </summary>
/// <remarks>
/// Despite its name, this type does not create or register a listener, bind an address or port, define framing or a
/// protocol, accept connections, or implement a connection lifecycle. It also configures no TLS, authentication,
/// tenant scope, concurrency, limits, timeouts, cancellation, or failure handling. The project contains no
/// <c>Module</c> attribute or service/pipeline registration for this type, so its presence is not evidence of an
/// exposed or secure TCP transport.
/// </remarks>
public class CustomTcpListener : AeroModuleBase
{
    /// <summary>Gets the fixed metadata name <c>CustomTcpListener</c>.</summary>
    public override string Name => nameof(CustomTcpListener);

    /// <summary>Gets the Aero CMS version reported by this metadata type.</summary>
    public override string Version => AeroConstants.Version;

    /// <summary>Gets the Aero CMS author metadata.</summary>
    public override string Author => AeroConstants.Author;

    /// <summary>Gets an empty module dependency list.</summary>
    public override IReadOnlyList<string> Dependencies => [];

    /// <summary>Gets an empty discovery-category list.</summary>
    public override IReadOnlyList<string> Category => [];

    /// <summary>Gets an empty discovery-tag list.</summary>
    public override IReadOnlyList<string> Tags => [];
}
