using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Setup.Bootstrap;

/// <summary>
/// Implementation of runtime activation service that coordinates in-process
/// activation of runtime services after bootstrap configuration.
/// </summary>
public sealed class RuntimeActivationService : IRuntimeActivationService, IDisposable
{
    private readonly ISetupInitializationService _setupInitializationService;
    private readonly ISetupCompletionService _setupCompletionService;
    private readonly IBootstrapPendingSetupRequestStore _pendingSetupRequestStore;
    private readonly IBootstrapCompletionWriter _completionWriter;
    private readonly ILogger<RuntimeActivationService> _logger;
    private readonly Channel<bool> _activationChannel = Channel.CreateBounded<bool>(1);
    private readonly Lock _lock = new();

    private bool _isActivated;
    private bool _isActivating;
    private string? _activationError;

    public bool IsActivated => _isActivated;
    public bool IsActivating => _isActivating;
    public string? ActivationError => _activationError;

    public RuntimeActivationService(
        ISetupInitializationService setupInitializationService,
        ISetupCompletionService setupCompletionService,
        IBootstrapPendingSetupRequestStore pendingSetupRequestStore,
        IBootstrapCompletionWriter completionWriter,
        ILogger<RuntimeActivationService> logger)
    {
        _setupInitializationService = setupInitializationService;
        _setupCompletionService = setupCompletionService;
        _pendingSetupRequestStore = pendingSetupRequestStore;
        _completionWriter = completionWriter;
        _logger = logger;
    }

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

    public async Task WaitForActivationAsync(CancellationToken cancellationToken = default)
    {
        if (_isActivated)
            return;

        await _activationChannel.Reader.ReadAsync(cancellationToken);
    }

    public void Dispose()
    {
        _activationChannel.Writer.TryComplete();
    }
}