namespace Aero.Cms.Web.Core.Pipelines;

/// <summary>
/// Defines an interface for IPageReadHook.
/// </summary>
public interface IPageReadHook
{
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
int Order { get; }
        /// <summary>
    /// ExecuteAsync method.
    /// </summary>
Task ExecuteAsync(PageReadContext ctx, CancellationToken ct);
}

/// <summary>
/// Defines an interface for IPageSaveHook.
/// </summary>
public interface IPageSaveHook
{
        /// <summary>
    /// Gets or sets the Order.
    /// </summary>
int Order { get; }
        /// <summary>
    /// ExecuteAsync method.
    /// </summary>
Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct);
}
