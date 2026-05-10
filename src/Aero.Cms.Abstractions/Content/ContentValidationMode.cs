namespace Aero.Cms.Abstractions.Content;

public enum ContentValidationMode
{
    /// <summary>Loose validation — allows missing optional/publish-required fields</summary>
    Draft,
    /// <summary>Strict validation — required fields, references must exist, slug must be unique</summary>
    Publish
}
