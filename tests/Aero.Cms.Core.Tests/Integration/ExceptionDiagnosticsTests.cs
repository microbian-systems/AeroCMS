using Aero.Cms.Web.Core.Diagnostics;
using Shouldly;

namespace Aero.Cms.Core.Tests.Integration;

public sealed class ExceptionDiagnosticsTests
{
    [Test]
    public void GetRootCausesFlattensAggregatesAndNestedExceptions()
    {
        var exception = new AggregateException(
            new InvalidOperationException("wrapper", new ArgumentException("invalid value")),
            new ObjectDisposedException("timeout source"));

        var rootCauses = ExceptionDiagnostics.GetRootCauses(exception);

        rootCauses.Count.ShouldBe(2);
        rootCauses[0].ShouldBeOfType<ArgumentException>();
        rootCauses[0].Message.ShouldBe("invalid value");
        rootCauses[1].ShouldBeOfType<ObjectDisposedException>();
    }

    [Test]
    public void GetRootCausesReturnsOriginalExceptionWhenThereIsNoInnerException()
    {
        var exception = new InvalidOperationException("database unavailable");

        var rootCauses = ExceptionDiagnostics.GetRootCauses(exception);

        rootCauses.ShouldHaveSingleItem().ShouldBeSameAs(exception);
    }
}
