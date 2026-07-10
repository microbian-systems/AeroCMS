namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for AeroErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("AeroErrorViewModel")]
public abstract record AeroErrorViewModel<T>
{
        /// <summary>
    /// Gets or sets the Message.
    /// </summary>
[Id(2000)]
    public string? Message { get; init; }
        /// <summary>
    /// Gets or sets the Errors.
    /// </summary>
[Id(2001)]
    public IList<string> Errors { get; init; } = [];
        /// <summary>
    /// Gets or sets the Data.
    /// </summary>
[Id(2002)]
    public T? Data { get; init; }
        /// <summary>
    /// Gets or sets the Success.
    /// </summary>
public bool Success => Errors.Count == 0;
}
