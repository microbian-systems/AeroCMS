using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aero.Cms.Html;

/// <summary>
/// Provides the manifest-backed set of HTML elements supported by the editor.
/// </summary>
public sealed class HtmlElementCatalog
{
    private const string ManifestResourceName = "Aero.Cms.Html.HtmlElementManifest.json";
    private readonly IReadOnlyDictionary<string, HtmlElementDefinition> _definitions;

    /// <summary>
    /// Creates a case-insensitive catalog while preserving each definition's canonical tag casing.
    /// </summary>
    /// <param name="definitions">The complete supported element definitions.</param>
    /// <param name="schemaVersion">The positive manifest shape version.</param>
    /// <param name="catalogVersion">The non-empty content version.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definitions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="schemaVersion"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="catalogVersion"/> is empty, or definitions contain duplicate tags.</exception>
    public HtmlElementCatalog(
        IEnumerable<HtmlElementDefinition> definitions,
        int schemaVersion = 1,
        string catalogVersion = "custom")
    {
        ArgumentNullException.ThrowIfNull(definitions);

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "The manifest schema version must be positive.");
        }

        if (string.IsNullOrWhiteSpace(catalogVersion))
        {
            throw new ArgumentException("The catalog version is required.", nameof(catalogVersion));
        }

        _definitions = definitions.ToDictionary(
            definition => definition.Tag,
            StringComparer.OrdinalIgnoreCase);
        SchemaVersion = schemaVersion;
        CatalogVersion = catalogVersion;
    }

    /// <summary>
    /// Loads the first-release catalog embedded with this library.
    /// </summary>
    /// <returns>The validated embedded catalog.</returns>
    /// <exception cref="InvalidOperationException">
    /// The resource is missing, unreadable, empty, or uses an unsupported schema version.
    /// </exception>
    public static HtmlElementCatalog CreateDefault()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ManifestResourceName)
            ?? throw new InvalidOperationException($"The HTML manifest resource '{ManifestResourceName}' was not found.");

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        serializerOptions.Converters.Add(new JsonStringEnumConverter());

        var manifest = JsonSerializer.Deserialize<HtmlElementManifest>(stream, serializerOptions)
            ?? throw new InvalidOperationException("The HTML manifest could not be read.");

        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"The HTML manifest schema version '{manifest.SchemaVersion}' is not supported.");
        }

        if (manifest.Elements.Count == 0)
        {
            throw new InvalidOperationException("The HTML manifest did not contain any element definitions.");
        }

        return new HtmlElementCatalog(
            manifest.Elements,
            manifest.SchemaVersion,
            manifest.CatalogVersion);
    }

    /// <summary>
    /// Gets every supported element definition.
    /// </summary>
    /// <remarks>A new collection snapshot is returned on each access.</remarks>
    public IReadOnlyCollection<HtmlElementDefinition> Definitions => _definitions.Values.ToArray();

    /// <summary>Gets the manifest shape version.</summary>
    public int SchemaVersion { get; }

    /// <summary>Gets the catalog content version.</summary>
    public string CatalogVersion { get; }

    /// <summary>
    /// Attempts to find a supported element by tag name.
    /// </summary>
    /// <param name="tagName">The tag to find; comparison is case-insensitive.</param>
    /// <param name="definition">Receives the catalog definition when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a non-empty supported tag was found.</returns>
    public bool TryGet(string? tagName, out HtmlElementDefinition? definition)
    {
        definition = null;
        return !string.IsNullOrWhiteSpace(tagName)
            && _definitions.TryGetValue(tagName, out definition);
    }

    /// <summary>
    /// Creates an element node only when the tag exists in this catalog.
    /// </summary>
    /// <param name="tagName">The supported tag to instantiate.</param>
    /// <returns>A fresh element using the definition's canonical tag casing and a fresh node identity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="tagName"/> is not in the catalog.</exception>
    public HtmlNode CreateElement(string tagName)
    {
        if (!TryGet(tagName, out var definition))
        {
            throw new ArgumentOutOfRangeException(nameof(tagName), tagName, "The tag is not supported by the HTML catalog.");
        }

        return HtmlNode.CreateElement(definition!.Tag);
    }
}
