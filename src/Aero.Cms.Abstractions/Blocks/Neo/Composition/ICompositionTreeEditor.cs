namespace Aero.Cms.Abstractions.Blocks.Neo.Composition;

public interface ICompositionTreeEditor
{
    Result<IReadOnlyList<NeoPageNode>, AeroError> Drop(
        IReadOnlyList<NeoPageNode> roots,
        CompositionDropRequest request);

    Result<IReadOnlyList<NeoPageNode>, AeroError> Remove(
        IReadOnlyList<NeoPageNode> roots,
        string nodeId);
}
