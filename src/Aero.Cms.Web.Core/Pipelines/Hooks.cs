namespace Aero.Cms.Web.Core.Pipelines;

/// <summary>
/// Defines an ordered participant in a page-read pipeline.
/// </summary>
public interface IPageReadHook
{
        /// <summary>
    /// Gets the relative execution order; lower values run before higher values when the runner honors it.
    /// </summary>
int Order { get; }
        /// <summary>
    /// Observes or mutates the shared read context.
    /// </summary>
    /// <param name="ctx">The shared page-read context.</param>
    /// <param name="ct">The pipeline cancellation token.</param>
    /// <remarks>Failure, cancellation, short-circuit, and side-effect behavior are implementation-defined.</remarks>
Task ExecuteAsync(PageReadContext ctx, CancellationToken ct);
}

/// <summary>
/// Defines an ordered participant in a page-save pipeline.
/// </summary>
public interface IPageSaveHook
{
        /// <summary>
    /// Gets the relative execution order; lower values run before higher values when the runner honors it.
    /// </summary>
int Order { get; }
        /// <summary>
    /// Observes or mutates the shared save context.
    /// </summary>
    /// <param name="ctx">The shared page-save context.</param>
    /// <param name="ct">The pipeline cancellation token.</param>
    /// <remarks>The contract itself does not persist, authorize, transact, retry, or map failures.</remarks>
Task ExecuteAsync(PageSaveContext ctx, CancellationToken ct);
}
