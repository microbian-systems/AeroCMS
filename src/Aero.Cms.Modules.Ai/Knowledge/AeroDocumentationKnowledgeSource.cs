using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Ai.Knowledge;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>Searches the curated, build-embedded AeroCMS product documentation corpus.</summary>
public interface IAeroDocumentationKnowledgeSource
{
    AeroDocumentationKnowledgeSnapshot GetSnapshot();

    IReadOnlyList<AeroAiKnowledgeMatch> Search(string query, int take);
}

/// <summary>
/// Loads the generated manager corpus embedded in the AI module and performs a
/// bounded deterministic text search. The corpus contains curated <c>docs/</c>
/// sources only; design history and security-sensitive entries are excluded by
/// the documentation generator.
/// </summary>
public sealed class EmbeddedAeroDocumentationKnowledgeSource
    : IAeroDocumentationKnowledgeSource
{
    private const string ResourceName =
        "Aero.Cms.Modules.Ai.Knowledge.manager-assistant-corpus.json";

    private readonly Lazy<AeroDocumentationKnowledgeSnapshot> _snapshot =
        new(LoadSnapshot, LazyThreadSafetyMode.ExecutionAndPublication);

    public AeroDocumentationKnowledgeSnapshot GetSnapshot() => _snapshot.Value;

    public IReadOnlyList<AeroAiKnowledgeMatch> Search(string query, int take)
    {
        if (string.IsNullOrWhiteSpace(query) || take <= 0)
            return [];

        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return [];

        return _snapshot.Value.Chunks
            .Select(chunk => new { Chunk = chunk, Score = Score(chunk, tokens) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Chunk.Title, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Chunk.Section, StringComparer.Ordinal)
            .Take(Math.Clamp(take, 1, AeroAiKnowledgeConstants.MaximumTake))
            .Select(candidate => candidate.Chunk.ToMatch())
            .ToArray();
    }

    private static AeroDocumentationKnowledgeSnapshot LoadSnapshot()
    {
        using var stream = typeof(EmbeddedAeroDocumentationKnowledgeSource)
            .Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The embedded AeroCMS documentation corpus '{ResourceName}' was not found.");
        var corpus = System.Text.Json.JsonSerializer.Deserialize(
            stream,
            AeroDocumentationCorpusJsonContext.Default.AeroDocumentationCorpus)
            ?? throw new InvalidOperationException(
                "The embedded AeroCMS documentation corpus is invalid.");
        AeroDocumentationCorpusValidator.Validate(corpus);

        var sourceRevision = StableId(corpus.LastVerifiedCommit);
        var chunks = new List<AeroDocumentationKnowledgeChunk>();
        foreach (var entry in corpus.Entries)
        {
            var sourceId = StableId(entry.CanonicalPath);
            var chunkRevision = 0;
            foreach (var section in SplitSections(entry.Content))
            {
                foreach (var content in AeroAiKnowledgeChunker.Chunk(section.Content))
                {
                    var fullText = string.Join(
                        ' ',
                        new[]
                        {
                            entry.Title,
                            entry.FeatureArea,
                            section.Name,
                            content
                        }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    var contentHash = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(fullText)));
                    var chunkId = StableId(
                        $"{AeroDocumentationKnowledgeConstants.CorpusId}|{entry.CanonicalPath}|{section.Name}|{chunkRevision}");
                    chunks.Add(new AeroDocumentationKnowledgeChunk(
                        chunkId,
                        sourceId,
                        entry.CanonicalPath,
                        "en-US",
                        entry.Title,
                        entry.FeatureArea,
                        entry.Maturity,
                        entry.Audience,
                        section.Name,
                        content,
                        fullText,
                        sourceRevision,
                        chunkRevision++,
                        contentHash,
                        corpus.TrustClass));
                }
            }
        }

        var corpusHashInput = string.Join(
            '\n',
            chunks.Select(chunk =>
                $"{chunk.Id}|{chunk.SourceRevision}|{chunk.ContentHash}|{chunk.SourceAudience}|{chunk.Maturity}"));
        var corpusHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(corpusHashInput)));

        return new AeroDocumentationKnowledgeSnapshot(
            corpus.SchemaVersion,
            corpus.Product,
            corpus.LastVerifiedCommit,
            sourceRevision,
            corpus.TrustClass,
            corpusHash,
            chunks);
    }

    private static IReadOnlyList<DocumentationSection> SplitSections(string content)
    {
        var sections = new List<DocumentationSection>();
        var name = "Overview";
        var body = new StringBuilder();
        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                AddSection(sections, name, body);
                name = line[3..].Trim();
                continue;
            }

            body.AppendLine(line);
        }

        AddSection(sections, name, body);
        return sections;
    }

    private static void AddSection(
        ICollection<DocumentationSection> sections,
        string name,
        StringBuilder body)
    {
        var content = body.ToString().Trim();
        body.Clear();
        if (!string.IsNullOrWhiteSpace(content))
            sections.Add(new DocumentationSection(name, content));
    }

    private static IReadOnlySet<string> Tokenize(string value)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var token = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Append(char.ToLowerInvariant(character));
                continue;
            }

            AddToken(tokens, token);
        }

        AddToken(tokens, token);
        return tokens;
    }

    private static void AddToken(ISet<string> tokens, StringBuilder token)
    {
        if (token.Length >= 2)
            tokens.Add(token.ToString());
        token.Clear();
    }

    private static int Score(
        AeroDocumentationKnowledgeChunk chunk,
        IReadOnlySet<string> tokens)
    {
        var title = chunk.Title.ToLowerInvariant();
        var featureArea = chunk.FeatureArea.ToLowerInvariant();
        var section = chunk.Section.ToLowerInvariant();
        var content = chunk.Content.ToLowerInvariant();
        var score = 0;
        foreach (var token in tokens)
        {
            if (title.Contains(token, StringComparison.Ordinal))
                score += 12;
            if (featureArea.Contains(token, StringComparison.Ordinal))
                score += 8;
            if (section.Contains(token, StringComparison.Ordinal))
                score += 6;
            if (content.Contains(token, StringComparison.Ordinal))
                score += 2;
        }

        return score;
    }

    private static long StableId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var id = BinaryPrimitives.ReadInt64LittleEndian(hash) & long.MaxValue;
        return id == 0 ? 1 : id;
    }

    private sealed record DocumentationSection(string Name, string Content);

}

public sealed record AeroDocumentationKnowledgeSnapshot(
    int SchemaVersion,
    string Product,
    string LastVerifiedCommit,
    long SourceRevision,
    string TrustClass,
    string CorpusHash,
    IReadOnlyList<AeroDocumentationKnowledgeChunk> Chunks);

public sealed record AeroDocumentationKnowledgeChunk(
    long Id,
    long SourceId,
    string CanonicalPath,
    string Culture,
    string Title,
    string FeatureArea,
    string Maturity,
    string SourceAudience,
    string Section,
    string Content,
    string FullText,
    long SourceRevision,
    int ChunkRevision,
    string ContentHash,
    string TrustClass)
{
    public AeroAiKnowledgeMatch ToMatch()
        => new(
            Id,
            AeroAiKnowledgeSourceKinds.AeroDocumentation,
            SourceId,
            CanonicalPath,
            Culture,
            Title,
            Section,
            Content,
            SourceRevision,
            ChunkRevision,
            ContentHash);
}

public sealed record AeroDocumentationCorpus(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("product")] string Product,
    [property: JsonPropertyName("last_verified_commit")] string LastVerifiedCommit,
    [property: JsonPropertyName("trust_class")] string TrustClass,
    [property: JsonPropertyName("entries")] IReadOnlyList<AeroDocumentationCorpusEntry> Entries);

public sealed record AeroDocumentationCorpusEntry(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("canonical_path")] string CanonicalPath,
    [property: JsonPropertyName("feature_area")] string FeatureArea,
    [property: JsonPropertyName("maturity")] string Maturity,
    [property: JsonPropertyName("audience")] string Audience,
    [property: JsonPropertyName("source_files")] IReadOnlyList<string> SourceFiles,
    [property: JsonPropertyName("content")] string Content);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(AeroDocumentationCorpus))]
public partial class AeroDocumentationCorpusJsonContext : JsonSerializerContext;

internal static class AeroDocumentationCorpusValidator
{
    private const int SupportedSchemaVersion = 1;
    private const int MaximumEntries = 4_096;
    private static readonly HashSet<string> AllowedAudiences =
        new(StringComparer.Ordinal)
        {
            "public",
            "manager-internal"
        };

    public static void Validate(AeroDocumentationCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        if (corpus.SchemaVersion != SupportedSchemaVersion)
            Invalid($"schema version '{corpus.SchemaVersion}' is not supported");
        if (!string.Equals(corpus.Product, "AeroCMS", StringComparison.Ordinal))
            Invalid("the product must be 'AeroCMS'");
        if (string.IsNullOrWhiteSpace(corpus.LastVerifiedCommit))
            Invalid("the verified Git commit is required");
        if (!string.Equals(
                corpus.TrustClass,
                "manager-internal",
                StringComparison.Ordinal))
        {
            Invalid("the root trust class must be 'manager-internal'");
        }

        if (corpus.Entries is null or { Count: 0 })
            Invalid("at least one documentation entry is required");
        if (corpus.Entries.Count > MaximumEntries)
            Invalid($"the corpus exceeds the {MaximumEntries}-entry limit");

        var canonicalPaths = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < corpus.Entries.Count; index++)
        {
            var entry = corpus.Entries[index]
                ?? throw new InvalidOperationException(
                    $"The embedded AeroCMS documentation corpus entry {index} is null.");
            if (string.IsNullOrWhiteSpace(entry.Title)
                || string.IsNullOrWhiteSpace(entry.FeatureArea)
                || string.IsNullOrWhiteSpace(entry.Maturity)
                || string.IsNullOrWhiteSpace(entry.Content))
            {
                Invalid($"entry {index} is missing required descriptive content");
            }

            if (string.IsNullOrWhiteSpace(entry.CanonicalPath)
                || !entry.CanonicalPath.StartsWith("/", StringComparison.Ordinal)
                || entry.CanonicalPath.Contains("..", StringComparison.Ordinal)
                || !canonicalPaths.Add(entry.CanonicalPath))
            {
                Invalid($"entry {index} has an invalid or duplicate canonical path");
            }

            if (string.IsNullOrWhiteSpace(entry.Audience)
                || !AllowedAudiences.Contains(entry.Audience))
            {
                Invalid($"entry {index} has an unsupported audience");
            }

            if (entry.SourceFiles is null or { Count: 0 }
                || entry.SourceFiles.Any(string.IsNullOrWhiteSpace))
            {
                Invalid($"entry {index} requires source-file provenance");
            }
        }
    }

    [DoesNotReturn]
    private static void Invalid(string detail)
        => throw new InvalidOperationException(
            $"The embedded AeroCMS documentation corpus is invalid: {detail}.");
}
