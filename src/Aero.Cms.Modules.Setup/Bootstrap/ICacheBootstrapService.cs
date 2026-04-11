namespace Aero.Cms.Modules.Setup.Bootstrap;

public interface ICacheBootstrapService
{
    Task PersistAsync(CacheBootstrapModel model, CancellationToken cancellationToken = default);
}
