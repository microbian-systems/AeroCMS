using System.Reflection;
using System.Runtime.ExceptionServices;
using RazorEngineCore;
using SharpTS.Compilation;
using SharpTS.Diagnostics;
using SharpTS.Execution;
using SharpTS.Modules;
using SharpTS.Parsing;
using SharpTS.TypeSystem;
using Shouldly;

namespace Aero.Cms.Rendering.Interop.Tests;

public sealed class SharpTsRazorEngineCoreInteropTests
{
    private const string TypeScriptSource = """
        import { RazorTemplateTestBridge }
            from "dotnet:Aero.Cms.Rendering.Interop.Tests.RazorTemplateTestBridge";

        RazorTemplateTestBridge.render("greeting", "AeroCMS");
        """;

    [Test]
    public void SharpTs_can_invoke_a_RazorEngineCore_wrapper()
    {
        foreach (var mode in new[] { SharpTsExecutionMode.Interpreted, SharpTsExecutionMode.Compiled })
        {
            RazorTemplateTestBridge.Reset();

            SharpTsTestHarness.Run(
                TypeScriptSource,
                mode,
                SharpTsDotNetImportPolicy.RenderingSafeV1);

            RazorTemplateTestBridge.LastRenderedHtml.ShouldBe(
                "<article><h1>Hello AeroCMS</h1></article>",
                $"SharpTS {mode} mode should invoke the RazorEngineCore-backed bridge.");
        }
    }

    [Test]
    public void Rendering_policy_rejects_unapproved_dotnet_imports()
    {
        const string source = """
            import { File } from "dotnet:System.IO";
            File.readAllText("secrets.txt");
            """;

        var exception = Should.Throw<SharpTsImportPolicyException>(() =>
            SharpTsTestHarness.Run(
                source,
                SharpTsExecutionMode.Interpreted,
                SharpTsDotNetImportPolicy.RenderingSafeV1));

        exception.Message.ShouldContain("System.IO.File");
    }

    [Test]
    public void Rendering_policy_rejects_DotNetType_bypass()
    {
        const string source = """
            @DotNetType("System.IO.File")
            declare class File {
                static readAllText(path: string): string;
            }
            """;

        var exception = Should.Throw<SharpTsImportPolicyException>(() =>
            SharpTsTestHarness.Run(
                source,
                SharpTsExecutionMode.Interpreted,
                SharpTsDotNetImportPolicy.RenderingSafeV1));

        exception.Message.ShouldContain("@DotNetType");
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

    public static void Run(
        string source,
        SharpTsExecutionMode mode,
        SharpTsDotNetImportPolicy importPolicy)
    {
        var virtualBase = Path.Combine(
            Path.GetTempPath(),
            $"aero_sharpts_interop_{Guid.NewGuid():N}");
        var entryPath = Path.GetFullPath(Path.Combine(virtualBase, "main.ts"));
        var virtualFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [entryPath] = source
        };

        var resolver = new ModuleResolver(entryPath, virtualFiles);
        var entryModule = resolver.LoadModule(entryPath, InteropDecoratorMode);
        var modules = resolver.GetModulesInOrder(entryModule);
        importPolicy.Validate(modules);

        var typeChecker = new TypeChecker();
        typeChecker.SetDecoratorMode(InteropDecoratorMode);
        var typeMap = typeChecker.CheckModules(modules, resolver);
        var errors = typeChecker.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Message)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                $"SharpTS type checking failed: {string.Join(Environment.NewLine, errors)}");
        }

        switch (mode)
        {
            case SharpTsExecutionMode.Interpreted:
                RunInterpreted(modules, resolver, typeMap);
                break;
            case SharpTsExecutionMode.Compiled:
                RunCompiled(modules, resolver, typeMap);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    private static void RunInterpreted(
        List<ParsedModule> modules,
        ModuleResolver resolver,
        TypeMap typeMap)
    {
        using var interpreter = new Interpreter(TextWriter.Null, TextWriter.Null);
        interpreter.SetDecoratorMode(InteropDecoratorMode);
        interpreter.InterpretModules(modules, resolver, typeMap);
    }

    private static void RunCompiled(
        List<ParsedModule> modules,
        ModuleResolver resolver,
        TypeMap typeMap)
    {
        var statementList = modules.SelectMany(module => module.Statements).ToList();
        var deadCodeInfo = new DeadCodeAnalyzer(typeMap).Analyze(statementList);
        var compiler = new ILCompiler($"aero_interop_{Guid.NewGuid():N}");
        compiler.SetDecoratorMode(InteropDecoratorMode);
        compiler.CompileModules(modules, resolver, typeMap, deadCodeInfo);

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

internal sealed class SharpTsDotNetImportPolicy
{
    private readonly IReadOnlySet<string> _allowedTypes;

    public static SharpTsDotNetImportPolicy RenderingSafeV1 { get; } = new(
        ["Aero.Cms.Rendering.Interop.Tests.RazorTemplateTestBridge"]);

    public SharpTsDotNetImportPolicy(IEnumerable<string> allowedTypes)
    {
        ArgumentNullException.ThrowIfNull(allowedTypes);
        _allowedTypes = allowedTypes.ToHashSet(StringComparer.Ordinal);
    }

    public void Validate(IEnumerable<ParsedModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        foreach (var module in modules)
        {
            foreach (var statement in module.Statements)
            {
                ValidateDotNetTypeDecorator(statement);
                switch (statement)
                {
                    case Stmt.Import import when IsDotNetModule(import.ModulePath):
                        ValidateImport(import);
                        break;
                    case Stmt.ImportRequire importRequire
                        when IsDotNetModule(importRequire.ModulePath):
                        throw new SharpTsImportPolicyException(
                            $"CommonJS import of '{importRequire.ModulePath}' is not allowed.");
                }
            }
        }
    }

    private void ValidateImport(Stmt.Import import)
    {
        if (import.DefaultImport is not null
            || import.NamespaceImport is not null
            || import.NamedImports is not { Count: > 0 })
        {
            throw new SharpTsImportPolicyException(
                $"Only named imports are allowed for '{import.ModulePath}'.");
        }

        var moduleTarget = import.ModulePath["dotnet:".Length..];
        foreach (var specifier in import.NamedImports)
        {
            var importedName = specifier.Imported.Lexeme;
            var candidates = new[]
            {
                moduleTarget,
                $"{moduleTarget}.{importedName}"
            };
            var resolvedType = candidates.FirstOrDefault(_allowedTypes.Contains);
            if (resolvedType is null)
            {
                throw new SharpTsImportPolicyException(
                    $"The .NET type '{candidates[1]}' is not allowed by rendering.safe-v1.");
            }
        }
    }

    private static void ValidateDotNetTypeDecorator(Stmt statement)
    {
        if (statement is not Stmt.Class { Decorators: { Count: > 0 } decorators })
        {
            return;
        }

        if (decorators.Any(decorator =>
                string.Equals(
                    GetDecoratorName(decorator.Expression),
                    "DotNetType",
                    StringComparison.Ordinal)))
        {
            throw new SharpTsImportPolicyException(
                "@DotNetType declarations are not allowed by rendering.safe-v1; use an approved dotnet: import.");
        }
    }

    private static string? GetDecoratorName(Expr expression) => expression switch
    {
        Expr.Variable variable => variable.Name.Lexeme,
        Expr.Call call => GetDecoratorName(call.Callee),
        Expr.Get get => get.Name.Lexeme,
        _ => null
    };

    private static bool IsDotNetModule(string modulePath)
        => modulePath.StartsWith("dotnet:", StringComparison.Ordinal);
}

internal sealed class SharpTsImportPolicyException(string message) : Exception(message);
