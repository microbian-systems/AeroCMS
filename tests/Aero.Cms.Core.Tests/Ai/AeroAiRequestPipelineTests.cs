using System.Security.Claims;
using Aero.Cms.Abstractions.Ai.Pipeline;
using Aero.Cms.Modules.AiAssistant.Pipeline;
using Aero.Core;
using Aero.Core.Railway;
using Shouldly;

namespace Aero.Cms.Core.Tests.Ai;

public sealed class AeroAiRequestPipelineTests
{
    [Test]
    public async Task Pipeline_wraps_terminal_in_deterministic_stage_order()
    {
        var calls = new List<string>();
        var pipeline = new AeroAiRequestPipeline(
        [
            new RecordingStage("late", 300, calls),
            new RecordingStage("early", 100, calls),
            new RecordingStage("middle", 200, calls)
        ]);

        var result = await pipeline.ExecuteAsync(
            Context(),
            (_, _) =>
            {
                calls.Add("terminal");
                return Task.FromResult<Result<string>>("ok");
            });

        result.ShouldBeOfType<Result<string>.Ok>().Value.ShouldBe("ok");
        calls.ShouldBe(
        [
            "early:enter",
            "middle:enter",
            "late:enter",
            "terminal",
            "late:exit",
            "middle:exit",
            "early:exit"
        ]);
    }

    [Test]
    public async Task Pipeline_failure_stops_later_stages_and_terminal_execution()
    {
        var calls = new List<string>();
        var pipeline = new AeroAiRequestPipeline(
        [
            new RecordingStage("first", 100, calls),
            new FailingStage(200, calls),
            new RecordingStage("unreachable", 300, calls)
        ]);

        var terminalCalled = false;
        var result = await pipeline.ExecuteAsync(
            Context(),
            (_, _) =>
            {
                terminalCalled = true;
                return Task.FromResult<Result<string>>("unexpected");
            });

        result.ShouldBeOfType<Result<string>.Failure>();
        terminalCalled.ShouldBeFalse();
        calls.ShouldBe(["first:enter", "failure", "first:exit"]);
    }

    [Test]
    public async Task Manager_scope_and_input_stages_fail_closed_before_provider_execution()
    {
        var pipeline = new AeroAiRequestPipeline(
        [
            new AeroAiRequestNormalizationStage(),
            new AeroAiScopeStage(),
            new AeroAiInputSafetyStage()
        ]);
        var terminalCalls = 0;
        AeroAiPipelineNext<string> terminal = (_, _) =>
        {
            terminalCalls++;
            return Task.FromResult<Result<string>>("unexpected");
        };

        var anonymous = await pipeline.ExecuteAsync(
            Context(authenticated: false),
            terminal);
        var oversized = await pipeline.ExecuteAsync(
            Context(inputCharacters: 32_001),
            terminal);

        anonymous.ShouldBeOfType<Result<string>.Failure>()
            .Error.ShouldBeOfType<AeroError.Unauthorized>();
        oversized.ShouldBeOfType<Result<string>.Failure>()
            .Error.ShouldBeOfType<AeroError.Validation>();
        terminalCalls.ShouldBe(0);
    }

    private static AeroAiPipelineContext Context(
        bool authenticated = true,
        int inputCharacters = 10)
    {
        var identity = authenticated
            ? new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "7")],
                authenticationType: "test")
            : new ClaimsIdentity();
        return new AeroAiPipelineContext(
            AeroAiAudience.Manager,
            AeroAiOperation.Assistant,
            new ClaimsPrincipal(identity),
            7,
            3,
            11,
            "en-US",
            "trace-1",
            1,
            inputCharacters,
            IsStreaming: true);
    }

    private sealed class RecordingStage(
        string name,
        int order,
        List<string> calls) : IAeroAiPipelineStage
    {
        public string Name => name;
        public int Order => order;
        public bool AppliesTo(AeroAiPipelineContext context) => true;

        public async Task<Result<T>> InvokeAsync<T>(
            AeroAiPipelineContext context,
            AeroAiPipelineNext<T> next,
            CancellationToken cancellationToken)
        {
            calls.Add($"{name}:enter");
            var result = await next(context, cancellationToken);
            calls.Add($"{name}:exit");
            return result;
        }
    }

    private sealed class FailingStage(int order, List<string> calls)
        : IAeroAiPipelineStage
    {
        public string Name => "failure";
        public int Order => order;
        public bool AppliesTo(AeroAiPipelineContext context) => true;

        public Task<Result<T>> InvokeAsync<T>(
            AeroAiPipelineContext context,
            AeroAiPipelineNext<T> next,
            CancellationToken cancellationToken)
        {
            calls.Add(Name);
            return Task.FromResult<Result<T>>(
                AeroError.ForbiddenError("blocked"));
        }
    }
}
