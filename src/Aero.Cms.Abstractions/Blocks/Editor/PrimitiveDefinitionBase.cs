using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Blocks.Neo.Composition;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Base class for embeddable leaf items such as Text, Button, Pill, Icon, and
/// Image primitives. Kind is fixed to <see cref="NeoPageNodeKind.Primitive"/>.
/// Implements <see cref="IEmbeddable"/> so every leaf primitive participates in
/// composition. Interaction is deliberately left abstract — each concrete leaf
/// definition must explicitly declare its interaction capabilities.
///
/// Transitional. Final once all primitives inherit from it. Composition rules,
/// rendering, and editing behavior belong to policies and services.
/// </summary>
public abstract class PrimitiveDefinitionBase :
    PageEditorCatalogDefinitionBase,
    IEmbeddable
{
        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public override NeoPageNodeKind Kind => NeoPageNodeKind.Primitive;
}
