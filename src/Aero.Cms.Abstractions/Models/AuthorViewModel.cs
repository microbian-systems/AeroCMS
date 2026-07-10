namespace Aero.Cms.Abstractions.Models;

/// <summary>
/// Represents a record for AuthorViewModel.
/// </summary>
[Alias("AuthorViewModel")]
[GenerateSerializer]
public record AuthorViewModel : AeroEntityViewModel
{
        /// <summary>
    /// Gets or sets the user Id.
    /// </summary>
[Id(0)]
    public long  userId { get; set; }
        /// <summary>
    /// Gets or sets the Name.
    /// </summary>
[Id(1)]
    public string? Name {get;set; }
        /// <summary>
    /// Gets or sets the Avatar Url.
    /// </summary>
[Id(2)]
    public string? AvatarUrl {get;set; }
}

/// <summary>
/// Represents a record for AuthorErrorViewModel.
/// </summary>
[GenerateSerializer]
[Alias("AuthorErrorViewModel")]
public record AuthorErrorViewModel : AeroErrorViewModel<AuthorViewModel>;