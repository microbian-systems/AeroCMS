using Microsoft.AspNetCore.Html;
using Aero.Cms.Abstractions.Http.Clients;

namespace Aero.Cms.Core.Blocks.Common;

/// <summary>
/// Defines the types of fields available in the form editor.
/// </summary>
public enum FormFieldType
{
    Text = 0,
    Email = 1,
    Number = 2,
    TextArea = 3,
    Checkbox = 4,
    Date = 5,
    Hidden = 6,
    Select = 7
}

/// <summary>
/// A definition for a single form field within a form editor block.
/// </summary>
public sealed class FormFieldDefinition
{
    /// <summary>
    /// Gets or sets the label/name of the field (e.g., "Email Address").
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default or initial value of the field.
    /// </summary>
    public string FieldValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the placeholder or accessibility alt text for the field.
    /// </summary>
    public string AltText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of input to render.
    /// </summary>
    public FormFieldType FieldType { get; set; } = FormFieldType.Text;

    /// <summary>
    /// Gets or sets whether the field is mandatory.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the options for Select/Radio fields (Value -> Label).
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = [];
}

/// <summary>
/// A block that allows building custom forms dynamically, with submission results saved to the database.
/// </summary>
[BlockMetadata("form_editor", "Form Editor", Category = "Forms")]
public sealed class FormEditorBlock : BlockBase
{
    public override string BlockType => "form_editor";

    /// <summary>
    /// Gets or sets the name/ID of the form to differentiate submissions in the database.
    /// </summary>
    public string FormName { get; set; } = "Default Form";

    /// <summary>
    /// Gets or sets the text displayed on the submit button.
    /// </summary>
    public string SubmitButtonText { get; set; } = "Send";

    /// <summary>
    /// Gets or sets the message shown to the user upon successful submission.
    /// </summary>
    public string SuccessMessage { get; set; } = "Your message has been sent successfully.";

    /// <summary>
    /// Gets or sets the target API endpoint for the form submission.
    /// </summary>
    public string ActionUrl { get; set; } = $"/{HttpConstants.ApiPrefix}forms/submit";

    /// <summary>
    /// Gets or sets whether a captcha should be displayed and validated for this form.
    /// </summary>
    /// <remarks>
    /// TODO: Investigate and implement Cloudflare Turnstile (hidden captcha) as the default provider.
    /// </remarks>
    public bool UseCaptcha { get; set; }

    /// <summary>
    /// Gets or sets the ordered collection of form fields.
    /// </summary>
    public OrderedDictionary<ushort, FormFieldDefinition> Fields { get; set; } = [];

    public override IHtmlContent Accept(IBlockVisitor visitor) => visitor.Visit(this);
}
