# Aero.Cms: Forms and Submissions

Aero.Cms includes a dynamic **Form Editor Block** that allows administrators to create custom forms (Contact, Feedback, Registration, etc.) directly within the page editor.

## Form Editor Block

The `FormEditorBlock` allows you to define a collection of fields using an `OrderedDictionary`. Each field definition includes:
- **FieldName**: The label/display name.
- **FieldType**: Text, Email, Number, TextArea, Checkbox, Date, or Hidden.
- **IsRequired**: Validation flag.
- **FieldValue**: Initial or default value.
- **AltText**: Placeholder or help text for accessibility.

## Submission Handling

By default, the block points to a generic submission API endpoint (`/api/v1/forms/submit`).

### Data Persistence via Marten

When a form is submitted, the data should be persisted as a `FormSubmission` document in Marten. Since Marten is a document store, all submissions are typically stored in the same collection (table), but they are differentiated by a `FormName` or `BlockId` property.

#### Recommended Submission Schema:

```json
{
  "Id": "0194...",
  "FormName": "Contact Us",
  "PageId": "0194...",
  "Data": {
    "full_name": "John Doe",
    "email": "john@example.com",
    "message": "Hello!"
  },
  "SubmittedAt": "2026-03-25T14:15:00Z"
}
```

### Querying Submissions

Because Marten supports LINQ and JSON-path queries, you can easily filter submissions by form:

```csharp
var contactSubmissions = await session.Query<FormSubmission>()
    .Where(x => x.FormName == "Contact Us")
    .ToListAsync();
```

---

> [!TIP]
> To prevent spam, it is recommended to integrate a CAPTCHA provider or a honeypot field (which can be added as a `Hidden` field type in the Form Editor).
