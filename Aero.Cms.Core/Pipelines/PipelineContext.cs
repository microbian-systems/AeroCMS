namespace Aero.Cms.Core.Pipelines;

public abstract class PipelineContext
{
    public bool IsShortCircuited { get; private set; }
    public string? ShortCircuitReason { get; private set; }

    public void ShortCircuit(string reason)
    {
        IsShortCircuited = true;
        ShortCircuitReason = reason;
    }
}
