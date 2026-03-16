using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.Logging;

namespace Aero.Cms.Modules.Grpc;

// Defines .NET interface as a Server/Client IDL.
// The interface is shared between server and client.
public interface IMyFirstService : IService<IMyFirstService>
{
    // The return type must be `UnaryResult<T>` or `UnaryResult`.
    UnaryResult<int> SumAsync(int x, int y);
    // UnaryResult<int> Foo();
    // UnaryResult<int> Bar();
    // UnaryResult<int> Baz();
}


// Implements RPC service in the server project.
// The implementation class must inherit `ServiceBase<IMyFirstService>` and `IMyFirstService`
[FromServiceFilter(typeof(MyServiceFilterAttribute))]
public class MyFirstService : ServiceBase<IMyFirstService>, IMyFirstService
{
    // `UnaryResult<T>` allows the method to be treated as `async` method.
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

public class MyServiceFilterAttribute(ILogger<MyServiceFilterAttribute> logger) : MagicOnionFilterAttribute
{
    private readonly ILogger _logger = logger;

    // the `logger` parameter will be injected at instantiating.

    public override async ValueTask Invoke(ServiceContext context, Func<ServiceContext, ValueTask> next)
    {
        _logger.LogInformation($"MyServiceFilter Begin: {context.ToString()}");
        await next(context);
        _logger.LogInformation($"MyServiceFilter End: {context.ToString()}");
    }
}