namespace Aero.Cms.Core.Pipelines;

/// <summary>
/// Represents a class for PipelineContext.
/// </summary>
public abstract class PipelineContext
{
        /// <summary>
    /// Gets or sets the Is Short Circuited.
    /// </summary>
public bool IsShortCircuited { get; private set; }
        /// <summary>
    /// Gets or sets the Short Circuit Reason.
    /// </summary>
public string? ShortCircuitReason { get; private set; }

        /// <summary>
    /// ShortCircuit method.
    /// </summary>
public void ShortCircuit(string reason)
    {
        IsShortCircuited = true;
        ShortCircuitReason = reason;
    }
}
