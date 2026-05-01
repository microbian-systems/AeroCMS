# Aero CMS ✈️

**Modern CMS without React.** or any other SPA framework you need to learn

A server-rendered, modular, multi-site CMS built on ASP.NET Core—designed for developers who want power without complexity.


# 💥💥💥 ALERT 💥💥💥

---

> **⚠️ Pre-Beta Software** — Aero CMS is in early development and **not production ready**. Use at your own risk. We welcome contributors!

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

**Aero stays out of your way.** It runs inside your ASP.NET Core app — the one you control. We don't force you into doing things our way for your application.

But when it comes to how Aero was designed *architecturally*, we're very opinionated: **no reflection**, **source generators** over runtime discovery, **PostgreSQL** for persistence, **Railway-oriented programming** for business logic, and clean patterns that are baked in at the foundation.

---

## 🧠 Razor + Scriban = The Best of Both Worlds

Most CMS platforms force you to choose:

- **Razor** — powerful but rigid
- **Scriban (Liquid)** — dynamic, sandboxed, and **Liquid-compatible** with some minor tweaks

Aero CMS gives you **both**:

- **Razor** for layouts, modules, and developer-owned UI — strongly typed, compiled, fast
- **Scriban** for dynamic, runtime-editable templates — flexible, sandboxed, safe

Strong typing where it matters, flexibility where it counts.

---

## 🔥 Dynamic Templates (That Don't Feel Like a Hack)

In most systems, "dynamic pages" are bolted on as an afterthought. In Aero, it's a **core design pillar**:

- Edit pages at runtime — no deploy required
- Scripting via HTMX / Scriban
- Safeguards for scripting
    - No JS allowed
    - Scriban Scripting guard rails

Real CMS flexibility— compile time safety without the flexibiity of dynamic programming (scripting).

---

## 🧩 True Modular Architecture

We didn't fake modularity. Features ship as **Razor Class Libraries (RCLs)**:

- Plug-and-play capabilities
- Clean separation between domains
- Build only what you need
- Extend without breaking everything

Each module is self-contained, independently versioned, and composed into the application.

---

## 🌍 Multi-Site by Design

Not "multi-site-ish." Actually multi-site:

- Host-based site resolution
- site-specific database isolation
- Per-site themes, templates, and content

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
| 🌍 **First-class multi-tenancy** | Host-based, per-site isolation |
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
| Testing | TUnit, Playwright, Alba, Bogus, Shouldly |

---

## Getting Started

*Coming soon — the project is in active development.*

---

## License

This project is **dual-licensed**:

| Use Case | License |
|---|---|
| **Non-commercial / OSS** | [Apache License 2.0](LICENSE) |
| **Commercial / SaaS** | [GNU AGPL v3](LICENSE) |

In short: if you're building and selling a product or service on top of Aero, the AGPL applies to ensure community contributions remain protected. If you're using it for non-commercial purposes, the standard Apache 2.0 terms apply.

See the [LICENSE](LICENSE) file for the full text of both licenses.

### 📋 Important Notes

- **NOTICE file** — Aero includes a [NOTICE](NOTICE) file as required by the Apache 2.0 license. Redistributions must preserve this notice.
- **Trademark** — *Aero* is a trademark. The Apache 2.0 license grants you the right to use the code but does not permit using the project name to market competing services without permission.
- **CLA** — All contributors must sign a [Contributor License Agreement](CONTRIBUTING.md) before contributions are accepted. This ensures we can continue to offer both the open-source and commercial versions of the project.
