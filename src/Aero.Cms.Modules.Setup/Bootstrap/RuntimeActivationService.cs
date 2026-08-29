using System.Threading.Channels;
using Aero.AppServer.Startup;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Serializes in-process activation and coordinates setup completion with bootstrap state persistence.
/// </summary>
/// <remarks>
/// Only one activation attempt may run at a time. Expected workflow failures are returned
/// as <see cref="RuntimeActivationResult"/> values; unexpected exceptions are logged,
/// captured as the activation error, and converted to failed results. After an attempt claims
/// the gate, an expected invalid-state, missing-payload, or unsuccessful-completion result
/// leaves the activation latch set and prevents a later retry on this service instance.
/// </remarks>
public sealed class RuntimeActivationService : IRuntimeActivationService, IDisposable
{
    private readonly ISetupInitializationService _setupInitializationService;
    private readonly ISetupCompletionService _setupCompletionService;
    private readonly IBootstrapPendingSetupRequestStore _pendingSetupRequestStore;
    private readonly IBootstrapCompletionWriter _completionWriter;
    private readonly ResolvedInfrastructureSettings _infrastructureSettings;
    private readonly ILogger<RuntimeActivationService> _logger;
    private readonly Channel<bool> _activationChannel = Channel.CreateBounded<bool>(1);
    private readonly Lock _lock = new();

    private bool _isActivated;
    private bool _isActivating;
    private string? _activationError;

    /// <inheritdoc />
public bool IsActivated => _isActivated;
    /// <inheritdoc />
public bool IsActivating => _isActivating;
    /// <inheritdoc />
public string? ActivationError => _activationError;

    /// <summary>
    /// Initializes a runtime activation coordinator.
    /// </summary>
    /// <param name="setupInitializationService">Provides the current persisted bootstrap state.</param>
    /// <param name="setupCompletionService">Performs setup seeding and completion.</param>
    /// <param name="pendingSetupRequestStore">Provides and removes the protected pending setup request.</param>
    /// <param name="completionWriter">Persists the transition to the running state.</param>
    /// <param name="infrastructureSettings">Provides the runtime database target selected from persisted configuration.</param>
    /// <param name="logger">Records activation decisions and failures.</param>
public RuntimeActivationService(
        ISetupInitializationService setupInitializationService,
        ISetupCompletionService setupCompletionService,
        IBootstrapPendingSetupRequestStore pendingSetupRequestStore,
        IBootstrapCompletionWriter completionWriter,
        ResolvedInfrastructureSettings infrastructureSettings,
        ILogger<RuntimeActivationService> logger)
    {
        _setupInitializationService = setupInitializationService;
        _setupCompletionService = setupCompletionService;
        _pendingSetupRequestStore = pendingSetupRequestStore;
        _completionWriter = completionWriter;
        _infrastructureSettings = infrastructureSettings;
        _logger = logger;
    }

    /// <inheritdoc />
public async Task<RuntimeActivationResult> ActivateAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isActivated)
            {
                _logger.LogWarning("Runtime already activated.");
                return RuntimeActivationResult.Success("Runtime was already activated.");
            }

            if (_isActivating)
            {
                _logger.LogWarning("Runtime activation already in progress.");
                return RuntimeActivationResult.Failed("Runtime activation already in progress.");
            }

            _isActivating = true;
        }

        try
        {
            _logger.LogInformation("Starting runtime activation...");

            var bootstrap = _setupInitializationService.GetBootstrapState();

            if (!bootstrap.IsConfiguredMode)
            {
                var error = $"Cannot activate runtime: bootstrap state is '{bootstrap.State}', expected 'Configured'.";
                _logger.LogError(error);
                return RuntimeActivationResult.Failed(error);
            }

            var request = await _pendingSetupRequestStore.LoadAsync(cancellationToken);
            if (request == null)
            {
                var error = "Bootstrap state is Configured but no pending seed payload exists.";
                _logger.LogError(error);
                return RuntimeActivationResult.Failed(error);
            }

            var scopeError = BootstrapDatabaseScopeGuard.GetValidationError(request, _infrastructureSettings);
            if (scopeError is not null)
            {
                _logger.LogError(scopeError);
                return RuntimeActivationResult.Failed(scopeError);
            }

            _logger.LogInformation("Executing setup completion...");

            var result = await _setupCompletionService.CompleteAsync(request, cancellationToken);
            if (!result.Succeeded)
            {
                var error = $"Setup completion failed: {string.Join("; ", result.Errors)}";
                _logger.LogError(error);
                return RuntimeActivationResult.Failed(error);
            }

            await _pendingSetupRequestStore.ClearAsync(cancellationToken);

            await _completionWriter.MarkCompleteAsync(cancellationToken);

            lock (_lock)
            {
                _isActivated = true;
                _isActivating = false;
            }

            // Signal any waiters
            await _activationChannel.Writer.WriteAsync(true, cancellationToken);

            _logger.LogInformation("Runtime activation completed successfully.");
            return RuntimeActivationResult.Success();
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _isActivating = false;
                _activationError = ex.Message;
            }

            _logger.LogError(ex, "Runtime activation failed.");
            return RuntimeActivationResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
public async Task WaitForActivationAsync(CancellationToken cancellationToken = default)
    {
        if (_isActivated)
            return;

        await _activationChannel.Reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Completes the activation signal channel so outstanding or future waits can observe disposal.
    /// </summary>
public void Dispose()
    {
        _activationChannel.Writer.TryComplete();
    }
}
