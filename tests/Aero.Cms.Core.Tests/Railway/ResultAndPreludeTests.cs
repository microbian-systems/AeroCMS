using Aero.Core;
using Aero.Core.Railway;
using FluentAssertions;

namespace Aero.Cms.Core.Tests.Railway;

public class ResultAndPreludeTests
{
    // ── Result<T> inner types ────────────────────────────────────────────

    [Test]
    public void ResultT_Ok_should_have_correct_value()
    {
        var result = new Result<string>.Ok("hello");

        result.Value.Should().Be("hello");
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Test]
    public void ResultT_Failure_should_have_correct_error()
    {
        var error = new AeroError.NotFound("missing");

        var result = new Result<string>.Failure(error);

        result.Error.Should().Be(error);
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
    }

    // ── Prelude.Ok<TValue> and Prelude.Fail<TValue> ──────────────────────

    [Test]
    public void Prelude_Ok_should_return_ResultT_Ok()
    {
        Result<string> result = Prelude.Ok("success");

        result.Should().BeOfType<Result<string>.Ok>();
        result.As<Result<string>.Ok>().Value.Should().Be("success");
    }

    [Test]
    public void Prelude_Fail_should_return_ResultT_Failure()
    {
        var error = new AeroError.NotFound("not found");

        Result<string> result = Prelude.Fail<string>(error);

        result.Should().BeOfType<Result<string>.Failure>();
        result.As<Result<string>.Failure>().Error.Should().Be(error);
    }

    [Test]
    public void Prelude_Ok_should_work_with_int_value_type()
    {
        var result = Prelude.Ok(42);

        result.Should().BeOfType<Result<int>.Ok>();
        result.As<Result<int>.Ok>().Value.Should().Be(42);
    }

    [Test]
    public void Prelude_Fail_should_work_with_int_value_type()
    {
        var error = AeroError.CreateError("fail");

        var result = Prelude.Fail<int>(error);

        result.Should().BeOfType<Result<int>.Failure>();
        result.As<Result<int>.Failure>().Error.Should().Be(error);
    }

    // ── Result<T> IS-A Result<T, AeroError> ──────────────────────────────

    [Test]
    public void ResultT_Ok_should_be_assignable_to_base_ResultT_TError()
    {
        Result<string, AeroError> result = new Result<string>.Ok("value");

        result.Should().BeOfType<Result<string>.Ok>();
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void ResultT_Failure_should_be_assignable_to_base_ResultT_TError()
    {
        var error = new AeroError.Timeout("timed out");
        Result<string, AeroError> result = new Result<string>.Failure(error);

        result.Should().BeOfType<Result<string>.Failure>();
        result.IsFailure.Should().BeTrue();
    }

    // ── Prelude existing Ok<TValue, TError> / Fail<TValue, TError> still works ──

    [Test]
    public void Prelude_Ok_TypedError_should_still_work()
    {
        Result<string, AeroError> result = Prelude.Ok<string, AeroError>("works");

        result.Should().BeOfType<Result<string, AeroError>.Ok>();
        result.As<Result<string, AeroError>.Ok>().Value.Should().Be("works");
    }

    [Test]
    public void Prelude_Fail_TypedError_should_still_work()
    {
        var error = new AeroError.Conflict("conflict");

        Result<string, AeroError> result = Prelude.Fail<string, AeroError>(error);

        result.Should().BeOfType<Result<string, AeroError>.Failure>();
        result.As<Result<string, AeroError>.Failure>().Error.Should().Be(error);
    }

    // ── Implicit conversions ──────────────────────────────────────────────

    [Test]
    public void Implicit_conversion_from_value_to_ResultT_TError_should_create_Ok()
    {
        Result<string, AeroError> result = "implicit ok";

        result.Should().BeOfType<Result<string, AeroError>.Ok>();
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Implicit_conversion_from_value_to_ResultT_should_create_Ok()
    {
        Result<string> result = "implicit ok";

        result.Should().BeOfType<Result<string>.Ok>();
        result.As<Result<string>.Ok>().Value.Should().Be("implicit ok");
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Implicit_conversion_from_error_to_ResultT_TError_should_create_Failure()
    {
        Result<string, AeroError> result = new AeroError.NotFound("implicit fail");

        result.Should().BeOfType<Result<string, AeroError>.Failure>();
        result.IsFailure.Should().BeTrue();
    }

    [Test]
    public void Implicit_conversion_from_AeroError_to_ResultT_should_create_Failure()
    {
        var error = new AeroError.NotFound("not found");
        Result<string> result = error;

        result.Should().BeOfType<Result<string>.Failure>();
        result.As<Result<string>.Failure>().Error.Should().Be(error);
        result.IsFailure.Should().BeTrue();
    }

    [Test]
    public void ToDefaultResult_should_convert_existing_ResultT_AeroError_Ok_to_ResultT()
    {
        Result<string, AeroError> legacy = Prelude.Ok<string, AeroError>("legacy ok");

        Result<string> result = legacy.ToDefaultResult();

        result.Should().BeOfType<Result<string>.Ok>();
        result.As<Result<string>.Ok>().Value.Should().Be("legacy ok");
    }

    [Test]
    public void ToDefaultResult_should_convert_existing_ResultT_AeroError_Failure_to_ResultT()
    {
        var error = new AeroError.Conflict("legacy fail");
        Result<string, AeroError> legacy = Prelude.Fail<string, AeroError>(error);

        Result<string> result = legacy.ToDefaultResult();

        result.Should().BeOfType<Result<string>.Failure>();
        result.As<Result<string>.Failure>().Error.Should().Be(error);
    }

    // ── ToString ──────────────────────────────────────────────────────────

    [Test]
    public void ResultT_Ok_ToString_should_return_value_string()
    {
        var result = new Result<int>.Ok(42);

        result.ToString().Should().Be("42");
    }

    [Test]
    public void ResultT_Failure_ToString_should_return_error_string()
    {
        var error = new AeroError.NotFound("missing resource");
        var result = new Result<string>.Failure(error);

        result.ToString().Should().Contain("missing resource");
    }

    // ── Pattern matching ──────────────────────────────────────────────────

    [Test]
    public void ResultT_Ok_should_match_Ok_pattern()
    {
        var result = Prelude.Ok("match me");

        var matched = result switch
        {
            Result<string>.Ok(var v) => v,
            Result<string>.Failure => "failed",
            _ => "unreachable"
        };

        matched.Should().Be("match me");
    }

    [Test]
    public void ResultT_Failure_should_match_Failure_pattern()
    {
        var result = Prelude.Fail<string>(new AeroError.NotFound("gone"));

        var matched = result switch
        {
            Result<string>.Ok => "ok",
            Result<string>.Failure(var e) => e.ToString(),
            _ => "unreachable"
        };

        matched.Should().Contain("gone");
    }

    // ── Prelude Option helpers (regression check) ─────────────────────────

    [Test]
    public void Prelude_Some_should_return_Option_Some()
    {
        Option<string> option = Prelude.Some("value");

        option.Should().BeOfType<Option<string>.Some>();
        option.IsSome.Should().BeTrue();
    }

    [Test]
    public void Prelude_None_should_return_Option_None()
    {
        Option<string> option = Prelude.None;

        option.Should().BeOfType<Option<string>.None>();
        option.IsNone.Should().BeTrue();
    }
}
