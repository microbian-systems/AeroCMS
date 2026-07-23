using Aero.Core;
using Aero.Core.Railway;
using Markdig;

namespace Aero.Cms.Html;

/// <summary>
/// Markdig-based Markdown interchange with the existing HTML importer as the
/// authoritative catalog, attribute, URL, nesting, and resource-policy boundary.
/// </summary>
public sealed class MarkdownInterchangeAdapter : IMarkdownInterchangeAdapter
{
    private static readonly MarkdownPipeline ImportPipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseEmphasisExtras()
        .UsePipeTables()
        .Build();

    private readonly IHtmlFragmentImporter _htmlImporter;
    private readonly MarkdownTreeExporter _exporter;
    private readonly MarkdownInterchangeLimits _limits;

    /// <summary>Creates an interchange adapter with bounded intermediate representations.</summary>
    /// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A supplied interchange limit is not positive.</exception>
    public MarkdownInterchangeAdapter(
        IHtmlFragmentImporter htmlImporter,
        IHtmlContentValidator contentValidator,
        MarkdownInterchangeLimits? limits = null)
    {
        _htmlImporter = htmlImporter ?? throw new ArgumentNullException(nameof(htmlImporter));
        _limits = limits ?? new MarkdownInterchangeLimits();
        _exporter = new MarkdownTreeExporter(
            contentValidator ?? throw new ArgumentNullException(nameof(contentValidator)),
            _limits);

        if (_limits.MaximumMarkdownLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "Maximum Markdown length must be positive.");
        }

        if (_limits.MaximumGeneratedHtmlLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "Maximum generated HTML length must be positive.");
        }

        if (_limits.MaximumExportLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "Maximum export length must be positive.");
        }
    }

    /// <inheritdoc />
    public Result<HtmlPageContent> Import(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        if (string.IsNullOrWhiteSpace(markdown))
        {
            return AeroError.ValidationError(["Markdown content cannot be empty."]);
        }

        if (markdown.Length > _limits.MaximumMarkdownLength)
        {
            return AeroError.ValidationError(
                [$"Markdown content exceeds the maximum length of {_limits.MaximumMarkdownLength} characters."]);
        }

        try
        {
            var html = Markdown.ToHtml(markdown, ImportPipeline);
            if (html.Length > _limits.MaximumGeneratedHtmlLength)
            {
                return AeroError.ValidationError(
                    [$"Generated HTML exceeds the maximum length of {_limits.MaximumGeneratedHtmlLength} characters."]);
            }

            return _htmlImporter.Import(html);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return AeroError.ValidationError(["Markdown content could not be converted safely."]);
        }
    }

    /// <inheritdoc />
    public Result<string> Export(HtmlPageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Root.Children.Count == 0)
        {
            return AeroError.ValidationError(
                ["An empty page has no Markdown content to export."]);
        }

        var exported = _exporter.Export(content);
        if (exported is not Result<string>.Ok ok)
        {
            return exported;
        }

        var verified = Import(ok.Value);
        if (verified is not Result<HtmlPageContent>.Ok imported
            || !AreEquivalent(content.Root, imported.Value.Root))
        {
            return AeroError.ValidationError(
                ["The page contains content that cannot be represented losslessly in Markdown."]);
        }

        return exported;
    }

    /// <summary>
    /// Compares the persisted semantic tree while intentionally ignoring regenerated editor identities.
    /// </summary>
    private static bool AreEquivalent(HtmlNode left, HtmlNode right)
    {
        if (left.Kind != right.Kind
            || !string.Equals(left.TagName, right.TagName, StringComparison.Ordinal)
            || !string.Equals(left.Text, right.Text, StringComparison.Ordinal)
            || left.Attributes.Count != right.Attributes.Count
            || left.ThemeClasses.Count != right.ThemeClasses.Count
            || (left.Style is null) != (right.Style is null)
            || left.Children.Count != right.Children.Count)
        {
            return false;
        }

        foreach (var attribute in left.Attributes)
        {
            if (!right.Attributes.TryGetValue(attribute.Key, out var value)
                || !string.Equals(attribute.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!left.ThemeClasses.SequenceEqual(right.ThemeClasses, StringComparer.Ordinal))
        {
            return false;
        }

        for (var index = 0; index < left.Children.Count; index++)
        {
            if (!AreEquivalent(left.Children[index], right.Children[index]))
            {
                return false;
            }
        }

        return true;
    }
}
