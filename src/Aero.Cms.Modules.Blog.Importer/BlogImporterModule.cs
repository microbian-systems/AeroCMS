using Aero.Cms.Core;
using Aero.Modular;

namespace Aero.Cms.Modules.Blog.Importer;

/// <summary>
/// Represents a class for BlogImporterModule.
/// </summary>
[Module(nameof(BlogImporterModule))]
public class BlogImporterModule : AeroModuleBase
{
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
public override string Name => nameof(BlogImporterModule);
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
public override IReadOnlyList<string> Category => ["Content", "Migration"];
        /// <summary>
    /// Gets or sets the Tags.
    /// </summary>
public override IReadOnlyList<string> Tags => ["blog", "import", "rss", "content"];
}
