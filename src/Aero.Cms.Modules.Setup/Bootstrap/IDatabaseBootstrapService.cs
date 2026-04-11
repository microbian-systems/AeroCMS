namespace Aero.Cms.Modules.Setup.Bootstrap;

public interface IDatabaseBootstrapService
{
    Task PersistAsync(DatabaseBootstrapModel model, CancellationToken cancellationToken = default);
}
