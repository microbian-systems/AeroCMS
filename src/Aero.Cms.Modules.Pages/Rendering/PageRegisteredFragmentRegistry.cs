using System.Net;
using System.Text.Json;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Core;
using Aero.Core.Railway;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Explicit rendering contract for a developer-registered page fragment.</summary>
public interface IPageRegisteredFragmentProvider
{
    PageRegisteredFragmentDescriptor Descriptor { get; }

    Task<Result<string>> RenderAsync(
        PageRegisteredFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolves, validates, and renders explicitly registered page fragments.</summary>
public interface IPageRegisteredFragmentRegistry
{
    IReadOnlyList<PageRegisteredFragmentDescriptor> Descriptors { get; }

    bool TryGetDescriptor(string key, out PageRegisteredFragmentDescriptor? descriptor);

    Result<PageRegisteredFragment> Validate(PageRegisteredFragment fragment);

    Task<Result<HtmlPageContent>> RenderAsync(
        PageRegisteredFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Registers one provider explicitly; no assembly or filesystem discovery occurs.</summary>
public static class PageRegisteredFragmentServiceCollectionExtensions
{
    public static IServiceCollection AddPageRegisteredFragment<TProvider>(this IServiceCollection services)
        where TProvider : class, IPageRegisteredFragmentProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPageRegisteredFragmentProvider, TProvider>());
        return services;
    }
}

/// <summary>Deterministic provider registry with schema and output policy enforcement.</summary>
public sealed class PageRegisteredFragmentRegistry : IPageRegisteredFragmentRegistry
{
    public const int MaximumRenderedMarkupLength = 64 * 1024;

    private readonly IHtmlFragmentImporter _importer;
    private readonly IReadOnlyDictionary<string, ProviderEntry> _providers;

    public PageRegisteredFragmentRegistry(
        IEnumerable<IPageRegisteredFragmentProvider> providers,
        IHtmlFragmentImporter importer)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _importer = importer ?? throw new ArgumentNullException(nameof(importer));

        var configured = new Dictionary<string, ProviderEntry>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            var descriptor = NormalizeAndValidateDescriptor(provider.Descriptor);
            if (!configured.TryAdd(descriptor.Key, new ProviderEntry(provider, descriptor)))
            {
                throw new InvalidOperationException(
                    $"More than one registered page-fragment provider uses key '{descriptor.Key}'.");
            }
        }

        _providers = configured;
        Descriptors = configured.Values
            .Select(entry => entry.Descriptor)
            .OrderBy(descriptor => descriptor.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.Key, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<PageRegisteredFragmentDescriptor> Descriptors { get; }

    public bool TryGetDescriptor(string key, out PageRegisteredFragmentDescriptor? descriptor)
    {
        if (_providers.TryGetValue(PageRegisteredFragment.NormalizeKey(key), out var entry))
        {
            descriptor = entry.Descriptor;
            return true;
        }

        descriptor = null;
        return false;
    }

    public Result<PageRegisteredFragment> Validate(PageRegisteredFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var normalizedKey = PageRegisteredFragment.NormalizeKey(fragment.Key);
        if (!_providers.TryGetValue(normalizedKey, out var entry))
        {
            return AeroError.ValidationError(
                [$"No registered page-fragment provider exists for key '{normalizedKey}'."]);
        }

        return ValidateParameters(fragment with { Key = normalizedKey }, entry.Descriptor, validateRequired: true);
    }

    public async Task<Result<HtmlPageContent>> RenderAsync(
        PageRegisteredFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default)
    {
        var validated = Validate(fragment);
        if (validated is Result<PageRegisteredFragment>.Failure validationFailure)
        {
            return validationFailure.Error;
        }

        var normalized = ((Result<PageRegisteredFragment>.Ok)validated).Value;
        var provider = _providers[normalized.Key].Provider;
        var rendered = await provider.RenderAsync(normalized, context, cancellationToken);
        if (rendered is Result<string>.Failure renderFailure)
        {
            return renderFailure.Error;
        }

        var markup = ((Result<string>.Ok)rendered).Value ?? string.Empty;
        if (markup.Length > MaximumRenderedMarkupLength)
        {
            return AeroError.ValidationError(
                [$"Registered page fragment '{normalized.Key}' exceeded the rendered output limit."]);
        }

        return _importer.Import(markup);
    }

    private static PageRegisteredFragmentDescriptor NormalizeAndValidateDescriptor(
        PageRegisteredFragmentDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var key = PageRegisteredFragment.NormalizeKey(descriptor.Key);
        if (!PageRegisteredFragment.IsValidKey(key))
        {
            throw new InvalidOperationException(
                $"Registered page-fragment key '{descriptor.Key}' is invalid.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
        {
            throw new InvalidOperationException($"Registered page fragment '{key}' requires a display name.");
        }

        var parameters = descriptor.Parameters ?? [];
        if (parameters.Count > PageRegisteredFragment.MaximumParameterCount)
        {
            throw new InvalidOperationException(
                $"Registered page fragment '{key}' exposes too many parameters.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name)
                || parameter.Name.Length > PageRegisteredFragment.MaximumParameterNameLength
                || !names.Add(parameter.Name))
            {
                throw new InvalidOperationException(
                    $"Registered page fragment '{key}' contains an invalid or duplicate parameter name.");
            }

            if (!Enum.IsDefined(parameter.Kind)
                || parameter.MaximumLength is < 0
                || (parameter.Minimum is { } minimum
                    && parameter.Maximum is { } maximum
                    && minimum > maximum)
                || (parameter.Kind == PageRegisteredFragmentParameterKind.Enum
                    && (parameter.Choices?.Count ?? 0) == 0))
            {
                throw new InvalidOperationException(
                    $"Registered page fragment '{key}' contains an invalid schema for '{parameter.Name}'.");
            }
        }

        var normalized = descriptor with
        {
            Key = key,
            Category = string.IsNullOrWhiteSpace(descriptor.Category) ? "Registered" : descriptor.Category.Trim(),
            Parameters = parameters.ToArray()
        };

        var probe = new PageRegisteredFragment { Key = key };
        var defaults = ValidateParameters(probe, normalized, validateRequired: false);
        if (defaults is Result<PageRegisteredFragment>.Failure defaultsFailure)
        {
            throw new InvalidOperationException(
                $"Registered page fragment '{key}' has an invalid default schema: {defaultsFailure.Error}");
        }

        return normalized;
    }

    private static Result<PageRegisteredFragment> ValidateParameters(
        PageRegisteredFragment fragment,
        PageRegisteredFragmentDescriptor descriptor,
        bool validateRequired)
    {
        var supplied = fragment.Parameters ?? new Dictionary<string, JsonElement>();
        var errors = new List<string>();
        if (supplied.Count > PageRegisteredFragment.MaximumParameterCount)
        {
            errors.Add($"Registered page fragment '{descriptor.Key}' contains too many parameters.");
        }

        foreach (var name in supplied.Keys)
        {
            if (string.IsNullOrWhiteSpace(name)
                || name.Length > PageRegisteredFragment.MaximumParameterNameLength
                || !descriptor.Parameters.Any(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal)))
            {
                errors.Add($"Parameter '{name}' is not declared by registered page fragment '{descriptor.Key}'.");
            }
        }

        var normalized = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var parameter in descriptor.Parameters)
        {
            JsonElement value;
            if (supplied.TryGetValue(parameter.Name, out var suppliedValue))
            {
                value = suppliedValue.Clone();
            }
            else if (parameter.DefaultValue is { } defaultValue)
            {
                value = defaultValue.Clone();
            }
            else
            {
                if (parameter.Required && validateRequired)
                {
                    errors.Add($"Parameter '{parameter.Name}' is required by registered page fragment '{descriptor.Key}'.");
                }

                continue;
            }

            var parameterError = ValidateParameterValue(parameter, value);
            if (parameterError is not null)
            {
                errors.Add(parameterError);
            }
            else
            {
                normalized[parameter.Name] = value;
            }
        }

        if (JsonSerializer.SerializeToUtf8Bytes(normalized).Length
            > PageRegisteredFragment.MaximumParametersUtf8Bytes)
        {
            errors.Add($"Registered page fragment '{descriptor.Key}' parameters exceed the 16 KiB limit.");
        }

        return errors.Count > 0
            ? AeroError.ValidationError(errors)
            : fragment with { Key = descriptor.Key, Parameters = normalized };
    }

    private static string? ValidateParameterValue(
        PageRegisteredFragmentParameterDescriptor parameter,
        JsonElement value)
    {
        decimal? numeric = null;
        switch (parameter.Kind)
        {
            case PageRegisteredFragmentParameterKind.String:
                if (value.ValueKind != JsonValueKind.String)
                {
                    return $"Parameter '{parameter.Name}' must be a string.";
                }

                if (parameter.MaximumLength is { } maximumLength
                    && (value.GetString()?.Length ?? 0) > maximumLength)
                {
                    return $"Parameter '{parameter.Name}' cannot exceed {maximumLength} characters.";
                }
                break;
            case PageRegisteredFragmentParameterKind.Integer:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var integer))
                {
                    return $"Parameter '{parameter.Name}' must be an integer.";
                }
                numeric = integer;
                break;
            case PageRegisteredFragmentParameterKind.Decimal:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
                {
                    return $"Parameter '{parameter.Name}' must be a decimal number.";
                }
                numeric = number;
                break;
            case PageRegisteredFragmentParameterKind.Boolean:
                if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                {
                    return $"Parameter '{parameter.Name}' must be a boolean.";
                }
                break;
            case PageRegisteredFragmentParameterKind.Enum:
                var selected = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (selected is null
                    || !(parameter.Choices ?? []).Contains(selected, StringComparer.Ordinal))
                {
                    return $"Parameter '{parameter.Name}' must be one of the declared choices.";
                }
                break;
            default:
                return $"Parameter '{parameter.Name}' has an unsupported type.";
        }

        if (numeric is { } numericValue
            && (parameter.Minimum is { } minimum && numericValue < minimum
                || parameter.Maximum is { } maximum && numericValue > maximum))
        {
            return $"Parameter '{parameter.Name}' is outside its declared range.";
        }

        return null;
    }

    private sealed record ProviderEntry(
        IPageRegisteredFragmentProvider Provider,
        PageRegisteredFragmentDescriptor Descriptor);
}

/// <summary>One safe code-backed slot proving the registered-fragment vertical.</summary>
public sealed class SiteNoticePageRegisteredFragmentProvider : IPageRegisteredFragmentProvider
{
    public PageRegisteredFragmentDescriptor Descriptor { get; } = new()
    {
        Key = "core.site-notice",
        DisplayName = "Site notice",
        Description = "A semantic notice supplied by an explicitly registered Pages provider.",
        Category = "Application",
        Parameters =
        [
            new PageRegisteredFragmentParameterDescriptor
            {
                Name = "message",
                DisplayName = "Message",
                Kind = PageRegisteredFragmentParameterKind.String,
                Required = true,
                MaximumLength = 240,
                DefaultValue = JsonSerializer.SerializeToElement("Important information")
            },
            new PageRegisteredFragmentParameterDescriptor
            {
                Name = "tone",
                DisplayName = "Tone",
                Kind = PageRegisteredFragmentParameterKind.Enum,
                Choices = ["info", "success", "warning"],
                DefaultValue = JsonSerializer.SerializeToElement("info")
            },
            new PageRegisteredFragmentParameterDescriptor
            {
                Name = "dismissible",
                DisplayName = "Dismissible",
                Kind = PageRegisteredFragmentParameterKind.Boolean,
                DefaultValue = JsonSerializer.SerializeToElement(false)
            }
        ]
    };

    public Task<Result<string>> RenderAsync(
        PageRegisteredFragment fragment,
        PageFragmentRenderContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var message = fragment.Parameters["message"].GetString() ?? string.Empty;
        var tone = fragment.Parameters["tone"].GetString() ?? "info";
        var suffix = fragment.Parameters["dismissible"].GetBoolean()
            ? " This notice may be dismissed."
            : string.Empty;
        var markup = $"<aside><p><strong>{WebUtility.HtmlEncode(tone)}:</strong> "
            + $"{WebUtility.HtmlEncode(message + suffix)}</p></aside>";
        return Task.FromResult<Result<string>>(markup);
    }
}
