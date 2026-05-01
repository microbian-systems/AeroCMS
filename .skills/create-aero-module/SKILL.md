# Create Aero CMS Module Skill

## Purpose

Create a new Aero CMS feature/module following the project's modular architecture.

A module represents a self-contained CMS feature such as:

- Pages
- Blog
- Media
- Categories
- Tags
- Banners
- Aliases
- Docs

The generated module must follow Aero CMS conventions and compile cleanly.

---

## Module Project Structure

Create a project named:

```txt
Aero.Cms.Modules.[FeatureName]
```

Examples:

```txt
Aero.Cms.Modules.Banner
Aero.Cms.Modules.Media
Aero.Cms.Modules.Pages
```

The project type should be:

* Razor Class Library when UI is required.
* Class Library when no UI is required.

Each module should include:

```txt
Aero.Cms.Modules.[FeatureName]/
  README.md
  [FeatureName]Module.cs
  Models/
    [FeatureName]Model.cs
  Services/
    I[FeatureName]Service.cs
    [FeatureName]Service.cs
  Validation/
    [FeatureName]Validator.cs
```

If APIs are required, place them where the current project expects headless APIs:

```txt
Aero.Cms.Modules.Headless/
  [FeatureName]Api.cs
```

---

## Module README

Every module must include a `README.md`.

The README should explain:

* What the module does.
* The primary entity/model.
* Registered services.
* API endpoints, if applicable.
* Any dependencies.
* Any special Marten configuration.

---

## Module Entry Point

The main class should be named:

```txt
[FeatureName]Module
```

Example:

```txt
BannerModule
```

The module class must inherit from the appropriate Aero module base type, usually:

```csharp
AeroWebModule
```

or:

```csharp
AeroModuleBase
```

Use the existing module base type used by nearby modules.

---

## Module Discovery

Modules are discovered using source generators.

Do not use:

* Reflection scanning
* Assembly scanning
* Runtime type discovery

The generated module should be compatible with the existing source-generator-based discovery system.

---

## Module Entry Point Example

```csharp
public class BannerModule : AeroWebModule
{
    public override string Name => nameof(BannerModule);

    public override string Version => AeroConstants.Version;

    public override string Author => AeroConstants.Author;

    public override IReadOnlyList<string> Dependencies => [];

    public override IReadOnlyList<string> Category => ["infrastructure"];

    public override IReadOnlyList<string> Tags => ["web", "infrastructure"];

    public override void ConfigureServices(
        IServiceCollection services,
        IConfiguration? config = null,
        IHostEnvironment? env = null)
    {
        base.ConfigureServices(services, config, env);

        services.AddScoped<IBannerService, BannerService>();
    }

    public override void Configure(IServiceProvider services, StoreOptions options)
    {
        base.Configure(services, options);
    }

    public override Task RunAsync(IServiceProvider sp)
    {
        return base.RunAsync(sp);
    }

    public override void Run(IEndpointRouteBuilder builder)
    {
        base.Run(builder);

        BannerApi.MapBannerApi(builder);
    }
}
```

---

## Feature API Rules

Feature APIs currently live in:

```txt
Aero.Cms.Modules.Headless
```

APIs must use:

* Minimal APIs
* Request/response DTOs
* Dependency injection
* `I[FeatureName]Service`

Do not use MVC controllers.

Authentication is required by default.

Anonymous access must be explicit.

Example intent:

```csharp
group.RequireAuthorization();
```

Only use anonymous access when explicitly requested:

```csharp
.AllowAnonymous();
```

---

## API Requirements

Each API should:

* Map endpoints using an extension method.
* Use clear route groups.
* Accept request DTOs instead of exposing entities directly when appropriate.
* Return response DTOs where appropriate.
* Use DI for services.
* Validate input using FluentValidation.
* Avoid business logic inside endpoint handlers.

Suggested shape:

```csharp
public static class BannerApi
{
    public static IEndpointRouteBuilder MapBannerApi(this IEndpointRouteBuilder builder)
    {
        var group = builder
            .MapGroup("/api/banners")
            .RequireAuthorization();

        group.MapGet("/", async (IBannerService service) =>
        {
            var results = await service.GetAllAsync();
            return Results.Ok(results);
        });

        return builder;
    }
}
```

---

## Entity / Model Rules

Each module should define a model named:

```txt
[FeatureName]Model
```

Example:

```txt
BannerModel
```

The model should inherit from the existing Aero entity abstraction.

Prefer:

```csharp
Entity<long>
```

or:

```csharp
IEntity<long>
```

Use the pattern already present in the repository.

Example:

```csharp
public class BannerModel : Entity<long>
{
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset EndDate { get; set; }
}
```

---

## Validation Rules

Validators must use FluentValidation.

Validator name:

```txt
[FeatureName]Validator
```

Example:

```csharp
public class BannerValidator : AbstractValidator<BannerModel>
{
    public BannerValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("the id must be greater than 0 and should be generated by the snowflake algorithm");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("name is required");

        RuleFor(x => x.StartDate)
            .NotEmpty()
            .WithMessage("start date is required for banners");

        RuleFor(x => x.EndDate)
            .NotEmpty()
            .WithMessage("end date is required for banners");
    }
}
```

Use FluentValidation APIs correctly.

Do not use invalid methods like:

```csharp
.NotNullOrEmpty()
```

Use:

```csharp
.NotEmpty()
```

---

## Service Rules

Each module should include:

```txt
I[FeatureName]Service
[FeatureName]Service
```

The service acts as the feature service and repository wrapper.

The service interface should inherit from:

```csharp
IGenericMartenRepository<[FeatureName]Model>
```

Example:

```csharp
public interface IBannerService : IGenericMartenRepository<BannerModel>
{
    Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end);
}
```

The concrete service should inherit from:

```csharp
GenericMartenRepository<[FeatureName]Model>
```

Example:

```csharp
public class BannerService(IDocumentSession session, ILogger<BannerService> log)
    : GenericMartenRepository<BannerModel>(session, log), IBannerService
{
    public async Task<IList<BannerModel>> FindByDateRange(DateTimeOffset start, DateTimeOffset end)
    {
        Expression<Func<BannerModel, bool>> predicate =
            b => b.StartDate >= start && b.EndDate <= end;

        var results = await FindAsync(predicate);

        return results.ToList();
    }
}
```

---

## Marten Rules

Use MartenDB for CRUD and persistence.

Prefer existing repository abstractions.

Do not create a new data access pattern unless explicitly requested.

Use:

```csharp
IDocumentSession
```

for document operations.

Use existing Aero repository base classes.

---

## Testing Rules

Create unit and integration tests where appropriate.

Required test areas:

* Service / repository behavior
* API behavior
* Validation behavior

Testing framework:

```txt
TUnit
```

Do not use:

* xUnit
* NUnit
* MSTest

API tests should use:

```txt
Alba
```

Test doubles may use:

* NSubstitute
* FakeItEasy

Do not use:

* Moq

Test data generation should use:

```txt
Bogus
```

---

## Expected Output

When creating a new module, generate:

```txt
Aero.Cms.Modules.[FeatureName]/
  README.md
  [FeatureName]Module.cs
  Models/[FeatureName]Model.cs
  Services/I[FeatureName]Service.cs
  Services/[FeatureName]Service.cs
  Validation/[FeatureName]Validator.cs
```

If headless APIs are required, also generate or update:

```txt
Aero.Cms.Modules.Headless/[FeatureName]Api.cs
```

If tests are required, generate:

```txt
tests/
  Aero.Cms.Modules.[FeatureName].Tests/
```

or follow the existing test project convention.

---

## Agent Behavior

Before writing code:

1. Inspect nearby existing modules.
2. Follow current namespace conventions.
3. Match existing folder structure.
4. Match existing test style.
5. Use the repository's current abstractions.
6. Avoid introducing unrelated changes.

After writing code:

1. Ensure the solution compiles.
2. Ensure APIs use minimal APIs.
3. Ensure validators use valid FluentValidation syntax.
4. Ensure services are registered in the module.
5. Ensure no reflection-based discovery was introduced.
6. Ensure tests use TUnit, Alba, Bogus, and NSubstitute/FakeItEasy.
