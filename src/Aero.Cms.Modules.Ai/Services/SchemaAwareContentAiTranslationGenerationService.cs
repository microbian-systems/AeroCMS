using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Ai;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Content.Localization;
using Aero.Core;
using Aero.Core.Railway;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Aero.Cms.Modules.Ai.Services;

/// <summary>Fails closed until the consuming host supplies a site authorization implementation.</summary>
public sealed class DenyContentTranslationSiteAuthorizer : IContentTranslationSiteAuthorizer
{
    public Task<Result<NoneType, AeroError>> AuthorizeAsync(long siteId, CancellationToken cancellationToken = default)
        => Task.FromResult<Result<NoneType, AeroError>>(AeroError.InvalidRequestError("No site authorization policy is registered for AI translation."));
}

/// <summary>Fails closed until the persistence lane supplies a site-scoped snapshot resolver.</summary>
public sealed class DenyContentAiTranslationSnapshotResolver : IContentAiTranslationSnapshotResolver
{
    public Task<Result<ContentAiTranslationGenerationSnapshot>> ResolveAsync(long siteId, long sourceItemId, long targetItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<Result<ContentAiTranslationGenerationSnapshot>>(AeroError.InvalidRequestError("No trusted content translation snapshot resolver is registered."));
}

/// <summary>Whitelists localized textual fields and produces a non-persisting translation application command.</summary>
public sealed class SchemaAwareContentAiTranslationGenerationService(
    IAiContentTranslationService translationService,
    IEnumerable<IContentTranslationContextContributor> contextContributors,
    IEnumerable<IContentTranslationFieldHandler> fieldHandlers,
    IContentAiTranslationSnapshotResolver snapshotResolver)
    : IContentAiTranslationGenerationService
{
    private const int MaxFields = 40;
    private const int MaxSourceBytes = 200_000;
    private const int MaxContextBytes = 8_000;
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public async Task<Result<GenerateContentAiTranslationResponse>> GenerateAsync(
        GenerateContentAiTranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        var snapshotResult = await snapshotResolver.ResolveAsync(request.SiteId, request.SourceItemId, request.TargetItemId, cancellationToken);
        if (snapshotResult is Result<ContentAiTranslationGenerationSnapshot>.Failure snapshotFailure)
        {
            return snapshotFailure.Error;
        }
        var snapshot = ((Result<ContentAiTranslationGenerationSnapshot>.Ok)snapshotResult).Value;
        var cultures = CanonicalCultures(snapshot.Localization);
        if (cultures is null || !cultures.Contains(snapshot.Source.Culture) || !cultures.Contains(snapshot.Target.Culture)
            || !string.Equals(snapshot.Target.Culture, request.TargetCulture, StringComparison.Ordinal)
            || string.Equals(snapshot.Source.Culture, snapshot.Target.Culture, StringComparison.Ordinal))
        {
            return AeroError.ValidationError(["Source and target cultures must be distinct canonical supported cultures."]);
        }

        if (request.SiteId <= 0 || snapshot.ContentType.SiteId != request.SiteId || snapshot.Localization.SiteId != request.SiteId
            || snapshot.Source.SiteId != request.SiteId || snapshot.Target.SiteId != request.SiteId
            || !string.Equals(snapshot.Source.ContentTypeAlias, snapshot.ContentType.Alias, StringComparison.Ordinal)
            || !string.Equals(snapshot.Target.ContentTypeAlias, snapshot.ContentType.Alias, StringComparison.Ordinal)
            || snapshot.Source.TranslationGroupId <= 0 || snapshot.Source.TranslationGroupId != snapshot.Target.TranslationGroupId
            || snapshot.Source.ContentItemId != request.SourceItemId || snapshot.Source.VersionNumber != request.SourceVersionNumber
            || snapshot.Target.ContentItemId != request.TargetItemId || snapshot.Target.VersionNumber != request.ExpectedTargetVersionNumber)
        {
            return AeroError.ValidationError(["Site, item, and version metadata must be positive and site-scoped."]);
        }

        var warnings = new List<string>();
        var fields = new List<TranslateDocumentField>();
        var handlers = fieldHandlers.ToDictionary(handler => handler.FieldType, StringComparer.OrdinalIgnoreCase);
        var selectedHandlers = new Dictionary<string, IContentTranslationFieldHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in snapshot.ContentType.Fields)
        {
            if (field.LocalizationMode != ContentFieldLocalizationMode.Localized || !snapshot.Source.Fields.TryGetValue(field.Name, out var value))
            {
                continue;
            }

            if (handlers.TryGetValue(field.FieldType, out var handler) && handler.TryCreate(field, value, out var translationField))
            {
                fields.Add(translationField);
                selectedHandlers[field.Name] = handler;
            }
            else
            {
                warnings.Add($"Localized field '{field.Name}' of type '{field.FieldType}' is unsupported or non-textual and was not sent to AI.");
            }
        }

        if (fields.Count == 0)
        {
            return AeroError.ValidationError(["The schema contains no supported localized fields to translate."]);
        }
        if (fields.Count > MaxFields || fields.Sum(field => System.Text.Encoding.UTF8.GetByteCount(field.SourceText)) > MaxSourceBytes)
        {
            return AeroError.ValidationError(["Translation request exceeds the field or source-byte limit."]);
        }

        var contextBytes = 0;
        var context = new List<ContentTranslationPromptContext>();
        foreach (var contributor in contextContributors)
        {
            var contribution = await contributor.ContributeAsync(
                new ContentTranslationContextRequest(request.SiteId, snapshot.Source.ContentItemId,
                    snapshot.Source.TranslationGroupId, snapshot.ContentType.Alias, snapshot.Source.Culture,
                    snapshot.Target.Culture, snapshot.Source.Fields), cancellationToken);
            if (contribution is Result<IReadOnlyList<ContentTranslationContextContribution>>.Failure failure)
            {
                return failure.Error;
            }
            var items = ((Result<IReadOnlyList<ContentTranslationContextContribution>>.Ok)contribution).Value;
            contextBytes += items.Sum(item => System.Text.Encoding.UTF8.GetByteCount(item.Key) + System.Text.Encoding.UTF8.GetByteCount(item.Value));
            if (contextBytes > MaxContextBytes)
            {
                return AeroError.ValidationError(["Translation context exceeds the byte limit."]);
            }
            context.AddRange(items.Select(item => new ContentTranslationPromptContext(item.Key, item.Value)));
        }

        var translated = await translationService.TranslateAsync(new TranslateDocumentRequest(fields, snapshot.Source.Culture, request.TargetCulture, request.ProviderId, context), cancellationToken);
        if (translated is Result<TranslateDocumentResponse>.Failure providerFailure)
        {
            return providerFailure.Error;
        }
        var providerResponse = ((Result<TranslateDocumentResponse>.Ok)translated).Value;
        warnings.AddRange(providerResponse.Warnings);
        var output = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var value = providerResponse.TranslatedFields.TryGetValue(field.Key, out var translatedValue) ? translatedValue : field.SourceText;
            if (!selectedHandlers[field.Key].IsSafeResult(field.SourceText, value))
            {
                warnings.Add($"AI translation for rich-text field '{field.Key}' did not preserve markup and was left unchanged.");
                value = field.SourceText;
            }
            output[field.Key] = JsonSerializer.SerializeToElement(value);
        }

        return new GenerateContentAiTranslationResponse(
            new ApplyContentAiTranslationCommand(snapshot.Source.ContentItemId, snapshot.Source.VersionNumber,
                snapshot.Target.ContentItemId, snapshot.Target.VersionNumber, snapshot.Source.Culture,
                request.TargetCulture, output, providerResponse.ProviderId, providerResponse.Model), warnings);
    }

    private static HashSet<string>? CanonicalCultures(ContentLocalizationContext context)
    {
        try
        {
            var supported = context.SupportedCultures.Select(value => CultureInfo.GetCultureInfo(value).Name).ToHashSet(StringComparer.Ordinal);
            return supported.Contains(CultureInfo.GetCultureInfo(context.DefaultCulture).Name) ? supported : null;
        }
        catch (CultureNotFoundException) { return null; }
    }

    internal static bool PreservesMarkup(string source, string translated)
    {
        if (!IsSafeMarkup(source) || !IsSafeMarkup(translated)) return false;
        return MarkdownFingerprint(source).SequenceEqual(MarkdownFingerprint(translated), StringComparer.Ordinal);
    }

    private static bool IsSafeMarkup(string value)
    {
        if (value.Contains("<script", StringComparison.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(value, "\\son[a-z]+\\s*=", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(value, "(?:href|src)\\s*=\\s*['\"]?\\s*(?:javascript|data|vbscript):", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(value, "\\[[^]]*\\]\\(\\s*(?:javascript|data|vbscript):", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return false;
        var stack = new Stack<string>();
        foreach (var tag in Tags(value))
        {
            if (tag.StartsWith("</", StringComparison.Ordinal))
            {
                if (stack.Count == 0 || !string.Equals(stack.Pop(), tag[2..^1], StringComparison.OrdinalIgnoreCase)) return false;
            }
            else if (!tag.EndsWith("/>", StringComparison.Ordinal) && !IsVoidTag(tag)) stack.Push(TagName(tag));
        }
        return stack.Count == 0;
    }

    private static IEnumerable<string> Tags(string value) => System.Text.RegularExpressions.Regex.Matches(value, "<\\/?[a-zA-Z][^>]*>")
        .Select(match => System.Text.RegularExpressions.Regex.Replace(match.Value, "\\s+", " ").Trim());
    private static IEnumerable<string> MarkdownFingerprint(string value)
    {
        var frontMatter = FrontMatter(value);
        if (frontMatter is not null) yield return $"frontmatter:{frontMatter}";
        var document = Markdown.Parse(value, MarkdownPipeline);
        foreach (var token in MarkdownBlockTokens(document))
        {
            yield return token;
        }
    }

    private static IEnumerable<string> MarkdownBlockTokens(ContainerBlock container)
    {
        foreach (var block in container)
        {
            yield return $"block-open:{block.GetType().FullName}:{BlockMetadata(block)}";
            if (block is ContainerBlock childContainer)
            {
                foreach (var token in MarkdownBlockTokens(childContainer)) yield return token;
            }
            if (block is LeafBlock { Inline: { } inline })
            {
                foreach (var token in MarkdownInlineTokens(inline)) yield return token;
            }
            yield return $"block-close:{block.GetType().FullName}";
        }
    }

    private static IEnumerable<string> MarkdownInlineTokens(ContainerInline container)
    {
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            yield return $"inline-open:{inline.GetType().FullName}:{InlineMetadata(inline)}";
            if (inline is ContainerInline childContainer)
            {
                foreach (var token in MarkdownInlineTokens(childContainer)) yield return token;
            }
            yield return $"inline-close:{inline.GetType().FullName}";
        }
    }

    private static string InlineMetadata(Inline inline) => inline switch
    {
        LinkInline link => $"link:{link.Url}:{link.Title}:{link.IsImage}",
        CodeInline code => $"inlinecode:{code.Content}",
        EmphasisInline emphasis => $"emphasis:{emphasis.DelimiterChar}:{emphasis.DelimiterCount}",
        HtmlInline html => $"htmlinline:{html.Tag}",
        _ => string.Empty
    };

    private static string BlockMetadata(Block block) => block switch
    {
        HeadingBlock heading => $"heading:{heading.Level}",
        ListBlock list => $"list:{list.IsOrdered}:{list.BulletType}:{list.OrderedStart}:{list.OrderedDelimiter}",
        FencedCodeBlock fenced => $"fence:{fenced.Info}:{fenced.Lines}",
        CodeBlock code => $"code:{code.Lines}",
        Table table => $"table:{string.Join(',', table.ColumnDefinitions?.Select(column => $"{column.Alignment}:{column.Width}") ?? [])}",
        HtmlBlock html => $"htmlblock:{html.Lines}",
        _ => string.Empty
    };
    private static string? FrontMatter(string value)
    {
        var match = System.Text.RegularExpressions.Regex.Match(value, "\\A---\\r?\\n(?<front>.*?)\\r?\\n---\\r?\\n", System.Text.RegularExpressions.RegexOptions.Singleline);
        return match.Success ? match.Groups["front"].Value : null;
    }
    private static string TagName(string tag) => tag.Trim('<', '>', '/', ' ').Split(' ', 2)[0];
    private static bool IsVoidTag(string tag) => TagName(tag).ToLowerInvariant() is "br" or "hr" or "img" or "meta" or "link" or "input";
}
