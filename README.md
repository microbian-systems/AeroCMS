# Aero CMS ✈️

**Modern CMS without React.**

A server-rendered, modular, multi-tenant CMS built on ASP.NET Core—designed for developers who want power without complexity.

---

## Why Another CMS?

We looked at the landscape and kept running into the same problems:

- **Commercial lock-in** — expensive licenses, closed ecosystems
- **Too big** — monolithic systems that take over your entire application
- **Too bloated** — legacy code, unnecessary abstractions, decade-old patterns
- **Massive learning curves** — you need to read a book before you can ship a page

We built Aero because there wasn't a CMS that felt like it was *designed* for modern .NET development. Something that:

- Leverages the power of ASP.NET Core without fighting it
- Lets you ship fast without painting yourself into a corner
- Is modular by default, not as an afterthought

---

## 🧠 Razor + Scriban = The Best of Both Worlds

Most CMS platforms force you to choose:

- **Razor** — powerful but rigid
- **Liquid/Scriban** — dynamic but limited

Aero CMS gives you **both**:

- **Razor** for layouts, modules, and developer-owned UI — strongly typed, compiled, fast
- **Scriban** for dynamic, runtime-editable templates — flexible, sandboxed, safe

Strong typing where it matters, flexibility where it counts.

---

## 🔥 Dynamic Templates (That Don't Feel Like a Hack)

In most systems, "dynamic templates" are bolted on as an afterthought. In Aero, it's a **core design pillar**:

- Edit templates at runtime — no deploy required
- Store in the database, filesystem, or both
- Override per tenant, per site, per theme

Real CMS flexibility—no compile-and-pray.

---

## 🧩 True Modular Architecture

We didn't fake modularity. Features ship as **Razor Class Libraries (RCLs)**:

- Plug-and-play capabilities
- Clean separation between domains
- Build only what you need
- Extend without breaking everything

Each module is self-contained, independently versioned, and composed into the application.

---

## 🌍 Multi-Tenant by Design

Not "multi-tenant-ish." Actually multi-tenant:

- Host-based tenant resolution
- Tenant-specific database isolation (SaaS-ready)
- Per-tenant themes, templates, and content

One platform → many sites → many customers.

---

## ⚡ HTMX + Alpine = Modern UX, Minimal JS

Instead of forcing a SPA framework:

- **HTMX** drives server-interactions — dynamic content without writing JavaScript
- **Alpine.js** adds lightweight client-side reactivity where you need it

No SPA required. No over-engineering. Just fast, responsive UI backed by server-rendered HTML.

---

## 🔐 Safe by Default

Scriban templates run in a **sandboxed execution environment**:

- No arbitrary C# in templates
- Controlled extensibility via exposed C# functions
- Secure by design, not as an afterthought

Powerful templates with complete control over what runs where.

---

## 🧭 Not Stuck in the Past

We designed Aero for the present and future of .NET:

- Built on modern **ASP.NET Core** patterns (minimal APIs, middleware, dependency injection)
- Designed for **cloud, containers, and scale**
- **Clean architecture** with vertical slices
- **Event-driven** ready with Wolverine + MartenDB
- **OpenTelemetry** observability out of the box

No legacy baggage. No unnecessary complexity. No decade-old hacks.

---

## 💡 What Makes Aero Stand Out

| Capability | Aero CMS |
|---|---|
| ✈️ **Razor + Scriban hybrid rendering** | First-class support |
| 🧩 **True modular system** | RCL-based modules, not plugins |
| 🌍 **First-class multi-tenancy** | Host-based, per-tenant isolation |
| ⚡ **HTMX + Alpine interactivity** | Server-driven UI, minimal JS |
| 🔐 **Safe runtime templates** | Sandboxed Scriban execution |
| 🧠 **Modern .NET patterns** | Clean architecture, vertical slices |

---

## 🚀 The Vision

We're building a CMS that feels like:

> *"What ASP.NET Core would look like if it had a CMS built in from day one."*

No hacks. No relics. No compromises.

Just a clean, modern platform you actually enjoy working with.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core (.NET 10) |
| Service Layer | Orleans |
| Persistence | MartenDB + PostgreSQL |
| Messaging / Workflow | Wolverine |
| Background Jobs | TickerQ |
| ORM | Entity Framework Core (Npgsql) |
| Server UI | HTMX.NET, Razor, Scriban |
| Client UI | HTMX, Alpine.js, Preact (opt-in) |
| CSS | Tailwind CSS |
| Validation | FluentValidation |
| Telemetry | OpenTelemetry + Serilog + OpenObserve |
| Testing | TUnit, Playwright, Alba, Bogus |

---

## Getting Started

*Coming soon — the project is in active development.*

---

## License

*TBD*
