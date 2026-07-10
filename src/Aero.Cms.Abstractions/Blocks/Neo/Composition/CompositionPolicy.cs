namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Validates placement using catalog-level composition capabilities.
/// </summary>
public sealed class CompositionPolicy(ICompositionCapabilityResolver capabilityResolver)
    : ICompositionPolicy
{
        /// <summary>
    /// ValidatePlacement method.
    /// </summary>
public Result<bool, AeroError> ValidatePlacement(
        NeoPageNode child,
        NeoPageNode? parent,
        string dropZoneId,
        CompositionTreeContext context)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(context);

        if (!capabilityResolver.TryGet(child.CatalogId, out var childCapabilities))
        {
            return Invalid($"No composition capabilities are registered for '{child.CatalogId}'.");
        }

        if (!childCapabilities.IsEmbeddable)
        {
            return Invalid($"'{child.CatalogId}' cannot be embedded in a composition.");
        }

        if (parent is null)
        {
            return true;
        }

        if (string.Equals(child.NodeId, parent.NodeId, StringComparison.Ordinal) ||
            context.MovingNodeDescendantIds.Contains(parent.NodeId))
        {
            return Invalid("A node cannot be placed inside itself or one of its descendants.");
        }

        if (!capabilityResolver.TryGet(parent.CatalogId, out var parentCapabilities))
        {
            return Invalid($"No composition capabilities are registered for '{parent.CatalogId}'.");
        }

        if (!parentCapabilities.CanContainChildren)
        {
            return Invalid($"'{parent.CatalogId}' cannot contain child nodes.");
        }

        if (!parentCapabilities.AllowedChildKinds.Contains(child.Kind))
        {
            return Invalid($"'{parent.CatalogId}' does not accept child kind '{child.Kind}'.");
        }

        if (!childCapabilities.AllowedParentKinds.Contains(parent.Kind))
        {
            return Invalid($"'{child.CatalogId}' cannot be placed inside parent kind '{parent.Kind}'.");
        }

        // --- Catalog-ID-level containment rules (optional, nil means not enforced) ---
        if (parentCapabilities.AllowedChildCatalogIds is { Count: > 0 } childCatalogIds
            && !childCatalogIds.Contains(child.CatalogId))
        {
            return Invalid($"Child '{child.CatalogId}' is not allowed inside parent '{parent.CatalogId}'. " +
                           $"Allowed child catalog IDs: [{string.Join(", ", childCatalogIds)}].");
        }

        if (childCapabilities.AllowedParentCatalogIds is { Count: > 0 } parentCatalogIds
            && !parentCatalogIds.Contains(parent.CatalogId))
        {
            return Invalid($"Child '{child.CatalogId}' cannot be placed inside parent '{parent.CatalogId}'. " +
                           $"Allowed parent catalog IDs: [{string.Join(", ", parentCatalogIds)}].");
        }

        var dropZone = parentCapabilities.SupportedDropZones.FirstOrDefault(
            zone => string.Equals(zone.Id, dropZoneId, StringComparison.Ordinal));

        if (dropZone is null)
        {
            return Invalid($"Drop zone '{dropZoneId}' is not defined by '{parent.CatalogId}'.");
        }

        if (!dropZone.AllowedChildKinds.Contains(child.Kind))
        {
            return Invalid($"Drop zone '{dropZoneId}' does not accept child kind '{child.Kind}'.");
        }

        var maximumChildren = Min(parentCapabilities.MaximumChildren, dropZone.MaximumChildren);
        var effectiveChildCount = context.ExistingChildrenInDropZone -
                                  (context.MovingNodeAlreadyInTargetDropZone ? 1 : 0);
        if (maximumChildren is { } maximum &&
            effectiveChildCount >= maximum)
        {
            return Invalid($"Drop zone '{dropZoneId}' allows at most {maximum} child node(s).");
        }

        return true;
    }

    private static int? Min(int? left, int? right) =>
        (left, right) switch
        {
            (null, null) => null,
            (not null, null) => left,
            (null, not null) => right,
            _ => Math.Min(left!.Value, right!.Value)
        };

    private static Result<bool, AeroError> Invalid(string message) =>
        AeroError.ValidationError([message]);
}
