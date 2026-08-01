using Aero.Cms.Abstractions.Pages.Rendering;
using Aero.Core;
using Aero.Core.Railway;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Deterministic registry for explicitly registered full-page rendering strategies.
/// </summary>
public sealed class PageRendererRegistry : IPageRendererRegistry
{
    private readonly IReadOnlyDictionary<string, IPageRenderer> _renderers;

    /// <summary>Creates a registry and fails startup for invalid or duplicate registrations.</summary>
    public PageRendererRegistry(IEnumerable<IPageRenderer> renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);

        var configured = new Dictionary<string, IPageRenderer>(StringComparer.Ordinal);
        var descriptors = new List<PageRendererDescriptor>();
        foreach (var renderer in renderers)
        {
            ArgumentNullException.ThrowIfNull(renderer);
            var id = PageRendererIds.NormalizeOrDefault(renderer.Id.Value);
            var descriptor = NormalizeAndValidateDescriptor(renderer.Descriptor);
            if (!string.Equals(id, descriptor.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Page renderer '{id}' advertises descriptor ID '{descriptor.Id}'.");
            }

            if (!configured.TryAdd(id, renderer))
            {
                throw new InvalidOperationException(
                    $"More than one page renderer is registered for '{id}'.");
            }

            descriptors.Add(descriptor);
        }

        if (!configured.ContainsKey(PageRendererIds.AeroComposition))
        {
            throw new InvalidOperationException(
                $"The required default page renderer '{PageRendererIds.AeroComposition}' is not registered.");
        }

        _renderers = configured;
        Descriptors = descriptors
            .OrderBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<PageRendererDescriptor> Descriptors { get; }

    /// <inheritdoc />
    public Result<IPageRenderer> Resolve(string? rendererId)
    {
        var normalized = PageRendererIds.NormalizeOrDefault(rendererId);
        if (!PageRendererIds.IsValid(normalized))
        {
            return AeroError.ValidationError(["The page renderer identifier is invalid."]);
        }

        return _renderers.TryGetValue(normalized, out var renderer)
            ? new Result<IPageRenderer>.Ok(renderer)
            : new Result<IPageRenderer>.Failure(
                AeroError.ValidationError(
                    [$"No page renderer is registered for '{normalized}'."]));
    }

    private static PageRendererDescriptor NormalizeAndValidateDescriptor(
        PageRendererDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var id = PageRendererIds.NormalizeOrDefault(descriptor.Id);
        if (!PageRendererIds.IsValid(id))
        {
            throw new InvalidOperationException(
                $"Page renderer ID '{descriptor.Id}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.DisplayName)
            || string.IsNullOrWhiteSpace(descriptor.EditorKind))
        {
            throw new InvalidOperationException(
                $"Page renderer '{id}' requires a display name and editor kind.");
        }

        return descriptor with
        {
            Id = id,
            DisplayName = descriptor.DisplayName.Trim(),
            EditorKind = descriptor.EditorKind.Trim(),
            SourceLanguage = string.IsNullOrWhiteSpace(descriptor.SourceLanguage)
                ? null
                : descriptor.SourceLanguage.Trim().ToLowerInvariant()
        };
    }
}
