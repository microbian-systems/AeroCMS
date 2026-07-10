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
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public abstract string CatalogId { get; }
        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public abstract string DisplayName { get; }
        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public virtual string? Description => null;
        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public abstract string Category { get; }
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public abstract NeoPageNodeKind Kind { get; }
        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public virtual string IconName => "unknown";
        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public virtual int SortOrder => 0;
        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public virtual bool PublicStaticSsrSafe => true;
        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public virtual Type? PreviewComponentType => null;
        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public virtual Type? PropertyEditorComponentType => null;
        /// <summary>
    /// Gets or sets the Composition.
    /// </summary>
public abstract ICompositionCapabilities Composition { get; }
        /// <summary>
    /// Gets or sets the Editor Capabilities.
    /// </summary>
public abstract EditorCapabilitySet EditorCapabilities { get; }

    /// <summary>
    /// Declares which canvas interactions are available.
    /// Abstract — each concrete definition must explicitly declare its
    /// capabilities. This prevents accidental grants from inherited defaults.
    /// </summary>
    public abstract EditorInteractionCapabilities Interaction { get; }

        /// <summary>
    /// CreateDefaultNode method.
    /// </summary>
public abstract NeoPageNode CreateDefaultNode();
}
