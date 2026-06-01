namespace Aero.Cms.Shared.Localization;

/// <summary>
/// Marker class for Manager UI shared resource localization.
/// Used with <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> to resolve
/// localized Manager interface strings from .resx files.
/// </summary>
/// <remarks>
/// Uses the standard ASP.NET Core convention: English strings are used directly as keys.
/// No English .resx file is needed — the key itself is the fallback value.
/// Translators create culture-specific .resx files (e.g., ManagerResource.es.resx, ManagerResource.fr.resx)
/// containing translations for each key.
/// </remarks>
public sealed class ManagerResource
{
}
