namespace Aero.Cms.Shared.Pages.Manager.PageEditor.Catalog;

public sealed record NeoPropertyDefinition
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required NeoPropertyFieldType FieldType { get; init; }
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
}
