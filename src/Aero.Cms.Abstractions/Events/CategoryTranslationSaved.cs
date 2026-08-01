namespace Aero.Cms.Abstractions.Events;

/// <summary>
/// Represents a record for CategoryTranslationSaved.
/// </summary>
public sealed record CategoryTranslationSaved(
    long CategoryId,
    long TranslationId,
    string Culture,
    DateTimeOffset SavedOn);
