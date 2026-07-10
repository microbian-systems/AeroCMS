using Microsoft.Extensions.Localization;
using NeoUI.Blazor;

namespace Aero.Cms.Shared.Localization;

/// <summary>
/// Bridges NeoUI's <see cref="ILocalizer"/> to ASP.NET Core's <see cref="IStringLocalizer{T}"/>.
/// Resolves keys from <see cref="NeoUiSharedResource"/> .resx files first,
/// falling back to <see cref="DefaultLocalizer"/> (English hardcoded defaults) when
/// no translation is found for the current culture.
/// </summary>
public sealed class NeoUiBridgeLocalizer : DefaultLocalizer, NeoUI.Blazor.ILocalizer
{
    private readonly IStringLocalizer<NeoUiSharedResource> _localizer;

        /// <summary>
    /// Initializes a new instance of the <see cref="NeoUiBridgeLocalizer"/> class.
    /// </summary>
public NeoUiBridgeLocalizer(IStringLocalizer<NeoUiSharedResource> localizer)
    {
        _localizer = localizer;
    }

    /// <inheritdoc />
    public override string this[string key]
    {
        get
        {
            var result = _localizer[key];
            return result.ResourceNotFound ? base[key] : result.Value;
        }
    }

    /// <inheritdoc />
    public override string this[string key, params object[] arguments]
    {
        get
        {
            var result = _localizer[key, arguments];
            return result.ResourceNotFound ? base[key, arguments] : result.Value;
        }
    }
}
