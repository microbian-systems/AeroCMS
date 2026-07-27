namespace Aero.Cms.Modules.Ai.Knowledge;

internal static class AeroAiKnowledgeChunker
{
    public static IReadOnlyList<string> Chunk(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (normalized.Length <= AeroAiKnowledgeConstants.MaximumChunkCharacters)
            return [normalized];

        var chunks = new List<string>();
        var offset = 0;
        while (offset < normalized.Length
               && chunks.Count < AeroAiKnowledgeConstants.MaximumChunksPerSource)
        {
            var remaining = normalized.Length - offset;
            var length = Math.Min(AeroAiKnowledgeConstants.MaximumChunkCharacters, remaining);
            if (length < remaining)
            {
                var candidate = normalized.AsSpan(offset, length);
                var paragraphBreak = candidate.LastIndexOf("\n\n".AsSpan());
                var lineBreak = candidate.LastIndexOf('\n');
                var whitespace = candidate.LastIndexOf(' ');
                var split = paragraphBreak >= length / 2
                    ? paragraphBreak + 2
                    : lineBreak >= length / 2
                        ? lineBreak + 1
                        : whitespace >= length / 2
                            ? whitespace + 1
                            : length;
                length = split;
            }

            var chunk = normalized.Substring(offset, length).Trim();
            if (chunk.Length > 0)
                chunks.Add(chunk);

            if (offset + length >= normalized.Length)
                break;

            offset += Math.Max(
                1,
                length - AeroAiKnowledgeConstants.ChunkOverlapCharacters);
        }

        return chunks;
    }
}
