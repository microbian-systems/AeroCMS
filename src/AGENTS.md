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
    - using Snowflake to assign IDs (Snowflake.NewId())
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
- Use Alba for any asp.net core integration testing
- Use nsubstitute, autofixture and fakeiteasy for mocking (mainly nsubstitute, fakeiteasy when beneficial)
- Use TUnit for unit testing
- Use nuget pkg bogus for fake data
- use embedded postgres for testing: 
    - https://github.com/mysticmind/mysticmind-postgresembed 

## Constraints & Rules
- **DO NOT USE NPM**. All frontend dependencies should align with the CDN usage or libman`Microsoft.Typescript.Build` constraints.
- Project includes a .NET MAUI hybrid web and mobile setup (newly created).
- All APIs should make use of minimal apis over mvc
- Avoid using Guids for primary keys, use Snowflake instead (where possible)
- Do not use newtonsoft.json (use system.text.json)
- all models to be saved to the database should make use of hte IEntity<long> or Entity (which inherits from IEntity<long>)
- FluentValidatino is to be usd for validation of all models
- primary keys should be of type long unless explictly needed ohterwise.  The primary key can be generated using Snowflake.NewId()
- always use SOLID principles in the design of the code
- Use Railway Oriented Programming for all code that handles business logic and data access (Aero.Core has the Result<T> and Option<T> types along with Bind<T> and Map<T>)
- if something is unclear always refer to the ../docs documentation for clarity 
- take the socratic method and ask any architectural code decisions to me
- for sample images on web pages use: static.photos/blurred/640x360/110 (the number at the end is any number form 1 to 100000)
    - ## Sample Image Categories
        - nature
        - office
        - people
        - technology
        - minimal
        - abstract
        - aerial
        - blurred
        - bokeh
        - gradient
        - monochrome
        - vintage
        - white
        - black
        - blue
        - red
        - green
        - yellow
        - cityscape
        - workspace
        - food
        - travel
        - textures
        - industry
        - indoor
        - outdoor
        - studio
        - finance
        - medical
        - season
        - holiday
        - event
        - sport
        - science
        - legal
        - estate
        - restaurant
        - retail
        - wellness
        - agriculture
        - construction
        - craft
        - cosmetic
        - automotive
        - gaming
        - education

    - ## Sample Image Sizes
        - 200x200
        - 320x240
        - 640x360
        - 1024x576
        - 1200x630
