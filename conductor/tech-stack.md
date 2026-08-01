# Tech Stack: AeroCMS

## Backend
- **Framework:** .NET 10.0 (ASP.NET Core)
- **Messaging/CQRS:** Wolverine
- **Persistence:** SurrealDB (via AeroDB.Sable document store — embedded SurrealKV or remote server)
- **Caching:** FusionCache, Garnet, Redis
- **Security:** JWT, OpenID, IdentityServer
- **Job Scheduling:** TickerQ

## Frontend
- **Framework:** Blazor (Server & WASM), Razor (cshtml/razor)
- **Styling:** Tailwind CSS
- **Client-side Components:** Preact, Lit, Alpine.js
- **Client-side Logic:** TypeScript / JavaScript

## Infrastructure & Orchestration
- **Cloud/Distributed:** .NET Aspire
- **Observability:** OpenTelemetry, Serilog
- **API Documentation:** Scalar

## Testing
- **Framework:** TUnit, Shouldly
- **Mocking:** NSubstitute
