namespace Aero.Cms.Core.Pipelines;

/// <summary>
/// Represents mutable state shared by a processing pipeline.
/// </summary>
/// <remarks>
/// Setting the short-circuit flag does not stop execution automatically. Pipeline components
/// must inspect <see cref="IsShortCircuited"/> and decide whether to return.
/// </remarks>
public abstract class PipelineContext
{
    /// <summary>Gets whether pipeline processing has been stopped early.</summary>
    public bool IsShortCircuited { get; private set; }

    /// <summary>Gets the reason supplied when processing was stopped early.</summary>
    public string? ShortCircuitReason { get; private set; }

    /// <summary>Marks the context as short-circuited and records the supplied reason.</summary>
    /// <param name="reason">The reason for stopping processing.</param>
    /// <remarks>Calling this method again leaves the flag set and replaces the previous reason.</remarks>
    public void ShortCircuit(string reason)
    {
        IsShortCircuited = true;
        ShortCircuitReason = reason;
    }
}
