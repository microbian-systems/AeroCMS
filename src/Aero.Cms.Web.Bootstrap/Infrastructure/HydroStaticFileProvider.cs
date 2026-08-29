using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Aero.Cms.Web.Bootstrap.Infrastructure;

/// <summary>
/// Exposes Hydro's embedded browser scripts before the host registers its static-file middleware.
/// Hydro's endpoint middleware is deliberately registered later, after CMS authorization.
/// </summary>
internal sealed class HydroStaticFileProvider : IFileProvider
{
    private readonly EmbeddedFileProvider _embedded =
        new(typeof(Hydro.Configuration.ApplicationBuilderExtensions).Assembly);

    public IDirectoryContents GetDirectoryContents(string subpath)
        => NotFoundDirectoryContents.Singleton;

    public IFileInfo GetFileInfo(string subpath)
        => subpath switch
        {
            "/hydro.js" or "/hydro/hydro.js" => _embedded.GetFileInfo("/Scripts.hydro.js"),
            "/hydro/alpine.js" => _embedded.GetFileInfo("/Scripts.AlpineJs.alpinejs-combined.min.js"),
            _ => new NotFoundFileInfo(subpath)
        };

    public IChangeToken Watch(string filter)
        => NullChangeToken.Singleton;
}
