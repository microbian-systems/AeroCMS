namespace Aero.Cms.Abstractions.Events;

public sealed record CategoryTranslationSaved(
    long CategoryId,
    long TranslationId,
    string Culture,
    DateTimeOffset SavedOn);
