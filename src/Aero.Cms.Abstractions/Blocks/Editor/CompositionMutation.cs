using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Abstractions.Blocks.Editor;

/// <summary>
/// An isolated before/after composition mutation emitted by an editor surface.
/// </summary>
public sealed record CompositionMutation(
    NeoPageNode Before,
    NeoPageNode After,
    string? CoalescingKey = null);
