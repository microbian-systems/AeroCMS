using Aero.Core;
using Aero.Core.Railway;
using Microsoft.AspNetCore.Http;

namespace Aero.Cms.Modules.Pages;

/// <summary>
/// Maps <see cref="Result{T, AeroError}"/> to ASP.NET Core <see cref="IResult"/>
/// for Minimal API endpoints. Provides a single-call translation following
/// the Railway Oriented Programming pattern.
/// </summary>
public static class ResultMinimalApiExtensions
{
    /// <summary>
    /// Converts a Result to the appropriate HTTP response.
    /// </summary>
    public static IResult ToMinimalApiResult<T>(this Result<T, AeroError> result) => result switch
    {
        { IsSuccess: true } => Results.Ok(((Result<T, AeroError>.Ok)result).Value),
        _ => result switch
        {
            Result<T, AeroError>.Failure { Error: AeroError.NotFound nf } =>
                Results.NotFound(new { message = nf.msg }),

            Result<T, AeroError>.Failure { Error: AeroError.Conflict c } =>
                Results.Conflict(new { message = c.msg }),

            Result<T, AeroError>.Failure { Error: AeroError.Validation v } =>
                Results.ValidationProblem(
                    v.Errors.ToDictionary(e => e, _ => new[] { "Validation error" })),

            Result<T, AeroError>.Failure { Error: AeroError.Unauthorized } =>
                Results.Unauthorized(),

            Result<T, AeroError>.Failure { Error: AeroError.Forbidden } =>
                Results.Forbid(),

            Result<T, AeroError>.Failure { Error: AeroError.Database db } =>
                Results.Problem(db.msg, statusCode: StatusCodes.Status500InternalServerError),

            Result<T, AeroError>.Failure { Error: AeroError.BadRequest br } =>
                Results.BadRequest(new { message = br.msg }),

            Result<T, AeroError>.Failure { Error: AeroError.NotAllowed na } =>
                Results.Problem(na.msg, statusCode: StatusCodes.Status405MethodNotAllowed),

            Result<T, AeroError>.Failure { Error: var err } =>
                Results.Problem(err.ToString(), statusCode: StatusCodes.Status500InternalServerError),

            _ => Results.Problem("Unexpected result state.", statusCode: StatusCodes.Status500InternalServerError)
        },
    };
}
