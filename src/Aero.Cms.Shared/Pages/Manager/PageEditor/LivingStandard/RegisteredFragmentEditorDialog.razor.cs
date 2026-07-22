using System.Globalization;
using System.Text.Json;
using Aero.Cms.Abstractions.Pages.Composition;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Aero.Cms.Shared.Pages.Manager.PageEditor.LivingStandard;

/// <summary>Edits only scalar values declared by one registered-fragment descriptor.</summary>
public partial class RegisteredFragmentEditorDialog
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private string? _localError;

    [Parameter, EditorRequired]
    public PageRegisteredFragmentDescriptor Descriptor { get; set; } = new();

    [Parameter, EditorRequired]
    public PageRegisteredFragment Fragment { get; set; } = new();

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyDictionary<string, JsonElement>> ParametersSaved { get; set; }

    [Parameter]
    public EventCallback Closed { get; set; }

    private string? DisplayError => _localError ?? ErrorMessage;

    protected override void OnInitialized()
    {
        foreach (var parameter in Descriptor.Parameters)
        {
            if (Fragment.Parameters.TryGetValue(parameter.Name, out var current))
            {
                _values[parameter.Name] = ToEditorText(current);
            }
            else if (parameter.DefaultValue is { } defaultValue)
            {
                _values[parameter.Name] = ToEditorText(defaultValue);
            }
            else
            {
                _values[parameter.Name] = parameter.Kind == PageRegisteredFragmentParameterKind.Boolean
                    ? "false"
                    : parameter.Kind == PageRegisteredFragmentParameterKind.Enum
                        ? parameter.Choices.FirstOrDefault() ?? string.Empty
                        : string.Empty;
            }
        }
    }

    protected string FieldId(PageRegisteredFragmentParameterDescriptor parameter)
        => $"aero-registered-{Fragment.NodeId}-{parameter.Name}";

    protected string GetText(PageRegisteredFragmentParameterDescriptor parameter)
        => _values.TryGetValue(parameter.Name, out var value) ? value : string.Empty;

    protected bool GetBoolean(PageRegisteredFragmentParameterDescriptor parameter)
        => bool.TryParse(GetText(parameter), out var value) && value;

    protected void SetText(PageRegisteredFragmentParameterDescriptor parameter, ChangeEventArgs args)
        => _values[parameter.Name] = args.Value?.ToString() ?? string.Empty;

    protected void SetBoolean(PageRegisteredFragmentParameterDescriptor parameter, ChangeEventArgs args)
        => _values[parameter.Name] = args.Value is bool value && value ? "true" : "false";

    protected async Task SaveAsync()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var descriptor in Descriptor.Parameters)
        {
            var text = GetText(descriptor);
            if (descriptor.Required && string.IsNullOrWhiteSpace(text))
            {
                _localError = $"{descriptor.DisplayName} is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(text)
                && descriptor.Kind is not PageRegisteredFragmentParameterKind.String)
            {
                continue;
            }

            if (!TryCreateValue(descriptor, text, out var value, out var error))
            {
                _localError = error;
                return;
            }

            parameters[descriptor.Name] = value;
        }

        if (JsonSerializer.SerializeToUtf8Bytes(parameters).Length
            > PageRegisteredFragment.MaximumParametersUtf8Bytes)
        {
            _localError = "Parameters exceed the 16 KiB limit.";
            return;
        }

        _localError = null;
        await ParametersSaved.InvokeAsync(parameters);
    }

    protected Task CloseAsync() => Closed.InvokeAsync();

    protected Task HandleKeyDownAsync(KeyboardEventArgs args)
        => args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    private static bool TryCreateValue(
        PageRegisteredFragmentParameterDescriptor descriptor,
        string text,
        out JsonElement value,
        out string? error)
    {
        error = null;
        value = default;
        switch (descriptor.Kind)
        {
            case PageRegisteredFragmentParameterKind.String:
                if (descriptor.MaximumLength is { } maximumLength && text.Length > maximumLength)
                {
                    error = $"{descriptor.DisplayName} cannot exceed {maximumLength} characters.";
                    return false;
                }
                value = JsonSerializer.SerializeToElement(text);
                return true;
            case PageRegisteredFragmentParameterKind.Integer:
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    error = $"{descriptor.DisplayName} must be an integer.";
                    return false;
                }
                return TryCreateNumber(descriptor, integer, out value, out error);
            case PageRegisteredFragmentParameterKind.Decimal:
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    error = $"{descriptor.DisplayName} must be a decimal number.";
                    return false;
                }
                return TryCreateNumber(descriptor, number, out value, out error);
            case PageRegisteredFragmentParameterKind.Boolean:
                value = JsonSerializer.SerializeToElement(bool.TryParse(text, out var boolean) && boolean);
                return true;
            case PageRegisteredFragmentParameterKind.Enum:
                if (!descriptor.Choices.Contains(text, StringComparer.Ordinal))
                {
                    error = $"{descriptor.DisplayName} must use one of the available choices.";
                    return false;
                }
                value = JsonSerializer.SerializeToElement(text);
                return true;
            default:
                error = $"{descriptor.DisplayName} has an unsupported parameter type.";
                return false;
        }
    }

    private static bool TryCreateNumber<T>(
        PageRegisteredFragmentParameterDescriptor descriptor,
        T number,
        out JsonElement value,
        out string? error)
        where T : struct, IConvertible
    {
        var comparable = Convert.ToDecimal(number, CultureInfo.InvariantCulture);
        if (descriptor.Minimum is { } minimum && comparable < minimum
            || descriptor.Maximum is { } maximum && comparable > maximum)
        {
            value = default;
            error = $"{descriptor.DisplayName} is outside its allowed range.";
            return false;
        }

        value = JsonSerializer.SerializeToElement(number);
        error = null;
        return true;
    }

    private static string ToEditorText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => value.GetRawText(),
        _ => string.Empty
    };
}
