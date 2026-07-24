using System.Globalization;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Core;
using Aero.Core.Railway;
using SharpTS.Diagnostics;
using SharpTS.Execution;
using SharpTS.Modules;
using SharpTS.Parsing;
using SharpTS.TypeSystem;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Detached, JSON-shaped values made available to one SharpTS render.</summary>
public sealed record SharpTsRenderContext(
    object Page,
    object Site,
    IReadOnlyDictionary<string, Aero.Cms.Abstractions.Content.ContentQueryResult> Content,
    bool IsPreview)
{
    public static SharpTsRenderContext Create(
        PageRenderMetadata metadata,
        PageContentQueryResolution contentQueries,
        bool isPreview) => new(
        new
        {
            id = metadata.Id?.ToString(CultureInfo.InvariantCulture),
            title = metadata.Title,
            slug = metadata.Slug,
            path = metadata.Path,
            culture = metadata.Culture
        },
        new
        {
            id = metadata.SiteId.ToString(CultureInfo.InvariantCulture),
            currentCulture = metadata.Culture
        },
        contentQueries.Results,
        isPreview);
}

/// <summary>Executes trusted-author SharpTS in the current process in interpret-only mode.</summary>
public interface ISharpTsExecutor
{
    Task<Result<string>> ExecuteAsync(
        string source,
        SharpTsRenderContext context,
        int maximumOutputLength,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Alpha SharpTS host. Imports and .NET type decorators are rejected before the
/// module is interpreted; returned markup is still validated by Aero's HTML importer.
/// </summary>
public sealed class SharpTsExecutor : ISharpTsExecutor
{
    private const DecoratorMode InteropDecoratorMode = DecoratorMode.Legacy;
    private const string OutputStart = "__AERO_RENDER_START__";
    private const string OutputEnd = "__AERO_RENDER_END__";
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    public async Task<Result<string>> ExecuteAsync(
        string source,
        SharpTsRenderContext context,
        int maximumOutputLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        if (maximumOutputLength is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputLength));
        }

        await _executionGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Execute(source, context, maximumOutputLength);
        }
        catch (OperationCanceledException)
        {
            return AeroError.CancelledError("SharpTS rendering was cancelled.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return AeroError.ValidationError(
                [$"SharpTS rendering failed: {exception.Message}"]);
        }
        finally
        {
            _executionGate.Release();
        }
    }

    private static Result<string> Execute(
        string source,
        SharpTsRenderContext context,
        int maximumOutputLength)
    {
        var contextJson = JsonSerializer.Serialize(
            new
            {
                page = context.Page,
                site = context.Site,
                content = context.Content,
                isPreview = context.IsPreview
            },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                MaxDepth = 12
            });
        var executableSource = BuildExecutableSource(source, contextJson);
        var virtualBase = Path.Combine(
            Path.GetTempPath(),
            $"aero_sharpts_render_{Guid.NewGuid():N}");
        var entryPath = Path.GetFullPath(Path.Combine(virtualBase, "main.ts"));
        var virtualFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [entryPath] = executableSource
        };

        var resolver = new ModuleResolver(entryPath, virtualFiles);
        var entryModule = resolver.LoadModule(entryPath, InteropDecoratorMode);
        var modules = resolver.GetModulesInOrder(entryModule);
        ValidateCapabilityProfile(modules);

        var typeChecker = new TypeChecker();
        typeChecker.SetDecoratorMode(InteropDecoratorMode);
        var typeMap = typeChecker.CheckModules(modules, resolver);
        var errors = typeChecker.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Message)
            .Take(20)
            .ToArray();
        if (errors.Length > 0)
        {
            return AeroError.ValidationError(
                errors.Select(error => $"SharpTS: {error}").ToArray());
        }

        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var errorsOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var interpreter = new Interpreter(output, errorsOutput);
        interpreter.SetDecoratorMode(InteropDecoratorMode);
        interpreter.InterpretModules(modules, resolver, typeMap);

        var captured = output.ToString();
        var start = captured.LastIndexOf(OutputStart, StringComparison.Ordinal);
        var end = captured.LastIndexOf(OutputEnd, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            var detail = errorsOutput.ToString().Trim();
            return AeroError.ValidationError(
                [string.IsNullOrWhiteSpace(detail)
                    ? "SharpTS render(context) did not return an HTML fragment."
                    : $"SharpTS rendering failed: {detail}"]);
        }

        start += OutputStart.Length;
        var markup = captured[start..end];
        if (markup.Length > maximumOutputLength)
        {
            return AeroError.ValidationError(
                [$"SharpTS output cannot exceed {maximumOutputLength} characters."]);
        }

        return markup;
    }

    private static string BuildExecutableSource(string source, string contextJson) => $$"""
        class AeroHtmlFragment {
            value: string;
            constructor(value: string) { this.value = value; }
            toString(): string { return this.value; }
        }

        function aeroEscape(value: any): string {
            return String(value)
                .split("&").join("&amp;")
                .split("<").join("&lt;")
                .split(">").join("&gt;")
                .split("\"").join("&quot;")
                .split("'").join("&#39;");
        }

        function aeroHtmlValue(value: any): string {
            if (value instanceof AeroHtmlFragment) {
                return value.value;
            }
            if (Array.isArray(value)) {
                return value.map(aeroHtmlValue).join("");
            }
            if (value === null || value === undefined) {
                return "";
            }
            return aeroEscape(value);
        }

        function html(strings: string[], ...values: any[]): AeroHtmlFragment {
            let output = "";
            for (let index = 0; index < strings.length; index++) {
                output += strings[index];
                if (index < values.length) {
                    output += aeroHtmlValue(values[index]);
                }
            }
            return new AeroHtmlFragment(output);
        }

        {{source}}

        const __aeroContext = {{contextJson}};
        const __aeroRendered = render(__aeroContext);
        console.log("{{OutputStart}}" + String(__aeroRendered) + "{{OutputEnd}}");
        """;

    private static void ValidateCapabilityProfile(IEnumerable<ParsedModule> modules)
    {
        foreach (var module in modules)
        {
            foreach (var statement in module.Statements)
            {
                if (statement is Stmt.Import import)
                {
                    throw new InvalidOperationException(
                        $"Imports are not available in the SharpTS rendering.safe-v1 profile ('{import.ModulePath}').");
                }

                if (statement is Stmt.ImportRequire importRequire)
                {
                    throw new InvalidOperationException(
                        $"CommonJS imports are not available in rendering.safe-v1 ('{importRequire.ModulePath}').");
                }

                if (statement is Stmt.Class { Decorators: { Count: > 0 } decorators }
                    && decorators.Any(decorator =>
                        string.Equals(
                            GetDecoratorName(decorator.Expression),
                            "DotNetType",
                            StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        "@DotNetType declarations are not available in rendering.safe-v1.");
                }
            }
        }
    }

    private static string? GetDecoratorName(Expr expression) => expression switch
    {
        Expr.Variable variable => variable.Name.Lexeme,
        Expr.Call call => GetDecoratorName(call.Callee),
        Expr.Get get => get.Name.Lexeme,
        _ => null
    };
}
