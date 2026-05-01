# Aero CMS - Page Editor Blazor/Radzen Porting Guide

This document analyzes the conversion of the current Alpine.js/JavaScript editor in `/manager/aero-cms` to a **Blazor WebAssembly/Server** implementation using **Radzen.Blazor** components.

---

## 🏗️ Infrastructure & Layout

A key architectural requirement for the port is to ensure that the manager-specific styles and Radzen components do not conflict with the public-facing styles of `Aero.Cms.Web`.

### Dedicated Manager Layout

We will implement a specific **`_ManagerLayout.razor`** (or site-specific equivalent) that will:
-   **Isolate Styles**: Host all manager-specific CSS (including Radzen themes and custom `styles.css`) so they are only loaded within the CMS management areas.
-   **Separation of Concerns**: Ensure that the public website (`Aero.Cms.Web`) remains lean and unaffected by the heavy editor UI components.
-   **Manager Context**: Provide the global navigation, sidebar, and editor canvas container specifically for administrative users.

## 🎨 Architectural Transition Overview

Converting the editor is a significant but feasible architectural shift that moves complex state management into structured C# logic.

### Technical Mapping: Components & Features

| Feature | Current (Alpine.js / HTML) | Blazor / Radzen Equivalent | Effort / Feasibility |
| :--- | :--- | :--- | :--- |
| **State Management** | `cmsEditor()` JS object | C# Service / `PageEditorState` class | **Moderate**. Straightforward conversion from JS properties to C# properties. |
| **Rich Text Editor** | TinyMCE | **[RadzenHtmlEditor](https://blazor.radzen.com/html-editor)** | **Low**. Radzen's editor natively supports source view, custom tools, and styling. |
| **Markdown Editor** | Manual Textarea / Preview | **[RadzenMarkdown](https://blazor.radzen.com/markdown)** | **Low**. Provides a full-featured Markdown editing experience with live preview and formatting tools. |
| **Drag & Drop** | Custom HTML5 API in `app.js` | **HTML5 D&D API** (Native Blazor) | **Moderate**. Blazor integrates well with HTML5 D&D for flexible, block-based canvas nodes. |
| **Layout System** | Custom Flexbox + Vanilla CSS | **`RadzenRow` / `RadzenColumn`** | **Low**. Recursive rendering in Blazor makes "Columns within Columns" much cleaner. |
| **Modals / Toasts** | Custom HTML/CSS/JS | **`DialogService` / `NotificationService`** | **Very Low**. Radzen's services are built for this. |
| **Media Library** | Custom Mock Logic | **`RadzenDataList` / `RadzenUpload`** | **Low**. Strength of Radzen components. |

---

## 🎨 Block-Level Inline Editing

A core feature of the page editor is the ability to edit block content (media, text, etc.) directly on the canvas. This is highly feasible in Blazor using **two-way data binding (`@bind-Value`)**.

### Implementation Strategies

#### 1. **Inline Canvas Editing**
You can swap block displays based on their `IsSelected` state directly on the editor canvas:
*   **Hero Block**: When clicked, swap `<span>` elements for **`RadzenTextBox`**.
*   **Rich Content**: Use **`RadzenHtmlEditor`** directly inside the block wrapper.
*   **References**: Use **`RadzenDropDown`** to allow users to select from available Pages or Posts.

#### 2. **Contextual & Floating Toolbars**
Instead of static inputs, you can show floating toolbars or overlays when a block is active:
*   **Media Swapping**: Trigger a **`DialogService`** modal to browse the library.
*   **Reordering**: Use simple arrow buttons that trigger native C# list manipulation.

#### 3. **Sidebar Settings Panel (Inspector)**
For complex blocks like `Columns` or `Analytics`, a dedicated "Settings" panel (using **`RadzenSidebar`**) is a resilient pattern:
*   Selecting a block on the canvas populates the sidebar.
*   Two-way binding ensures the canvas preview updates instantly as settings (e.g., column gap, background color) are adjusted.

---

## 🏗️ Radzen Drag & Drop Strategy

After researching the available Radzen components, the following strategy is recommended for the editor canvas:

### 🏆 Selection: Radzen DropZone

| Feature | Radzen DropZone (Selected) | Radzen DataGrid Row Drag |
| :--- | :--- | :--- |
| **Visual Fidelity** | **High**. Blocks can look exactly like the live site (Hero, Video, etc.). | **Low**. Blocks are forced into a grid/row format. |
| **Block Nesting** | **Native Support**. Allows dropping blocks into columns inside other blocks. | **Complex**. Requires nested grids, leading to a cluttered UI. |
| **Data Diversity** | **Excellent**. Designed to handle varied objects (`HeroBlock`, `TextBlock`, etc.). | **Limited**. Optimized for uniform data sets. |
| **Implementation** | Uses clear `Drop` and `CanDrop` events in C#. | Relies on manual HTML5 attributes on table rows. |

**Verdict**: The `RadzenDropZone` component is significantly better for a block-based CMS. It handles the structural complexity of nested blocks naturally and doesn't force a tabular UI on the user.

---

## 🎥 Native Video Block Implementation

For hosted video content (distinct from YouTube/Vimeo embeds), the port will implement a native `<video>` component block.

### Razor Component Design:
Using the HTML5 standard ensure maximum browser compatibility:

```razor
<div class="video-block-wrapper">
    <video width="@Block.Width" height="@Block.Height" controls="@Block.ShowControls">
        <source src="@Block.SourceUrl" type="video/mp4">
        Your browser does not support the video tag.
    </video>
</div>
```

### Integration Details:
*   **Backend Model**: Represented by a `VideoPlayerBlock` class in C# storing `Width`, `Height`, `SourceUrl`, and boolean `ShowControls`.
*   **UI Features**: Leverage Radzen's `UploadUrl` for direct file uploads to the CMS backend, automatically updating the `<source>` tag once the upload completes.

---

## ✅ Core Advantages & Disadvantages

### Advantages
1.  **Shared Logic & Models**: The editor and backend share the *exact same* `BlockBase` classes and JSON logic, eliminating the risk of data mismatch between JS and C#.
2.  **Type Safety**: Blazor provides full IntelliSense and compile-time checking for block properties, drastically reducing runtime errors.
3.  **AOT-Ready Rendering**: Using the backend's visitor pattern (`IBlockVisitor`) ensures that "What You See" (editor preview) is virtually identical to "What You Get" (published HTML).
4.  **Rich Control Library**: Radzen provides immediate access to color pickers, sliders, numeric inputs, and complex autocomplete dropdowns that would require significant custom JS in the current version.

### Challenges
1.  **Drop Zone Logic**: Radzen does not have a generic "UI element drop zone" for rearranging abstract nodes. You will need to implement the **HTML5 Drag & Drop API** within your Razor components to manage block reordering and nesting.
2.  **JS Interop**: While 95% of the logic moves to C#, small behaviors (like scroll-to-element or integrating specific external JS libraries) will still require minimal JS Interop.

---

## 📊 Estimated Level of Effort (LOE)

| Task | Estimated Time | Level of Difficulty |
| :--- | :--- | :--- |
| **Infrastructure** (State, Project Setup, Base Services) | 1-2 Days | Simple |
| **Block Library** (Porting individual block Razor UIs) | 3-5 Days | Moderate |
| **Drag & Drop Engine** (Implementing reordering/nesting) | 2-3 Days | Moderate |
| **Advanced Features** (Media Selector, Reference Linking) | 2-3 Days | Moderate |

**Final Recommendation**: **High Feasibility**. The transition is highly "sensible" because it consolidates the CMS ecosystem into a single language (C#) and leverages Radzen's premium UI components to accelerate development.
