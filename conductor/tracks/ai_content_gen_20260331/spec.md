# Track ai_content_gen_20260331: AI Content Generation Integration

## Overview
This track focuses on integrating AI-driven content generation into AeroCMS using the Microsoft Agent SDK and Tornado LLM. The goal is to enable automated generation of blog posts, page content, and documentation within the existing modular framework.

## Objectives
- Integrate Microsoft Agent SDK and Tornado LLM into the `Aero.Cms.Modules.Ai` and `Aero.Cms.Modules.AiAssistant` modules.
- Implement a core service for content generation (blog, pages, docs).
- Provide a user-facing interface (via Blazor/Tailwind) for triggering and reviewing AI-generated content.
- Ensure all AI operations are tracked and observable using existing infrastructure.

## Technical Details
- **AI Backend:** Microsoft Agent SDK for orchestration, Tornado LLM for model interaction.
- **CMS Integration:** Plug into `Aero.Cms.Modules.Blog` and `Aero.Cms.Modules.Pages`.
- **UI:** Custom Blazor components using Tailwind CSS for AI interaction dialogs.
- **Storage:** Use Marten to store AI-generated drafts and prompt history.

## Success Criteria
- [ ] Successful generation of a blog post draft from a prompt.
- [ ] Successful generation of page content based on a structured outline.
- [ ] AI Assistant can answer technical questions based on the `Aero.Cms.Modules.Docs` content.
- [ ] >80% test coverage for all new AI services.
