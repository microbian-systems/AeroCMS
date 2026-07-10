namespace Aero.Cms.Abstractions.Events;

/// <summary>
/// Represents a record for ProductTranslationSaved.
/// </summary>
public sealed record ProductTranslationSaved(
    long ProductId,
    long TranslationId,
    string Culture,
    DateTimeOffset SavedOn);
