using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Blog.Importer;

/// <summary>
/// Describes the blog-import module to the Aero module catalog.
/// </summary>
/// <remarks>
/// The module currently contributes metadata only; it does not register services or endpoints.
/// </remarks>
[Module(nameof(BlogImporterModule))]
public class BlogImporterModule : AeroModuleBase
{
        /// <inheritdoc />
public override string Name => nameof(BlogImporterModule);
        /// <inheritdoc />
public override string Version => AeroConstants.Version;
        /// <inheritdoc />
public override string Author => AeroConstants.Author;
        /// <inheritdoc />
public override IReadOnlyList<string> Dependencies => [];
        /// <inheritdoc />
public override IReadOnlyList<string> Category => ["Content", "Migration"];
        /// <inheritdoc />
public override IReadOnlyList<string> Tags => ["blog", "import", "rss", "content"];
}
