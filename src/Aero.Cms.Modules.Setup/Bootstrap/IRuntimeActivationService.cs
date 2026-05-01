namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Service that handles in-process runtime activation after bootstrap configuration.
/// This allows the setup wizard to complete without requiring an application restart.
/// </summary>
public interface IRuntimeActivationService
{
    /// <summary>
    /// Gets whether the runtime has been activated.
    /// </summary>
    bool IsActivated { get; }

    /// <summary>
    /// Gets whether the runtime is currently activating.
    /// </summary>
    bool IsActivating { get; }

    /// <summary>
    /// Gets the activation error, if any.
    /// </summary>
    string? ActivationError { get; }

    /// <summary>
    /// Activates the runtime services in-process after bootstrap configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when activation is done.</returns>
    Task<RuntimeActivationResult> ActivateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for the runtime to be activated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the runtime is activated.</returns>
    Task WaitForActivationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of runtime activation.
/// </summary>
public sealed class RuntimeActivationResult
{
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public string? Warning { get; set; }

    public static RuntimeActivationResult Success(string? warning = null) => new()
    {
        Succeeded = true,
        Warning = warning
    };

    public static RuntimeActivationResult Failed(string error) => new()
    {
        Succeeded = false,
        Error = error
    };
}