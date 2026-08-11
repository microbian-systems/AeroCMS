namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Allows only one setup handoff to own process-wide configuration persistence.
/// </summary>
public sealed class SetupBootstrapHandoffGate
{
    private int _claimed;

    /// <summary>Attempts to claim the handoff for the current setup process.</summary>
    public bool TryClaim() => Interlocked.CompareExchange(ref _claimed, 1, 0) == 0;

    /// <summary>Releases a failed handoff so the operator can retry.</summary>
    public void Release() => Volatile.Write(ref _claimed, 0);
}
