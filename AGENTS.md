# Aero.Cms Project Guidelines

## Tech Stack
- **Backend framework**: asp.net core (.net 10)
- **Service Layer**: Orleans - main services layer
- **Data persistence**: MartenDB, Postgres
- **Messaging/Workflow**: Wolverine
    - Wolverine FX for event sourcing
- TickerQ for background jobs
- **ORM**: Entityframework Core (npgsql)
- Scalar for API
- **Patterns**:
    - Heavy use of the repository pattern (`IRepository<T>`) between MartenDB and database.
    - HTMX.NET for server-side interactivity.
- Open Telemetry using serilog and openobserve (serilog sinkg for openobserve)

## Frontend
- **Language**: TypeScript first (using `Microsoft.Typescript.MSBuild` for compilation).
- **Strategy**: Using CDN first.
- **CSS Framework**: Tailwind CSS
- **UI Components**: Radzen Blazor
    - HTML/Markdown editor: radzen wysiwyg editor
    - Markdig for markdown rendering
- **JS Libraries**:
    - htmx
    - alpinejs
    - preact

## Architectural Patterns
- Entity : IEntity<long>; for database entities
    - IEntity<long> { long Id { get; set; } }
    - using Snowflake to assign IDs
    - This includes MartenDB entities and EF Core entities and aspnet Identity
- Use the **Railway Oriented Programming** patterns:
    - `Result<T>`
    - `Option<T>`
    - `Bind<T>`
    - `Map<T>`

## Testing
- **Unit Testing**: TUnit
- **GUI Integration Testing**: Microsoft Playwright
- **Integration Testing Resource**: Investigate using [mysticmind-postgresembed](https://github.com/mysticmind/mysticmind-postgresembed) for embedded Postgres in tests.

## Constraints & Rules
- **DO NOT USE NPM**. All frontend dependencies should align with the CDN usage or `Microsoft.Typescript.Build` constraints.
- Project includes a .NET MAUI hybrid web and mobile setup (newly created).
- Avoid using Guids for primary keys, use Snowflake instead (where possible)
- Do not use newtonsoft.json (use system.text.json)
