namespace Aero.Cms.Abstractions.Events;

public sealed record TagTranslationSaved(
    long TagId,
    long TranslationId,
    string Culture,
    DateTimeOffset SavedOn);
