namespace Aero.Cms.Hosting;

/// <summary>
/// Declares capabilities supplied by an Aero CMS module registration.
/// </summary>
[Flags]
public enum AeroCmsCapabilities
{
    /// <summary>No optional hosting capability is supplied.</summary>
    None = 0,

    /// <summary>Server-side Razor components are supplied.</summary>
    ServerComponents = 1 << 0,

    /// <summary>Interactive WebAssembly components are supplied.</summary>
    WebAssemblyComponents = 1 << 1,

    /// <summary>Manager identity endpoints and services are supplied.</summary>
    Identity = 1 << 2,

    /// <summary>Setup endpoints and services are supplied.</summary>
    Setup = 1 << 3,

    /// <summary>Public read-only query endpoints are supplied.</summary>
    PublicQuery = 1 << 4,

    /// <summary>CMS manager endpoints and UI are supplied.</summary>
    Manager = 1 << 5
}
