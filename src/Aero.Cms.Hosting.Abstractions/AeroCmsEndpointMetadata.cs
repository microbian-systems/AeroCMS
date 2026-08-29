namespace Aero.Cms.Hosting;

/// <summary>
/// Marks an endpoint as owned by the Aero CMS request pipeline. Consumers may
/// apply this metadata to their own CMS extension endpoints.
/// </summary>
public sealed class AeroCmsEndpointMetadata
{
    /// <summary>Gets the shared immutable metadata instance.</summary>
    public static AeroCmsEndpointMetadata Instance { get; } = new();

    private AeroCmsEndpointMetadata()
    {
    }
}
