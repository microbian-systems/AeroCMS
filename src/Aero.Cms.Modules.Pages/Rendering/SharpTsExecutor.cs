using System.Globalization;
using System.Text;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Core;
using Aero.Core.Railway;
using SharpTS.Diagnostics;
using SharpTS.Execution;
using SharpTS.Modules;
using SharpTS.Parsing;
using SharpTS.TypeSystem;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>Detached, JSON-shaped page values made available to one SharpTS render.</summary>
public sealed record SharpTsPageContext(
    string? Id,
    string Title,
    string Slug,
    string Path,
    string Culture);

/// <summary>Detached, JSON-shaped site values made available to one SharpTS render.</summary>
public sealed record SharpTsSiteContext(
    string Id,
    string CurrentCulture);

/// <summary>Detached, JSON-shaped values made available to one SharpTS render.</summary>
public sealed record SharpTsRenderContext(
    SharpTsPageContext Page,
    SharpTsSiteContext Site,
    IReadOnlyDictionary<string, ContentQueryResult> Content,
    bool IsPreview)
{
    public static SharpTsRenderContext Create(
        PageRenderMetadata metadata,
        PageContentQueryResolution contentQueries,
        bool isPreview) => new(
        new SharpTsPageContext(
            metadata.Id?.ToString(CultureInfo.InvariantCulture),
            metadata.Title,
            metadata.Slug,
            metadata.Path,
            metadata.Culture),
        new SharpTsSiteContext(
            metadata.SiteId.ToString(CultureInfo.InvariantCulture),
            metadata.Culture),
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
    private const string ContentModuleSpecifier = "aero:content";
    private const string ContentModuleFileName = "aero-content.ts";
    private static readonly IReadOnlySet<string> AllowedDotNetImportTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Collections.ArrayList",
            "System.Collections.BitArray",
            "System.Collections.Hashtable",
            "System.Collections.Queue",
            "System.Collections.SortedList",
            "System.Collections.Stack",
            "System.Linq.Enumerable",
            "System.Linq.Queryable",
            "System.Linq.Expressions.BinaryExpression",
            "System.Linq.Expressions.ConstantExpression",
            "System.Linq.Expressions.Expression",
            "System.Linq.Expressions.LambdaExpression",
            "System.Linq.Expressions.MemberExpression",
            "System.Linq.Expressions.MethodCallExpression",
            "System.Linq.Expressions.NewExpression",
            "System.Linq.Expressions.ParameterExpression",
            "System.Threading.Tasks.Task",
            "System.Threading.Tasks.ValueTask"
        };
    private static readonly IReadOnlySet<string> AllowedDotNetGenericTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Collections.Generic.List`1[System.String]",
            "System.Collections.Generic.List`1[System.Int64]",
            "System.Collections.Generic.List`1[System.Double]",
            "System.Collections.Generic.List`1[System.Boolean]",
            "System.Collections.Generic.Dictionary`2[System.String,System.String]",
            "System.Collections.Generic.Dictionary`2[System.String,System.Int64]",
            "System.Collections.Generic.HashSet`1[System.String]",
            "System.Collections.Generic.Queue`1[System.String]",
            "System.Collections.Generic.Stack`1[System.String]"
        };
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
        var virtualBase = Path.Combine(
            Path.GetTempPath(),
            $"aero_sharpts_render_{Guid.NewGuid():N}");
        var entryPath = Path.GetFullPath(Path.Combine(virtualBase, "main.ts"));
        var contentModulePath = Path.GetFullPath(Path.Combine(virtualBase, ContentModuleFileName));
        ValidateSourceCapabilityProfile(source);
        var rewrittenSource = RewriteContentModuleImport(source);
        var executableSource = BuildExecutableSource(rewrittenSource, contextJson);
        var virtualFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [entryPath] = executableSource,
            [contentModulePath] = SharpTsContentVirtualModule.Build(context.Content)
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
        interface AeroContentNode {
            id: string;
            contentType: string;
            title: string;
            slug: string;
            fields: { [name: string]: any };
            children: AeroContentNode[];
        }

        interface AeroContentQueryResult {
            name: string;
            contentTypeAlias: string;
            roots: AeroContentNode[];
            totalItems: number;
            wasTruncated: boolean;
        }

        interface AeroPageContext {
            id: string | null;
            title: string;
            slug: string;
            path: string;
            culture: string;
        }

        interface AeroSiteContext {
            id: string;
            currentCulture: string;
        }

        interface AeroRenderContext {
            page: AeroPageContext;
            site: AeroSiteContext;
            content: { [name: string]: AeroContentQueryResult };
            isPreview: boolean;
        }

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

    private static string RewriteContentModuleImport(string source)
    {
        var tokens = new Lexer(source).ScanTokens();
        var moduleTokens = tokens
            .Select((token, index) => (Token: token, Index: index))
            .Where(candidate =>
                candidate.Index > 0
                && candidate.Token.Type == TokenType.STRING
                && string.Equals(
                    candidate.Token.Literal as string,
                    ContentModuleSpecifier,
                    StringComparison.Ordinal)
                && tokens[candidate.Index - 1].Type == TokenType.FROM)
            .Select(candidate => candidate.Token)
            .OrderByDescending(token => token.Start)
            .ToArray();

        var rewritten = new StringBuilder(source);
        foreach (var token in moduleTokens)
        {
            var quote = token.Lexeme[0];
            rewritten.Remove(token.Start, token.Lexeme.Length);
            rewritten.Insert(
                token.Start,
                $"{quote}./aero-content{quote}");
        }

        return rewritten.ToString();
    }

    private static void ValidateSourceCapabilityProfile(string source)
    {
        var tokens = new Lexer(source).ScanTokens();
        ValidateImportLikeTokens(tokens);
        ValidateDotNetTypeTokens(tokens);

        var statements = new Parser(tokens, InteropDecoratorMode)
            .WithFilePath("main.ts")
            .ParseOrThrow();
        foreach (var statement in statements)
        {
            ValidateSourceStatement(statement);
        }
    }

    private static void ValidateImportLikeTokens(IReadOnlyList<Token> tokens)
    {
        for (var index = 0; index + 1 < tokens.Count; index++)
        {
            var token = tokens[index];
            var next = tokens[index + 1];
            if (token.Type == TokenType.IMPORT
                && next.Type == TokenType.LEFT_PAREN)
            {
                throw new InvalidOperationException(
                    "Dynamic imports are not available in rendering.safe-v1.");
            }

            if (token.Type == TokenType.IDENTIFIER
                && string.Equals(token.Lexeme, "require", StringComparison.Ordinal)
                && next.Type == TokenType.LEFT_PAREN)
            {
                throw new InvalidOperationException(
                    "CommonJS imports are not available in rendering.safe-v1.");
            }
        }
    }

    private static void ValidateDotNetTypeTokens(IReadOnlyList<Token> tokens)
    {
        for (var index = 0; index + 4 < tokens.Count; index++)
        {
            if (!string.Equals(
                    tokens[index].Lexeme,
                    "DotNetType",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (index == 0 || tokens[index - 1].Type != TokenType.AT)
            {
                throw new InvalidOperationException(
                    "@DotNetType aliases are not available in rendering.safe-v1.");
            }

            var hasExpectedShape =
                tokens[index + 1].Type == TokenType.LEFT_PAREN
                && tokens[index + 2].Type == TokenType.STRING
                && tokens[index + 3].Type == TokenType.RIGHT_PAREN;
            var typeName = hasExpectedShape
                ? tokens[index + 2].Literal as string
                : null;
            if (typeName is null
                || !AllowedDotNetGenericTypes.Contains(typeName))
            {
                throw new InvalidOperationException(
                    $"@DotNetType is limited to approved closed generic families in rendering.safe-v1 ('{typeName ?? "invalid declaration"}').");
            }
        }
    }

    private static void ValidateSourceStatement(Stmt statement)
    {
        switch (statement)
        {
            case Stmt.Import
                {
                    NamedImports: { Count: > 0 },
                    DefaultImport: null,
                    NamespaceImport: null
                } import
                when string.Equals(
                    import.ModulePath,
                    ContentModuleSpecifier,
                    StringComparison.Ordinal):
                return;
            case Stmt.Import import
                when string.Equals(
                    import.ModulePath,
                    ContentModuleSpecifier,
                    StringComparison.Ordinal):
                throw new InvalidOperationException(
                    $"Only named imports are allowed for '{ContentModuleSpecifier}'.");
            case Stmt.Import import when IsDotNetModule(import.ModulePath):
                ValidateDotNetImport(import);
                return;
            case Stmt.Import import:
                throw new InvalidOperationException(
                    $"Module '{import.ModulePath}' is not available in rendering.safe-v1.");
            case Stmt.ImportRequire importRequire:
                throw new InvalidOperationException(
                    $"CommonJS imports are not available in rendering.safe-v1 ('{importRequire.ModulePath}').");
            case Stmt.Export { FromModulePath: not null } export:
                throw new InvalidOperationException(
                    $"Module re-exports are not available in rendering.safe-v1 ('{export.FromModulePath}').");
        }
    }

    private static void ValidateCapabilityProfile(IEnumerable<ParsedModule> modules)
    {
        foreach (var module in modules)
        {
            foreach (var statement in module.Statements)
            {
                if (statement is Stmt.Import import)
                {
                    if (IsGeneratedContentModuleImport(import))
                    {
                        continue;
                    }

                    if (IsDotNetModule(import.ModulePath))
                    {
                        ValidateDotNetImport(import);
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Only the '{ContentModuleSpecifier}' import and explicitly allowed .NET types are available in the SharpTS rendering.safe-v1 profile ('{import.ModulePath}').");
                }

                if (statement is Stmt.ImportRequire importRequire)
                {
                    throw new InvalidOperationException(
                        $"CommonJS imports are not available in rendering.safe-v1 ('{importRequire.ModulePath}').");
                }

            }
        }
    }

    private static bool IsGeneratedContentModuleImport(Stmt.Import import)
        => string.Equals(import.ModulePath, "./aero-content", StringComparison.Ordinal);

    private static bool IsDotNetModule(string modulePath)
        => modulePath.StartsWith("dotnet:", StringComparison.Ordinal);

    private static bool IsAllowedDotNetModule(string modulePath)
    {
        if (!IsDotNetModule(modulePath))
        {
            return false;
        }

        var moduleTarget = modulePath["dotnet:".Length..];
        return AllowedDotNetImportTypes.Contains(moduleTarget)
               || AllowedDotNetImportTypes.Any(type =>
                   type.StartsWith($"{moduleTarget}.", StringComparison.Ordinal));
    }

    private static void ValidateDotNetImport(Stmt.Import import)
    {
        if (import.DefaultImport is not null
            || import.NamespaceImport is not null
            || import.NamedImports is not { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"Only named imports are allowed for '{import.ModulePath}'.");
        }

        var moduleTarget = import.ModulePath["dotnet:".Length..];
        foreach (var specifier in import.NamedImports)
        {
            var importedName = specifier.Imported.Lexeme;
            var directName = moduleTarget[
                (moduleTarget.LastIndexOf('.') + 1)..];
            var genericArityIndex = directName.IndexOf('`');
            if (genericArityIndex >= 0)
            {
                directName = directName[..genericArityIndex];
            }

            var directMatch = AllowedDotNetImportTypes.Contains(moduleTarget)
                              && string.Equals(
                                  importedName,
                                  directName,
                                  StringComparison.Ordinal);
            var namespaceMatch = AllowedDotNetImportTypes.Contains(
                $"{moduleTarget}.{importedName}");
            if (!directMatch && !namespaceMatch)
            {
                throw new InvalidOperationException(
                    $"The .NET type '{moduleTarget}.{importedName}' is not allowed by rendering.safe-v1.");
            }
        }
    }

}
