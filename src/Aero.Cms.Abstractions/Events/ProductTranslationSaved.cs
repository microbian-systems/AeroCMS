namespace Aero.Cms.Abstractions.Events;

public sealed record ProductTranslationSaved(
    long ProductId,
    long TranslationId,
    string Culture,
    DateTimeOffset SavedOn);
