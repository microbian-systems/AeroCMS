namespace Aero.Cms.Abstractions.Models;

[Alias("AeroRequestResponse")]
[GenerateSerializer]
public record AeroRequestResponse<T, TError>(T data, TError error)
    where T : AeroEntityViewModel
    where TError : AeroErrorViewModel<T>;


[Alias("AeroRequestResponse<T>")]
[GenerateSerializer]
public sealed record AeroRequestResponse<T>(T data, AeroErrorViewModel<T> error)
    : AeroRequestResponse<T, AeroErrorViewModel<T>>(data, error)
    where T : AeroEntityViewModel;

