namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

/// <summary>
/// Authoritative validator for add, move, re-parent, paste, template, load, and save operations.
/// </summary>
public interface ICompositionPolicy
{
    Result<bool, AeroError> ValidatePlacement(
        NeoPageNode child,
        NeoPageNode? parent,
        string dropZoneId,
        CompositionTreeContext context);
}
