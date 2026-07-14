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
    public IReadOnlyCollection<HtmlElementDefinition> Definitions => _definitions.Values.ToArray();

    public int SchemaVersion { get; }

    public string CatalogVersion { get; }

    /// <summary>
    /// Attempts to find a supported element by tag name.
    /// </summary>
    public bool TryGet(string? tagName, out HtmlElementDefinition? definition)
    {
        definition = null;
        return !string.IsNullOrWhiteSpace(tagName)
            && _definitions.TryGetValue(tagName, out definition);
    }

    /// <summary>
    /// Creates an element node only when the tag exists in this catalog.
    /// </summary>
    public HtmlNode CreateElement(string tagName)
    {
        if (!TryGet(tagName, out var definition))
        {
            throw new ArgumentOutOfRangeException(nameof(tagName), tagName, "The tag is not supported by the HTML catalog.");
        }

        return HtmlNode.CreateElement(definition!.Tag);
    }
}
