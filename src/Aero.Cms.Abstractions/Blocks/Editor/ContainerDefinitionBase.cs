using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Base class for nodes that may contain child nodes through declared drop
/// zones and composition policy validation. Extends
/// <see cref="PrimitiveDefinitionBase"/> — containers are still embeddable.
/// Kind is fixed to <see cref="NeoPageNodeKind.Container"/>.
///
/// Transitional. Final once Container, Columns, and Grid inherit from it.
/// Drop-zone definitions, nesting rules, and editing behavior belong to the
/// catalog definition's <see cref="ICompositionCapabilities"/> and the
/// central <see cref="ICompositionPolicy"/>.
///
/// Expected concrete concrete classes: ContainerPrimitiveDefinition,
/// ColumnsDefinition, GridDefinition.
/// </summary>
public abstract class ContainerDefinitionBase : PrimitiveDefinitionBase
{
    public override NeoPageNodeKind Kind => NeoPageNodeKind.Container;
}
