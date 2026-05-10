namespace Aero.Cms.Abstractions.Content;

public interface IFieldTemplateSnippet
{
    string FieldType { get; }
    string Render(ContentFieldDefinition field);
}
