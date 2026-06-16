using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Abstract base class for catalog definitions. Removes repeated metadata
/// defaults while keeping behavior in policies, commands, renderers, and
/// editors. Members with virtual defaults should be overridden only when the
/// concrete definition has a meaningful value.
///
/// Transitional: this base class is introduced during Phase 0.5 catalog
/// consolidation. It implements <see cref="IPageEditorCatalogDefinition"/>,
/// <see cref="INeoNodeFactory"/>, and <see cref="IEditorInteractionProvider"/>.
///
/// Pattern: Template Method. Concrete definitions override abstract members to
/// declare identity, composition rules, and capabilities. Mutation, rendering,
/// editing, and persistence behavior belong to separate command/policy/renderer/
/// editor services, not to this class.
///
/// Expected concrete hierarchies:
///   PrimitiveDefinitionBase   -> Text, Button, Image, Pill, Icon, Separator
///   ContainerDefinitionBase   -> Container, Columns, Grid
///   ComponentDefinitionBase   -> Card, CustomComponent (deferred)
///   CannedBlockDefinitionBase -> Hero, Pricing, Features (deferred)
/// </summary>
public abstract class PageEditorCatalogDefinitionBase :
    IPageEditorCatalogDefinition,
    INeoNodeFactory,
    IEditorInteractionProvider
{
    public abstract string CatalogId { get; }
    public abstract string DisplayName { get; }
    public virtual string? Description => null;
    public abstract string Category { get; }
    public abstract NeoPageNodeKind Kind { get; }
    public virtual string IconName => "unknown";
    public virtual int SortOrder => 0;
    public virtual bool PublicStaticSsrSafe => true;
    public virtual Type? PreviewComponentType => null;
    public virtual Type? PropertyEditorComponentType => null;
    public abstract ICompositionCapabilities Composition { get; }
    public abstract EditorCapabilitySet EditorCapabilities { get; }

    /// <summary>
    /// Declares which canvas interactions are available.
    /// Abstract — each concrete definition must explicitly declare its
    /// capabilities. This prevents accidental grants from inherited defaults.
    /// </summary>
    public abstract EditorInteractionCapabilities Interaction { get; }

    public abstract NeoPageNode CreateDefaultNode();
}
