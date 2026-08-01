using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Grpc;

// Defines .NET interface as a Server/Client IDL.
// The interface is shared between server and client.
/// <summary>
/// Defines a MagicOnion unary RPC contract that adds two 32-bit integers.
/// </summary>
/// <remarks>
/// The contract declares no streaming operation, request context, tenant/user scope, authentication metadata,
/// deadline, retry policy, or explicit cancellation token.
/// </remarks>
public interface IMyFirstService : IService<IMyFirstService>
{
    // The return type must be `UnaryResult<T>` or `UnaryResult`.
        /// <summary>
    /// Adds two integers and returns the result as a unary MagicOnion response.
    /// </summary>
    /// <param name="x">The first operand.</param>
    /// <param name="y">The second operand.</param>
    /// <returns>A unary result containing <paramref name="x"/> plus <paramref name="y"/>.</returns>
UnaryResult<int> SumAsync(int x, int y);
    // UnaryResult<int> Foo();
    // UnaryResult<int> Bar();
    // UnaryResult<int> Baz();
}


// Implements RPC service in the server project.
// The implementation class must inherit `ServiceBase<IMyFirstService>` and `IMyFirstService`
/// <summary>
/// Implements the sample integer-addition RPC contract.
/// </summary>
/// <remarks>
/// The type can be discovered by MagicOnion after service registration, but this project does not map a MagicOnion
/// endpoint. It applies <see cref="MyServiceFilterAttribute"/> and performs no authentication, authorization,
/// tenant/user lookup, validation, deadline handling, or explicit cancellation handling.
/// </remarks>
[FromServiceFilter(typeof(MyServiceFilterAttribute))]
public class MyFirstService : ServiceBase<IMyFirstService>, IMyFirstService
{
    // `UnaryResult<T>` allows the method to be treated as `async` method.
        /// <summary>
    /// Writes the supplied operands to standard output and returns their sum.
    /// </summary>
    /// <param name="x">The first operand, also included in the console message.</param>
    /// <param name="y">The second operand, also included in the console message.</param>
    /// <returns>A unary result containing the integer sum.</returns>
public async UnaryResult<int> SumAsync(int x, int y)
    {
        Console.WriteLine($"Received:{x}, {y}");
        var sum = x + y;
        return sum;
    }

    // public UnaryResult<int> Foo()
    // {
    //     var value = 1;
    //     return value;
    // }
    //
    // public UnaryResult<int> Bar()
    // {
    //     return 5 + 3;
    // }
    //
    // public UnaryResult<int> Baz()
    // {
    //     return 5 + 3;
    // }
}

/// <summary>
/// Logs the MagicOnion service context before and after a service invocation.
/// </summary>
/// <remarks>
/// The context text may include request or transport metadata; this filter performs no redaction. If the downstream
/// delegate throws or is cancelled, the exception propagates and the post-invocation log statement is not reached.
/// The filter does not authenticate, authorize, retry, map failures, or enforce message limits.
/// </remarks>
public class MyServiceFilterAttribute(ILogger<MyServiceFilterAttribute> logger) : MagicOnionFilterAttribute
{
    private readonly ILogger _logger = logger;

    // the `logger` parameter will be injected at instantiating.

        /// <summary>
    /// Logs the context, awaits the downstream filter/service delegate, and logs the context again on success.
    /// </summary>
    /// <param name="context">The MagicOnion invocation context rendered into both log messages.</param>
    /// <param name="next">The next filter or service delegate.</param>
public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        _logger.LogInformation($"MyServiceFilter Begin: {context.ToString()}");
        await next(context);
        _logger.LogInformation($"MyServiceFilter End: {context.ToString()}");
    }
}
