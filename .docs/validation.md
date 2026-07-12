# AeroDB.Validation — Design Document

> Written: 2026-07-11
> Future implementation task. Source gen nullable fix (Option B) already implemented.

---

## 1. Nullable Strategy: Option B (IMPLEMENTED)

**All reference types default to `TYPE option<T>`. Use `[Required]` to opt into non-nullable `TYPE string`.**

### Why

C# `string` is always nullable at runtime regardless of `?` annotations. SurrealDB's `TYPE string` genuinely forbids `NONE`. Mapping `string` → `option<string>` aligns the semantics: by default the field accepts `NONE`, and `[Required]` upgrades to `TYPE string`.

Cross-assembly nullable context detection is too fragile — `Aero.Core` has nullable disabled, so `NullableAnnotation` is always `None` for its properties.

### Implemented

`AeroDBDocumentGenerator.cs` — `IsNullableProperty` now treats all non-value-type properties as nullable unless `[Required]` is present.

| C# Declaration | SurrealDB TYPE |
|---|---|
| `string Name` | `option<string>` |
| `[Required] string Title` | `string` |
| `string? Bio` | `option<string>` |
| `[Required] string? Title` | `string` |
| `int Age` | `int` |
| `int? Age` | `option<int>` |
| `bool IsPublished` | `bool` |

Verify: SurrealDB MCP testing confirmed that `ASSERT` does **not** fire for `NONE` on `option<T>` fields. Only bare `TYPE string` (without `option<>`) reliably rejects `NONE`.

---

## 2. Separation of Concerns

AeroDB document validation ensures that tracked documents satisfy **persistence-level invariants** before they are written. It does not replace application-layer validation.

| Layer | What it validates | Where |
|---|---|---|
| **Application** | "Can this user perform this action?" "Is this command valid in this workflow?" | Command handlers, ASP.NET validation pipeline |
| **Document** | "Is this object structurally and semantically safe to persist?" | AeroDB `SaveChangesAsync` pre-save interceptor |
| **Database** | Types, required fields, uniqueness, references | SurrealDB `DEFINE FIELD TYPE` / `ASSERT` |

### What belongs in a document validator

These describe whether a document is valid regardless of which application operation created it:

```csharp
public sealed class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}
```

### What belongs in the application layer

Authorization and use-case-specific rules belong in command/application validators:

```csharp
public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.RequestedCreditIncrease).LessThan(10_000);
    }
}
```

---

## 3. Package Structure

FluentValidation is kept in an optional integration package to avoid forcing the dependency on every AeroDB user:

```
AeroDB/
├── AeroDB.Sable/                              # Core — no validation dependency
├── AeroDB.Validation/                          # Shared validation abstractions
│   └── DocumentValidationException
│   └── DocumentValidationFailure
│   └── SaveChangesOptions.ValidationMode
│   └── PatchValidationMode
└── AeroDB.Validation.FluentValidation/         # FluentValidation integration
    ├── ValidateWith<TValidator>() extension
    ├── AddFluentValidation() DI registration
    └── Validation interceptor (pre-save hook)
```

### Dependencies

**AeroDB.Validation (shared):**
- `AeroDB.Sable` (project reference)

**AeroDB.Validation.FluentValidation (integration):**
- `AeroDB.Sable` (project reference)
- `AeroDB.Validation` (project reference)
- `FluentValidation` (already in Directory.Packages.props at 12.1.1)
- `Microsoft.Extensions.DependencyInjection.Abstractions`

---

## 4. Fluent API

### Registration

Prefer `ValidateWith<TValidator>()` over `Validate<TValidator>()` — it describes registration, not immediate validation:

```csharp
opts.Schema.For<Customer>()
    .Identity(x => x.Id)
    .ValidateWith<CustomerValidator>()
    .SetSchemaMode(SchemaMode.Flexible);
```

### Overloads

```csharp
// Typed validator
Schema.For<Customer>().ValidateWith<CustomerValidator>();

// Interface-based validator (DI-friendly)
Schema.For<Customer>().ValidateWith<IValidator<Customer>>();

// Lightweight inline validation (no FluentValidation dependency)
Schema.For<Customer>().ValidateUsing((customer, ct) =>
{
    if (string.IsNullOrWhiteSpace(customer.Name))
        return ValueTask.FromResult(DocumentValidationResult.Invalid("Name", "Name is required."));
    return ValueTask.FromResult(DocumentValidationResult.Valid);
});
```

### Internal metadata

Store validator metadata on the document mapping:

```csharp
public sealed class DocumentValidationMetadata
{
    public Type? ValidatorType { get; init; }
}
```

---

## 5. Validation Pipeline in SaveChangesAsync

Validation happens **before** any database commands so that a batch does not partially succeed:

```
Track document
    ↓
Detect changes
    ↓
Validate changed documents   ← interceptor runs here
    ↓
Generate SurrealQL
    ↓
Execute transaction
    ↓
Accept changes
```

### Resolving validators from DI

Do NOT instantiate validators with `Activator.CreateInstance()`. Validators may have dependencies such as repositories, configuration, localization, or other services. Resolve from the session's DI scope:

```csharp
var validator = serviceProvider.GetRequiredService<IValidator<T>>();
```

### Async validation

Use `ValidateAsync()`, not `Validate()`, because FluentValidation rules can be asynchronous:

```csharp
var result = await validator.ValidateAsync(document, cancellationToken);
```

### Aggregate all failures

Do not stop at the first document. Collect validation failures across all changed documents:

```csharp
try
{
    await session.SaveChangesAsync(ct);
}
catch (DocumentValidationException ex)
{
    foreach (var failure in ex.Failures)
    {
        Console.WriteLine(
            $"{failure.DocumentType.Name}.{failure.PropertyName}: " +
            failure.ErrorMessage);
    }
}
```

---

## 6. Exception Design

Do not expose FluentValidation's exception directly. Wrap results in an AeroDB-owned exception so that other validation libraries can be supported later without changing the core exception model:

```csharp
public sealed class DocumentValidationException : AeroDbException
{
    public DocumentValidationException(
        IReadOnlyList<DocumentValidationFailure> failures)
        : base("One or more documents failed validation.")
    {
        Failures = failures;
    }

    public IReadOnlyList<DocumentValidationFailure> Failures { get; }
}

public sealed record DocumentValidationFailure(
    Type DocumentType,
    object? DocumentId,
    string PropertyName,
    string ErrorMessage,
    string? ErrorCode);
```

---

## 7. DI Registration

```csharp
services.AddAeroDb(...)
    .AddFluentValidation();  // registers validators in DI, enables pre-save interceptor
```

---

## 8. Patch Validation

Server-side patches are difficult because validators normally require the full document, but a patch may not load it:

```csharp
session.Patch<Customer>(id)
    .Set(x => x.Email, email);
```

Make the behaviour explicit:

```csharp
Schema.For<Customer>()
    .ValidateWith<CustomerValidator>()
    .PatchValidation(PatchValidationMode.LoadAndValidate);
```

```csharp
public enum PatchValidationMode
{
    None,               // Skip validation — rely on SurrealDB assertions
    ValidatePatchOnly,   // Validate only the patched fields
    LoadAndValidate      // Load document, apply patch locally, validate, then save
}
```

`LoadAndValidate` is safest but adds a round trip and creates concurrency considerations.

---

## 9. Bulk Operations

Bulk insertion APIs sometimes bypass normal unit-of-work behaviour. Make semantics explicit — do not silently skip validation:

```csharp
await store.BulkInsertAsync(
    customers,
    new BulkInsertOptions
    {
        ValidateDocuments = true
    },
    ct);
```

---

## 10. Events and Projections

Event validation is useful:

```csharp
Schema.ForEvent<MoneyDeposited>()
    .ValidateWith<MoneyDepositedValidator>();
```

Generated projections generally should not require application validators because their correctness follows from valid events and projection code. Allow it as an optional diagnostic feature.

---

## 11. Deletes

Deleting a document normally does not require document validation. Referential integrity or "may this document be deleted?" checks are business/database concerns, not `IValidator<T>` document validation.

---

## 12. Escape Hatch / ValidateOnly

Migration, repair, import, and replay scenarios need a way to bypass validation:

```csharp
await session.SaveChangesAsync(
    new SaveChangesOptions
    {
        ValidationMode = DocumentValidationMode.Skip
    },
    ct);
```

Require an explicit option — do not put a casual `DisableValidation()` on the session.

```csharp
public enum DocumentValidationMode
{
    Default,        // Normal validation
    Skip,           // Skip document validation entirely
    ValidateOnly    // Run validation but do not persist
}
```

`ValidateOnly` allows a dry-run:

```csharp
var result = await session.ValidateChangesAsync(ct);

if (!result.IsValid)
{
    // Display or log validation errors without writing.
}
```

---

## 13. FluentValidation Validator Examples

### Document-level: structural and semantic persistence invariants

```csharp
public sealed class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}
```

### Application-level: command/workflow validation (NOT in AeroDB)

```csharp
public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.RequestedCreditIncrease).LessThan(10_000);
    }
}
```

---

## 14. Future Considerations

### Schema migration

Existing SurrealDB schemas have `DEFINE FIELD created_by TYPE string`. After Option B, new schema gen emits `option<string>`. SurrealDB will fail on `DEFINE FIELD` for an already-defined field.

**Mitigation path:**
1. Short-term: migration guide telling users to `REMOVE TABLE` / re-`DEFINE TABLE`
2. Medium-term: `StoreOptions.Schema.UseOptionTypes = true` opt-in flag (default false for backward compat)
3. Long-term: schema diff in `SchemaManager.EnsureDocumentSchemaAsync` that detects type changes and emits `REMOVE FIELD` + `DEFINE FIELD`

### Source gen + Validation overlap

Do NOT auto-generate FluentValidation rules from `[Required]` or other data annotations. The source gen's job is mapping C# types to SurrealDB types. Validation rule generation is a separate concern — complex to map, couples the gen to a specific validation library, and can't capture cross-property validation logic.

### Generate SurrealDB assertions from validators

Where a validation rule can also be enforced at the database level, consider generating `DEFINE FIELD ... ASSERT` statements from FluentValidation rules (e.g., `MaximumLength(200)` → `ASSERT string::len($value) <= 200`). This is a separate project with its own source generator.
