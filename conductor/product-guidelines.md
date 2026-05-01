# Product Guidelines: AeroCMS

## Prose Style & Tone
- **Professional & Technical:** Content should be clear, concise, and technically accurate.
- **Developer-Centric:** Use language familiar to .NET developers (e.g., "Dependency Injection," "Middleware," "Modules").
- **Confident & Forward-Looking:** Emphasize the cutting-edge nature of the technology stack (.NET 10.0+).
- **Direct & Helpful:** Instructions and documentation should be actionable and minimize ambiguity.

## Design & UI Principles
- **Tailwind-First Styling:** All UI styling must be done using Tailwind CSS utility classes. Avoid custom CSS files.
- **Modern & Lightweight:** Leverage Preact, Lit, and Alpine.js for lightweight and performant client-side components.
- **Razor Integration:** Seamlessly integrate client-side components within standard Razor/cshtml files.
- **Responsive by Default:** Ensure all management and content-facing UIs work seamlessly across all device sizes using Tailwind's responsive utilities.
- **Accessible (a11y):** Prioritize accessibility in all UI components to ensure inclusivity for all users.
- **Performance First:** Minimize UI lag and prioritize fast loading times through efficient data fetching and minimal client-side bundles.

## UX Principles
- **Predictability:** Features should behave consistently across the platform.
- **Minimalism:** Present users with only the information and actions necessary for their current context.
- **Discoverability:** Use clear navigation and search to help users find features and content easily.
- **Feedback Loops:** Provide immediate and informative feedback for all user actions (e.g., saving, deleting, error handling).
- **Graceful Error Handling:** Inform users of errors clearly and suggest actionable steps for resolution.

## Architectural Conventions
- **Module Isolation:** Features must be developed as self-contained modules.
- **TDD First:** All new functionality must be accompanied by comprehensive unit and integration tests.
- **Strong Typing:** Use TypeScript for complex client-side code and leverage C#'s strong type system for backend services.
- **Observability Integrated:** Ensure all modules include logging and telemetry (OpenTelemetry) for real-time monitoring.
- **Documentation as Code:** Maintain up-to-date documentation alongside the source code.
