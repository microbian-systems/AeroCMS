using Aero.Cms.Abstractions.Content.Views;
using Aero.Cms.Core.Content.Services;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentViewExecutionLimitEnforcerTests
{
    [Test]
    public void Enforce_fails_closed_when_an_executor_exceeds_row_or_byte_limits()
    {
        var rows = new IReadOnlyDictionary<string, object?>[]
        {
            new Dictionary<string, object?> { ["name"] = new string('x', 64) },
            new Dictionary<string, object?> { ["name"] = "second" }
        };

        var result = new ContentViewExecutionResult(rows, false);
        Should.Throw<InvalidOperationException>(() => ContentViewExecutionLimitEnforcer.Enforce(result, 1, new ContentViewExecutionLimits(1, 1)));
        Should.Throw<InvalidOperationException>(() => ContentViewExecutionLimitEnforcer.Enforce(new ContentViewExecutionResult([rows[0]], false), 1,
            new ContentViewExecutionLimits(1, 1, MaximumBytes: 10)));
    }

    [Test]
    public void Enforce_fails_closed_when_nested_values_exceed_depth_limit()
    {
        object value = "leaf";
        for (var index = 0; index < 5; index++) value = new Dictionary<string, object?> { ["next"] = value };
        var result = new ContentViewExecutionResult([new Dictionary<string, object?> { ["value"] = value }], false);

        Should.Throw<InvalidOperationException>(() => ContentViewExecutionLimitEnforcer.Enforce(result, 1,
            new ContentViewExecutionLimits(1, 1, MaximumDepth: 3)));
    }
}
