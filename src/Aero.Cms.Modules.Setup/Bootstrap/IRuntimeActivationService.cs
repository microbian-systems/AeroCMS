namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Coordinates one in-process runtime activation after bootstrap configuration is persisted.
/// </summary>
public interface IRuntimeActivationService
{
    /// <summary>
    /// Gets whether setup completion, pending-payload cleanup, and the running-state write succeeded.
    /// </summary>
    bool IsActivated { get; }

    /// <summary>
    /// Gets the activation gate's backing latch value.
    /// </summary>
    /// <remarks>
    /// The latch becomes <see langword="true"/> when an attempt claims the gate and is cleared
    /// after success or a caught exception. Expected workflow failures returned after the gate
    /// is claimed currently leave it set, causing later calls to report that activation is
    /// already in progress.
    /// </remarks>
    bool IsActivating { get; }

    /// <summary>
    /// Gets the message from an exception caught during the most recent activation attempt, if any.
    /// </summary>
    string? ActivationError { get; }

    /// <summary>
    /// Completes setup from the persisted pending request and marks bootstrap as running.
    /// </summary>
    /// <param name="cancellationToken">Cancels pending-request, setup-completion, and persistence operations.</param>
    /// <returns>A result describing success, duplicate activation, invalid state, missing payload, or a caught failure.</returns>
    Task<RuntimeActivationResult> ActivateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for this service instance to signal a successful activation.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait without cancelling an activation already in progress.</param>
    /// <returns>A task that completes immediately when already activated, or after the activation signal is consumed.</returns>
    Task WaitForActivationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes the observable outcome of a runtime activation request.
/// </summary>
public sealed class RuntimeActivationResult
{
    /// <summary>
    /// Gets or sets whether activation completed successfully.
    /// </summary>
public bool Succeeded { get; set; }
    /// <summary>
    /// Gets or sets the failure message returned to the caller.
    /// </summary>
public string? Error { get; set; }
    /// <summary>
    /// Gets or sets a non-fatal condition, such as a request made after activation already completed.
    /// </summary>
public string? Warning { get; set; }

    /// <summary>
    /// Creates a successful result with an optional warning.
    /// </summary>
    /// <param name="warning">An optional non-fatal condition to expose to the caller.</param>
    /// <returns>A successful activation result.</returns>
public static RuntimeActivationResult Success(string? warning = null) => new()
    {
        Succeeded = true,
        Warning = warning
    };

    /// <summary>
    /// Creates a failed result without throwing.
    /// </summary>
    /// <param name="error">The failure message to expose to the caller.</param>
    /// <returns>A failed activation result.</returns>
public static RuntimeActivationResult Failed(string error) => new()
    {
        Succeeded = false,
        Error = error
    };
}
