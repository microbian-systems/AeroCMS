using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// Creates a new node with definition-owned defaults.
/// </summary>
public interface INeoNodeFactory
{
    NeoPageNode CreateDefaultNode();
}
