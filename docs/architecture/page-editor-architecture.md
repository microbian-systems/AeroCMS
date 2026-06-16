# AeroCMS WYSIWYG Page Editor — Architecture Diagrams

> **Audience:** Stakeholders, engineering leads, new team members  
> **Format:** Mermaid — renders natively in GitHub, GitLab, VS Code, and Bitbucket  
> **Last updated:** 2026-06-16

---

## 1. System Context

How the page editor fits into the AeroCMS application stack.

```mermaid
flowchart TB
    subgraph Aspire["AeroCMS Aspire Orchestrator"]
        direction TB

        subgraph Web["Web Host (Blazor Server)"]
            direction TB

            subgraph Manager["Manager UI (Admin Panel)"]
                PE["PageEditor.razor\nOrchestrator, canvas,\npalette, modals"]
                OTHER["Other manager pages\nDashboard, Media Library,\nSettings"]
            end

            API["Minimal API Endpoints\n/api/pages, /api/custom-components"]
            PUBLIC["Public Rendering\nSSR Pipeline + Block Cache"]
        end

        subgraph Orleans["Orleans Silo"]
            PG["Page Grain\nState + Publish"]
            PRG["Preview Grain\nDraft Layout Generation"]
        end

        subgraph Data["Persistence"]
            MDB[("MartenDB\nDocument DB / Postgres")]
            EFC[("EF Core\nIdentity / Relational")]
        end

        subgraph Identity["ASP.NET Core Identity"]
            AUI["Auth / Tenant Scoping\nClaims + Roles"]
        end
    end

    AUTHOR(["Editor / Author"]) -->|HTTPS| PE
    PE --> API
    API --> Orleans
    Orleans --> Data
    Identity -.-> API
    Identity -.-> Manager

    VISITOR(["Visitor"]) -->|HTTPS| PUBLIC
    PUBLIC --> MDB
```

**Patterns:** *C4 System Context* — shows the Manager UI (hosting the page editor) as one component within a larger distributed system (Orleans grains, MartenDB document store, SSR public rendering).

---

## 2. Class Hierarchy — Definition Inheritance

Every visual element on the canvas — primitives, containers, blocks, custom components — shares a common contract hierarchy.

```mermaid
classDiagram
    class IPageEditorCatalogDefinition {
        <<interface>>
        +CatalogId
        +DisplayName
        +Category
        +Kind
        +Composition
        +EditorCapabilities
    }

    class INeoNodeFactory {
        <<interface>>
        +CreateDefaultNode()
    }

    class IEditorInteractionProvider {
        <<interface>>
        +Interaction
    }

    class ICompositionCapabilities {
        <<interface>>
        +IsEmbeddable
        +CanContainChildren
        +AllowedParentKinds
        +AllowedChildKinds
    }

    class PageEditorCatalogDefinitionBase {
        <<abstract>>
        #virtual Description, IconName, SortOrder
        #abstract CatalogId, DisplayName, Kind
        #abstract Composition, EditorCapabilities
        #abstract CreateDefaultNode()
    }

    class PrimitiveDefinitionBase {
        <<abstract>>
        +Kind = Primitive
    }

    class ContainerDefinitionBase {
        <<abstract>>
        +Kind = Container
    }

    class CannedBlockDefinitionBase {
        <<abstract>>
        +Kind = Block
    }

    class TextPrimitiveDefinition {
        +CatalogId = "primitive.text"
        +DisplayName = "Text"
    }

    class ButtonPrimitiveDefinition {
        +CatalogId = "primitive.button"
    }

    class ImagePrimitiveDefinition {
        +CatalogId = "primitive.image"
    }

    class ContainerPrimitiveDefinition {
        +CatalogId = "primitive.container"
    }

    class ColumnsDefinition {
        +CatalogId = "neo.layout.columns"
    }

    class HeroBlockDefinition {
        +CatalogId = "ui.hero.basic"
    }

    class PricingBlockDefinition {
        +CatalogId = "ui.pricing"
    }

    class LegacyPageEditorDefinitionAdapter {
        <<adapter>>
        -wraps IPageEditorBlockDefinition
        +ToDescriptor()
    }

    class PageEditorDefinitionDescriptor {
        <<record>>
        +Catalog
        +NodeFactory
        +BlockMapper
        +Interaction
    }

    IPageEditorCatalogDefinition    <|.. PageEditorCatalogDefinitionBase : implements
    INeoNodeFactory                 <|.. PageEditorCatalogDefinitionBase : implements
    IEditorInteractionProvider      <|.. PageEditorCatalogDefinitionBase : implements

    PageEditorCatalogDefinitionBase <|-- PrimitiveDefinitionBase : extends
    PageEditorCatalogDefinitionBase <|-- CannedBlockDefinitionBase : extends
    PrimitiveDefinitionBase         <|-- ContainerDefinitionBase : extends

    PrimitiveDefinitionBase         <|-- TextPrimitiveDefinition : extends
    PrimitiveDefinitionBase         <|-- ButtonPrimitiveDefinition : extends
    PrimitiveDefinitionBase         <|-- ImagePrimitiveDefinition : extends
    ContainerDefinitionBase         <|-- ContainerPrimitiveDefinition : extends
    ContainerDefinitionBase         <|-- ColumnsDefinition : extends
    CannedBlockDefinitionBase       <|-- HeroBlockDefinition : extends
    CannedBlockDefinitionBase       <|-- PricingBlockDefinition : extends

    IPageEditorCatalogDefinition    <|.. LegacyPageEditorDefinitionAdapter : implements
    LegacyPageEditorDefinitionAdapter --> PageEditorDefinitionDescriptor : creates
```

**Patterns:** *Template Method* (base class defines skeleton, concretes supply values), *Factory Method* (`CreateDefaultNode()`), *Bridge* (interface hierarchy separate from implementation), *Adapter* (LegacyPageEditorDefinitionAdapter bridges old `IPageEditorBlockDefinition` into new catalog).

---

## 3. Service Interaction — DI Wiring

How definitions flow from providers through the registry to editor consumers. One `AddSingleton` call per component; no static state.

```mermaid
flowchart TB
    subgraph Registration["DI Registration (Startup)"]
        NP["NeoPageEditorBlockProvider\nIPageEditorBlockProvider\nIPageEditorDefinitionProvider"]
        HP["HyperPageEditorBlockProvider\nIPageEditorBlockProvider"]
        CP["CannedBlockDefinitionProvider\nIPageEditorBlockProvider"]
        REG["PageEditorDefinitionRegistry\nIPageEditorDefinitionRegistry"]
        ACT["EditorNodeActionProvider\nIEditorNodeActionProvider"]
        POL["CompositionPolicy\nICompositionPolicy"]
    end

    subgraph Consumers["Runtime Consumers"]
        PAL["PageEditorPaletteSection\nBuilds catalog from\nAllDescriptors"]
        CAN["PageEditorCanvas\nResolves preview types\nfrom descriptor"]
        EBF["EditorBlockFrame\nComputes context menu\nvia IEditorNodeActionProvider\nusing descriptor.Interaction"]
        SCS["SortableCompositionSurface\nValidates placement via\nICompositionPolicy"]
        MODAL["BlockEditorModal\nShows property editors\nbased on EditorCapabilities"]
    end

    NP --> REG
    HP --> REG
    CP --> REG
    REG --> PAL
    REG --> CAN
    REG --> EBF
    POL --> SCS
    REG --> ACT
    REG --> MODAL
```

**Patterns:** *Dependency Inversion* (consumers depend on `IPageEditorDefinitionRegistry`, not concrete static registry), *Single Responsibility* (providers produce, registry stores, policies decide, UI renders).

---

## 4. Blazor Component Tree

The runtime UI hierarchy. Every block is rendered through the same `EditorBlockFrame` wrapper; nested composition uses `SortableCompositionSurface`.

```mermaid
flowchart TB
    PE["PageEditor.razor\nOrchestrator\nOwns Blocks[], selection, undo stacks"]
    PE --> HDR["PageEditorHeader.razor\nBreakpoint switcher\nUndo / Redo / Preview / Save"]
    PE --> PAL["PageEditorPaletteSection.razor\nCatalog sections\nSearch + drag source"]
    PE --> PV["PageEditorPropertyPanel.razor\nSelected block inspector"]
    PE --> PC["PageEditorCanvas.razor\nRoot Sortable surface\nSortable<EditorBlock>"]

    PC --> EBF["EditorBlockFrame.razor\nPer-block wrapper\nDrag handle + toolbar\nRight-click context menu"]
    EBF --> BPH["BlockEditorPreviewHost.razor\nRenders live preview"]
    EBF --> BME["BlockEditorModal\nContent / Design / Advanced\nTabs driven by\nEditorCapabilitySet"]

    PE --> SCS["SortableCompositionSurface.razor\nNested Sortable<NeoPageNode>\nUsed by Containers, Columns\nInline editing + toolbar"]
    SCS --> SCF["EditorBlockFrame\n(child nodes)\nSame wrapper as root"]
    SCS --> SCB["BlockEditorModal\nNested node property editing"]
```

**Note:** Both root blocks (`EditorBlockFrame`) and nested composition nodes (`SortableCompositionSurface`) use the same `EditorBlockFrame` wrapper, the same `IEditorNodeActionProvider` for context menus, and the same `BlockEditorModal` for property editing. This is the unified interaction path.

---

## 5. Composition Data Model — NeoPageNode Tree

Every page is a tree of `NeoPageNode` instances. Each node carries a `CatalogId` (resolves to a definition), a `Kind` (composition role), typed `Properties` (JSON), responsive `Style`, and optional `Children`.

```mermaid
flowchart TB
    ROOT["NeoPageNode: root\nCatalogId: 'page.root'\nKind: Section\nResponsiveStyle: Base"]

    ROOT --> CONT["NeoPageNode: hero-wrapper\nCatalogId: 'primitive.container'\nKind: Container\nStyle: Base BG=#1a1a2e\nEditorCapabilities:\n  Background, Spacing, Border"]

    CONT --> COLS["NeoPageNode: columns\nCatalogId: 'neo.layout.columns'\nKind: Container\nChildren: 2\nComposition:\n  AllowedChildKinds: [Primitive, Container]\n  MaxChildren: 2\n  DropZones: [col-0, col-1]"]

    COLS --> COL1["NeoPageNode: left-col\nCatalogId: 'primitive.container'\nKind: Container\nStyle: Padding=16px"]

    COLS --> COL2["NeoPageNode: right-col\nCatalogId: 'primitive.container'\nKind: Container\nStyle: Padding=16px"]

    COL1 --> H["NeoPageNode: heading\nCatalogId: 'primitive.text'\nKind: Primitive\nProperties: { text: 'Welcome' }"]
    COL1 --> B["NeoPageNode: cta\nCatalogId: 'primitive.button'\nKind: Primitive\nProperties: { text: 'Get Started', url: '/signup' }"]

    COL2 --> I["NeoPageNode: hero-img\nCatalogId: 'primitive.image'\nKind: Primitive\nProperties: { url: '/hero.png', alt: 'Hero' }"]

    subgraph Legend["Key Concepts"]
        L1["CatalogId → resolves to\nPageEditorDefinitionDescriptor\n(via IPageEditorDefinitionRegistry)"]
        L2["Kind → determines\ncomposition rules:\nSection, Container,\nComponent, Primitive, Block"]
        L3["Properties → typed JSON\n(definition-specific)\ne.g.: text, url, iconName"]
        L4["ResponsiveStyle →\n3 breakpoints:\nBase → Tablet → Mobile\ninheritance chain"]
        L5["CompositionPolicy →\nvalidates every insert,\nmove, re-parent operation"]
    end
```

**Patterns:** *Composite* (tree of nodes, uniform treatment of leaves and containers), *Strategy* (definition selected by `CatalogId`).

---

## 6. Block Editing Flow — Palette to Canvas

Sequence for dragging a primitive from the palette into a container on the canvas.

```mermaid
sequenceDiagram
    actor User
    participant Palette as PageEditorPaletteSection
    participant Canvas as SortableCompositionSurface
    participant Editor as CompositionTreeEditor
    participant Policy as CompositionPolicy
    participant Registry as IPageEditorDefinitionRegistry
    participant History as CompositionHistory

    User->>Palette: Drag "Text" primitive

    Palette->>Registry: TryGetDescriptor("primitive.text")
    Registry-->>Palette: Descriptor + NodeFactory
    Palette->>Palette: descriptor.NodeFactory.CreateDefaultNode()
    Note over Palette: NeoPageNode{ NodeId: new,\n  CatalogId: "primitive.text",\n  Kind: Primitive,\n  Properties: { text: "..." } }

    Palette->>Canvas: OnTransfer(node, parentId, dropZone, index)

    Canvas->>Editor: Drop(currentRoots, DropRequest)
    Editor->>Policy: ValidatePlacement(child, parent, "default", context)

    alt Valid
        Policy-->>Editor: OK
        Editor->>Editor: Deep-clone roots
        Editor->>Editor: Insert node at target
        Editor-->>Canvas: Updated roots
        Canvas->>History: Record(newState)
        Canvas-->>User: Re-render
    else Invalid
        Policy-->>Editor: Error
        Editor-->>Canvas: Result.Error
        Canvas-->>User: Toast: "Cannot place here"
    end
```

**Patterns:** *Command* (DropRequest encapsulates mutation), *Strategy* (Policy selects validation rules by definition), *Memento* (History stores deep-clone snapshots).

---

## 7. Interaction Capabilities — Context Menu Strategy

Canvas actions are not hardcoded per block type. Each definition declares its allowed interactions via flags; a central provider resolves the current menu based on flags + runtime state.

```mermaid
flowchart LR
    subgraph Definition["Block Definition"]
        CAP["EditorInteractionCapabilities\n[Flags] Enum"]
        CAP_FLAGS["Selectable\nEditable\nDraggable\nDuplicatable\nDeletable\nCopyable\nPasteTarget\nSaveAsCustom\nMediaSelectable"]
    end

    subgraph Runtime["Editor Session State"]
        CTX["EditorNodeActionContext"]
        CTX_FIELDS["HasClipboardContent\nCanMoveUp / CanMoveDown\nCanSaveAsCustom"]
    end

    subgraph Provider["IEditorNodeActionProvider\n(DI Service - Singleton)"]
        ACT["GetAvailableActions(\n  capabilities,\n  context\n) → IReadOnlyList<EditorNodeAction>"]
    end

    subgraph Result["Context Menu (UI)"]
        MENU["🔹 Edit\n🔹 Duplicate\n🔹 Delete\n🔹 Copy\n🔹 Paste (if has clipboard)\n🔹 Move Up (if not first)\n🔹 Move Down (if not last)\n🔹 Save as Custom"]
    end

    CAP --> ACT
    CTX --> ACT
    ACT --> MENU

    subgraph Example["Example: Text Primitive"]
        TEXT_EX["Selectable | Editable |\nDraggable | Duplicatable |\nDeletable | Copyable"]
    end

    subgraph Example2["Example: Container"]
        CONT_EX["Selectable | Editable |\nDraggable | Duplicatable |\nDeletable | Copyable |\nPasteTarget"]
    end
```

**Patterns:** *Strategy* (action provider algorithm is swappable), *Interface Segregation* (interaction flags separate from property editor capabilities), *Open/Closed* (adding a new block type adds a definition, not a switch case).

---

## 8. Undo / Redo — Command + Memento

Two history systems handle different scope levels. Both use the Memento pattern (deep-clone snapshots) with bounded capacity.

```mermaid
flowchart TB
    subgraph PageCanvasHistory["Top-Level: PageCanvasHistory"]
        direction LR
        PCH["PageCanvasHistory\ncapacity: 100"]
        PCBEFORE["EditorBlockListMemento\nJSON-serialized\nList<EditorBlock>"]
        PCAFTER["EditorBlockListMemento"]
        PCH -->|Undo| PCBEFORE
        PCH -->|Redo| PCAFTER
        PCH_NOTE["Coalescing:\nSame EditorBlock type\nwithin 500ms → overwrite"]
    end

    subgraph CompositionHistory["Nested: CompositionHistory"]
        direction LR
        CH["CompositionHistory\ncapacity: 100"]
        CHBEFORE["EditorNodeMemento\nDeep-cloned NeoPageNode"]
        CHAFTER["EditorNodeMemento\nDeep-cloned NeoPageNode"]
        CH -->|Undo| CHBEFORE
        CH -->|Redo| CHAFTER
        CH_NOTE["Coalescing:\nSame CompositionMutation key\n→ replace last entry\nRedo invalidation on new mutation"]
    end

    subgraph NodeEditorSession["Modal: NodeEditorSession"]
        NES["NodeEditorSession"]
        NES_WORKING["WorkingNode\nMutable copy"]
        NES -->|Apply| CH
        NES -->|Cancel| DISCARD["Changes discarded\nNo history mutation"]
    end

    PageCanvasHistory -.->|"new mutation invalidates redo"| PageCanvasHistory
    CompositionHistory -.->|"new mutation invalidates redo"| CompositionHistory
```

**Patterns:** *Command* (each user action is a discrete mutation), *Memento* (snapshots capture full state for undo/redo), *Coalescing* (rapid sequential mutations of the same type merge into one undo step).

---

## 9. Public Rendering Pipeline

From Manager UI save to visitor's browser. The composition tree flows through Orleans grains, MartenDB projections, and the SSR pipeline.

```mermaid
flowchart LR
    MANAGER["Manager UI\nPage Editor\nSave / Publish"] --> API["Pages API\nPUT /api/pages/{id}"]

    API --> PG["Orleans Page Grain\nUpdateState()"]
    PG --> MDB1[("MartenDB\nDraft Document")]
    PG --> PUB["Publish Command"]

    PUB --> PROJ["Inline Event Projection\n→ Layout Manifest"]
    PROJ --> MDB2[("MartenDB\nPublished Version")]
    PUB --> CACHE["Invalidate\nRequest Block Cache"]

    VISITOR["Visitor\nHTTP Request"] --> ROUTER["Page Router\nMatch URL + Culture"]
    ROUTER --> CACHE2["Request Block Cache\n(miss → load)"]
    CACHE2 --> MDB2
    CACHE2 --> RENDER["SSR Render Pipeline"]

    subgraph SSR["SSR Pipeline Details"]
        RENDER --> RESOLVE["Resolve CatalogId\n→ IPageEditorDefinitionRegistry"]
        RESOLVE --> RENDER_NODE["RenderNode()\nRecursive\nChildren + Styles"]
        RENDER_NODE --> EMIT["Sanitized HTML\nResponsive CSS\nLTR/RTL support"]
    end

    EMIT --> BROWSER["Browser"]
```

**Patterns:** *Strategy* (renderer selected by `CatalogId`), *Observer* (publish event → projection + cache invalidation), *Chain of Responsibility* (cache → load → resolve → render → emit).

---

## Pattern Reference Summary

| GoF Pattern | Where Applied |
|---|---|
| **Composite** | `NeoPageNode` tree — uniform treatment of leaf nodes and containers |
| **Command** | Every user mutation: `CompositionDropRequest`, undo/redo actions |
| **Memento** | `EditorNodeMemento`, `EditorBlockListMemento` — snapshot-based undo/redo |
| **Strategy** | `ICompositionPolicy`, `IEditorNodeActionProvider`, renderer selection by CatalogId |
| **Template Method** | `PageEditorCatalogDefinitionBase` skeleton → concrete overrides |
| **Factory Method** | `INeoNodeFactory.CreateDefaultNode()` — each definition creates its own default |
| **Adapter** | `LegacyPageEditorDefinitionAdapter` — bridges old `IPageEditorBlockDefinition` |
| **Bridge** | Interface hierarchy (catalog) separate from implementation hierarchy (services) |
| **Observer** | Publish event triggers inline projection + cache invalidation |
| **Chain of Responsibility** | Public request: cache → MartenDB → resolve → render → emit |
| **Single Responsibility** | Each service owns one concern: registry stores, policy validates, provider builds |

---

## SOLID Principles Applied

| Principle | How |
|---|---|
| **S** | `IPageEditorDefinitionRegistry` stores; `ICompositionPolicy` validates; `IEditorNodeActionProvider` builds menus; renderers render; editors edit |
| **O** | New block type = new concrete class + optional provider registration. No switch cases |
| **L** | Any `IPageEditorCatalogDefinition` implementation is a valid canvas participant |
| **I** | `EditorInteractionCapabilities` separate from `EditorCapabilitySet`; `INeoNodeFactory` separate from `INeoNodeBlockMapper` |
| **D** | UI depends on `IPageEditorDefinitionRegistry`, not a concrete static class |

---

*Generated from the AeroCMS codebase at `src/Aero.Cms.Abstractions/Blocks/Editor/`, `src/Aero.Cms.Shared/Pages/Manager/PageEditor/`, and `docs/ux-refactor.md`.*
