namespace Aero.Cms.Web.Core.Diagnostics;

/// <summary>
/// Extracts actionable leaf exceptions from wrapper and aggregate exceptions.
/// </summary>
public static class ExceptionDiagnostics
{
    /// <summary>
    /// Returns the deepest exception from each branch of an exception tree.
    /// </summary>
    public static IReadOnlyList<Exception> GetRootCauses(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var rootCauses = new List<Exception>();
        CollectRootCauses(exception, rootCauses);
        return rootCauses;
    }

    private static void CollectRootCauses(Exception exception, ICollection<Exception> rootCauses)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                CollectRootCauses(innerException, rootCauses);
            }

            return;
        }

        if (exception.InnerException is not null)
        {
            CollectRootCauses(exception.InnerException, rootCauses);
            return;
        }

        rootCauses.Add(exception);
    }
}
