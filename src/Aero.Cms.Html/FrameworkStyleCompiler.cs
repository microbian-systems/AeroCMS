using System.Security.Cryptography;
using System.Text;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Html;

/// <summary>
/// Decorates the native compiler with exact framework-class mappings and preserves
/// scoped native CSS for every intent the selected framework cannot express exactly.
/// </summary>
/// <param name="adapter">The exact-mapping strategy for the selected CSS framework.</param>
/// <param name="nativeFallback">The compiler used for residual style intent; the native compiler is used when omitted.</param>
public sealed class FrameworkStyleCompiler(
    IStyleFrameworkAdapter adapter,
    IStyleCompiler? nativeFallback = null) : IStyleCompiler
{
    private readonly IStyleCompiler _nativeFallback = nativeFallback ?? new NativeCssStyleCompiler();

    /// <inheritdoc />
    public Result<CompiledPageStyles> Compile(HtmlPageContent content, IStyleProfile profile)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(profile);

        var residualContent = HtmlTreeOperations.ClonePreservingNodeIds(content);
        var frameworkClasses = new Dictionary<long, IReadOnlyList<string>>();
        MapNode(residualContent.Root, profile, frameworkClasses);

        var fallbackResult = _nativeFallback.Compile(residualContent, profile);
        if (fallbackResult is Result<CompiledPageStyles>.Failure failure)
        {
            return failure.Error;
        }

        if (fallbackResult is not Result<CompiledPageStyles>.Ok fallback)
        {
            return AeroError.CreateError("The native style fallback returned an unexpected result.");
        }

        var nodeClasses = MergeClasses(content.Root, frameworkClasses, fallback.Value);
        var profileId = $"{profile.ProfileId}/{adapter.AdapterId}";
        var profileVersion = $"{profile.ProfileVersion}+{adapter.AdapterVersion}";
        var canonical = Canonicalize(content.Root, nodeClasses, fallback.Value.CssText, profileId, profileVersion);

        return new Result<CompiledPageStyles>.Ok(new CompiledPageStyles
        {
            NodeClasses = nodeClasses,
            CssText = fallback.Value.CssText,
            ContentHash = Hash(canonical),
            ProfileId = profileId,
            ProfileVersion = profileVersion
        });
    }

    /// <summary>Maps exact framework classes on a cloned tree and leaves only residual style intent.</summary>
    private void MapNode(
        HtmlNode node,
        IStyleProfile profile,
        IDictionary<long, IReadOnlyList<string>> frameworkClasses)
    {
        if (node.Style is not null)
        {
            var mapping = adapter.Map(node.Style, profile);
            if (mapping.Classes.Count > 0)
            {
                frameworkClasses[node.NodeId] = mapping.Classes
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            node.Style = mapping.ResidualStyle;
        }

        foreach (var child in node.Children)
        {
            MapNode(child, profile, frameworkClasses);
        }
    }

    /// <summary>Merges framework and native-fallback classes for every node in source-tree order.</summary>
    private static IReadOnlyDictionary<long, IReadOnlyList<string>> MergeClasses(
        HtmlNode root,
        IReadOnlyDictionary<long, IReadOnlyList<string>> frameworkClasses,
        CompiledPageStyles fallback)
    {
        var merged = new Dictionary<long, IReadOnlyList<string>>();
        MergeNode(root, frameworkClasses, fallback, merged);
        return merged;
    }

    /// <summary>Combines and de-duplicates both compiler class sets for one node before recursing.</summary>
    private static void MergeNode(
        HtmlNode node,
        IReadOnlyDictionary<long, IReadOnlyList<string>> frameworkClasses,
        CompiledPageStyles fallback,
        IDictionary<long, IReadOnlyList<string>> merged)
    {
        var classes = frameworkClasses.GetValueOrDefault(node.NodeId, [])
            .Concat(fallback.ClassesFor(node.NodeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (classes.Length > 0)
        {
            merged[node.NodeId] = classes;
        }

        foreach (var child in node.Children)
        {
            MergeNode(child, frameworkClasses, fallback, merged);
        }
    }

    /// <summary>Builds the complete deterministic fingerprint input for profile, CSS, structure, and classes.</summary>
    private static string Canonicalize(
        HtmlNode root,
        IReadOnlyDictionary<long, IReadOnlyList<string>> nodeClasses,
        string css,
        string profileId,
        string profileVersion)
    {
        var builder = new StringBuilder()
            .Append(profileId).Append('|')
            .Append(profileVersion).Append('|')
            .Append(css);
        AppendClasses(root, nodeClasses, builder);
        return builder.ToString();
    }

    /// <summary>Appends structural identity and ordered class assignments without depending on node IDs.</summary>
    private static void AppendClasses(
        HtmlNode node,
        IReadOnlyDictionary<long, IReadOnlyList<string>> nodeClasses,
        StringBuilder builder)
    {
        builder.Append('|').Append(node.Kind).Append(':').Append(node.TagName);
        foreach (var className in nodeClasses.GetValueOrDefault(node.NodeId, []))
        {
            builder.Append(':').Append(className);
        }

        foreach (var child in node.Children)
        {
            AppendClasses(child, nodeClasses, builder);
        }
    }

    /// <summary>Computes a deterministic lowercase SHA-256 digest for compiled output identity.</summary>
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
