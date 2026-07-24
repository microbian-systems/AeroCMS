using System.Reflection;
using System.Runtime.ExceptionServices;
using RazorEngineCore;
using SharpTS.Compilation;
using SharpTS.Execution;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Shouldly;

namespace Aero.Cms.Rendering.Interop.Tests;

public sealed class SharpTsRazorEngineCoreInteropTests
{
    private const string TypeScriptSource = """
        @DotNetType("Aero.Cms.Rendering.Interop.Tests.RazorTemplateTestBridge")
        declare class RazorTemplateTestBridge {
            static render(templateKey: string, name: string): void;
        }

        RazorTemplateTestBridge.render("greeting", "AeroCMS");
        """;

    [Test]
    public void SharpTs_can_invoke_a_RazorEngineCore_wrapper()
    {
        foreach (var mode in new[] { SharpTsExecutionMode.Interpreted, SharpTsExecutionMode.Compiled })
        {
            RazorTemplateTestBridge.Reset();

            SharpTsTestHarness.Run(TypeScriptSource, mode);

            RazorTemplateTestBridge.LastRenderedHtml.ShouldBe(
                "<article><h1>Hello AeroCMS</h1></article>",
                $"SharpTS {mode} mode should invoke the RazorEngineCore-backed bridge.");
        }
    }
}

public static class RazorTemplateTestBridge
{
    public static string? LastRenderedHtml { get; private set; }

    public static void Render(string templateKey, string name)
    {
        var templateSource = templateKey switch
        {
            "greeting" => "<article><h1>Hello @Model.Name</h1></article>",
            _ => throw new ArgumentOutOfRangeException(nameof(templateKey), templateKey, "Unknown template.")
        };

        IRazorEngine razorEngine = new RazorEngine();
        var template = razorEngine.Compile(templateSource);

        LastRenderedHtml = template.Run(new { Name = name });
    }

    public static void Reset()
    {
        LastRenderedHtml = null;
    }
}

internal enum SharpTsExecutionMode
{
    Interpreted,
    Compiled
}

internal static class SharpTsTestHarness
{
    private const DecoratorMode InteropDecoratorMode = DecoratorMode.Legacy;

    public static void Run(string source, SharpTsExecutionMode mode)
    {
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.ScanTokens(), InteropDecoratorMode);
        var statements = parser.ParseOrThrow();

        var typeChecker = new TypeChecker();
        typeChecker.SetDecoratorMode(InteropDecoratorMode);
        var typeMap = typeChecker.Check(statements);

        switch (mode)
        {
            case SharpTsExecutionMode.Interpreted:
                RunInterpreted(statements, typeMap);
                break;
            case SharpTsExecutionMode.Compiled:
                RunCompiled(statements, typeMap);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    private static void RunInterpreted(
        IReadOnlyList<Stmt> statements,
        TypeMap typeMap)
    {
        using var interpreter = new Interpreter(TextWriter.Null, TextWriter.Null);
        interpreter.SetDecoratorMode(InteropDecoratorMode);
        interpreter.Interpret(statements.ToList(), typeMap);
    }

    private static void RunCompiled(
        IReadOnlyList<Stmt> statements,
        TypeMap typeMap)
    {
        var statementList = statements.ToList();
        var deadCodeInfo = new DeadCodeAnalyzer(typeMap).Analyze(statementList);
        var compiler = new ILCompiler($"aero_interop_{Guid.NewGuid():N}");
        compiler.SetDecoratorMode(InteropDecoratorMode);
        compiler.Compile(statementList, typeMap, deadCodeInfo);

        var assembly = System.Reflection.Assembly.Load(compiler.SaveToBytes());
        var programType = assembly.GetType("$Program")
            ?? throw new InvalidOperationException("Compiled SharpTS assembly has no $Program type.");
        var mainMethod = programType.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Compiled SharpTS assembly has no public static Main method.");

        try
        {
            mainMethod.Invoke(null, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }
}
