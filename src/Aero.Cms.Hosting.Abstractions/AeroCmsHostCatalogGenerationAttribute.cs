namespace Aero.Cms.Hosting;

/// <summary>
/// Opts a C# catalog assembly into compile-time aggregation of the Aero CMS module
/// manifests and Wolverine registrations exposed by its referenced packages.
/// </summary>
/// <remarks>
/// This attribute is a C# convenience only. F# and other .NET languages consume
/// compiled <see cref="AeroCmsHostCatalog"/> instances or construct registrations
/// through <see cref="AeroCmsHostCatalogBuilder"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class AeroCmsHostCatalogGenerationAttribute : Attribute;
