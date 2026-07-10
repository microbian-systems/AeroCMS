namespace Aero.Cms.Abstractions.Events;

/// <summary>
/// Represents a record for TagTranslationSaved.
/// </summary>
public sealed record TagTranslationSaved(
    long TagId,
    long TranslationId,
    string Culture,
    DateTimeOffset SavedOn);
