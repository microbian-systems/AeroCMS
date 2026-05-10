# Meraki UI Porting Plan

> [!NOTE]
> This plan identifies the optimal integration path for the 31 categories of Tailwind UI components found in the `./meraki` directory, ensuring they align with the existing Aero CMS block architecture.

## 🏗️ Architecture & Placement

To maintain the clean separation of concerns in Aero CMS, the ported components will be split across three main locations:

| Layer | Project / Path | Responsibility |
| :--- | :--- | :--- |
| **Data (State)** | `Aero.Cms.Core` | C# classes inheriting from `BlockBase` with data properties. |
| **Shared Data** | `src/Aero.Cms.Core/Blocks/Common` | Location for common Meraki data models. |
| **UI (Renderer)** | `Aero.Cms.Shared` | The public-facing Razor components that render the Tailwind HTML. |
| **Renderers** | `src/Aero.Cms.Shared/Blocks/Rendering` | Location for the actual UI rendering logic. |
| **Editor (UI)** | `Aero.Cms.Shared` | Inline render helpers in `PageEditor.razor` for the canvas view. |
| **Editor Logic** | `src/Aero.Cms.Shared/Pages/Manager/PageEditor` | Sidebar and active editing UI. |

---

## 📋 Implementation Steps

### Phase 1: Core Data Models (The "What")
For each Meraki component (e.g., `blog/Cards.html`), create a corresponding C# class:
1.  **Define the Block:** Create `MerakiBlogBlock.cs` in `Aero.Cms.Core/Blocks/Common`.
2.  **Add Metadata:** Decorate with `[BlockMetadata("meraki_blog", "Meraki Blog Grid")]`.
3.  **Map Properties:** Add properties for all text, image, and link portions (e.g., `string Title`, `List<BlogItem> Posts`).

### Phase 2: Public Renderers (The "How It Looks")
Create the Razor components that consumers (and eventually the public site) will use:
1.  **Create Renderer:** Create `MerakiBlogRenderer.razor` in `Aero.Cms.Shared/Blocks/Rendering`.
2.  **Inject Data:** Use `@inherits BlockRendererBase<MerakiBlogBlock>` (or similar base).
3.  **Port HTML:** Copy the Tailwind HTML from Meraki and replace hardcoded content with `@Block.Title`, etc.

### Phase 3: Manager Integration (The "How It's Edited")
Integrate the new blocks into the Page Editor workspace:
1.  **Update State (`PageEditor.razor.cs`):** 
    *   Add `protected bool CategoryMeraki { get; set; }`.
    *   Update `ToggleCategory()` with the new case.
    *   Update `CreateBlock()` to initialize the default state of the new Meraki types.
2.  **Update Sidebar (`PageEditor.razor`):**
    *   Add the "Meraki" category section with `SidebarBlockItem` components.
    *   Set up icons and drag-and-drop triggers.
3.  **Canvas Preview (`PageEditor.razor`):**
    *   Add cases to the `RenderBlock` switch to provide a live, editable preview on the canvas.

---

## 🏷️ Naming Conventions

To differentiate these specialized components from basic CMS primitives, we will use the `Aero` prefix for all ported items:
*   **Block Types:** `aero_hero`, `aero_blog`, `aero_feature`.
*   **C# Classes:** `AeroHeroBlock`, `AeroBlogBlock`.
*   **Razor Files:** `AeroHeroRenderer.razor`, `AeroBlogRenderer.razor`.

---

## 🚀 Component Priorities

With 31 categories available, the following rollout order is recommended based on common use cases:

1.  **High Priority:** Hero sections, CTA (Call to Action), Features, Blog Grids.
2.  **Medium Priority:** Pricing Tables, Team Sections, FAQ, Testimonials.
3.  **Low Priority:** Modals, Tooltips, Skeleton loaders (usually internal components).

---

## 🛠️ Technical Considerations

*   **List-Based Blocks:** Many Meraki components are repetitive (e.g., a "Team" section with 4 members). These should use `OrderedDictionary<ushort, T>` or `List<T>` properties in the `Core` class to allow reordering and flexible counts.
*   **Media Handling:** All image fields should correspond to `MediaId` (long) or `MediaItem` DTOs to reuse the existing `MediaSelectorModal`.
*   **Themes:** Ensure the ported HTML includes both `light` and `dark` mode Tailwind classes as provided by Meraki.
*   **Colors/Theming:** While we will maintain all layout and structural styles, the specific color themes (primary/secondary assignments) will not be ported at this stage; this will be addressed in a future phase.
