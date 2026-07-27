using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Aero.Cms.Abstractions.Ai.Knowledge;

namespace Aero.Cms.Modules.Ai.Knowledge;

/// <summary>Searches the curated, build-embedded AeroCMS product documentation corpus.</summary>
public interface IAeroDocumentationKnowledgeSource
{
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

    private readonly Lazy<IReadOnlyList<DocumentationChunk>> _chunks =
        new(LoadChunks, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<AeroAiKnowledgeMatch> Search(string query, int take)
    {
        if (string.IsNullOrWhiteSpace(query) || take <= 0)
            return [];

        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return [];

        return _chunks.Value
            .Select(chunk => new { Chunk = chunk, Score = Score(chunk, tokens) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Chunk.Title, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Chunk.Section, StringComparer.Ordinal)
            .Take(Math.Clamp(take, 1, AeroAiKnowledgeConstants.MaximumTake))
            .Select(candidate => candidate.Chunk.Match)
            .ToArray();
    }

    private static IReadOnlyList<DocumentationChunk> LoadChunks()
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

        var sourceRevision = StableId(corpus.LastVerifiedCommit);
        var chunks = new List<DocumentationChunk>();
        foreach (var entry in corpus.Entries)
        {
            var sourceId = StableId(entry.CanonicalPath);
            foreach (var section in SplitSections(entry.Content))
            {
                var chunkRevision = 0;
                foreach (var content in AeroAiKnowledgeChunker.Chunk(section.Content))
                {
                    var contentHash = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(content)));
                    var chunkId = StableId(
                        $"{entry.CanonicalPath}|{section.Name}|{chunkRevision}|{contentHash}");
                    var match = new AeroAiKnowledgeMatch(
                        chunkId,
                        AeroAiKnowledgeSourceKinds.AeroDocumentation,
                        sourceId,
                        entry.CanonicalPath,
                        "en-US",
                        entry.Title,
                        section.Name,
                        content,
                        sourceRevision,
                        chunkRevision++,
                        contentHash);
                    chunks.Add(new DocumentationChunk(
                        entry.Title,
                        entry.FeatureArea,
                        section.Name,
                        content,
                        match));
                }
            }
        }

        return chunks;
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
        DocumentationChunk chunk,
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

    private sealed record DocumentationChunk(
        string Title,
        string FeatureArea,
        string Section,
        string Content,
        AeroAiKnowledgeMatch Match);
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
