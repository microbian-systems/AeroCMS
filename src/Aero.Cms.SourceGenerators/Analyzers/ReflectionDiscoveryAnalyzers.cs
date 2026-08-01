using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aero.Cms.SourceGenerators.Analyzers;

/// <summary>
/// AERO010-AERO013: Analyzer guardrails that prevent reintroduction of
/// runtime reflection-based discovery in production code paths.
///
/// Annotate explicitly allowed reflection paths with
/// <c>[LegacyReflectionDiscovery]</c> (from <c>Aero.Cms.Generated</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReflectionDiscoveryAnalyzers : DiagnosticAnalyzer
{
    private const string LegacyReflectionDiscoveryAttr = "Aero.Cms.Generated.LegacyReflectionDiscoveryAttribute";

    private static readonly DiagnosticDescriptor Aero010 = new(
        id: "AERO010",
        title: "Do not call AppDomain.CurrentDomain.GetAssemblies() in production code",
        messageFormat: "'{0}' performs broad assembly scanning. Annotate with [LegacyReflectionDiscovery] or use a typed dependency.",
        category: "AeroCMS.ReflectionDiscovery",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AppDomain.CurrentDomain.GetAssemblies() scans every loaded assembly at runtime. " +
                     "Use an explicit typed reference or generated registry instead.");

    private static readonly DiagnosticDescriptor Aero011 = new(
        id: "AERO011",
        title: "Do not call Assembly.GetTypes() for discovery in production code",
        messageFormat: "'{0}' performs assembly type scanning. Annotate with [LegacyReflectionDiscovery] or use a generated type registry.",
        category: "AeroCMS.ReflectionDiscovery",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Assembly.GetTypes() scans every type in an assembly at runtime. " +
                     "Use a generated catalog or explicit type reference instead.");

    private static readonly DiagnosticDescriptor Aero012 = new(
        id: "AERO012",
        title: "Do not call Type.GetMethods() for extension-point discovery",
        messageFormat: "'{0}' performs method scanning for discovery. Annotate with [LegacyReflectionDiscovery] or use a generated plug/extension catalog.",
        category: "AeroCMS.ReflectionDiscovery",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Type.GetMethods() with BindingFlags for discovery should be replaced " +
                     "with a source-generated or startup-cached extension-point catalog.");

    // Note: AERO013 is not implemented as a standalone analyzer rule because detecting
    // "broad interface scanning" in source generators requires deep semantic analysis
    // of generator patterns that is better handled by code review.
    // See the existing AERO001-AERO006 block-renderer diagnostics for the recommended
    // pattern (marker attributes + ForAttributeWithMetadataName).

    /// <summary>
    /// Gets the AERO010, AERO011, and AERO012 descriptors produced by this analyzer.
    /// </summary>
public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Aero010, Aero011, Aero012];

    /// <summary>
    /// Enables concurrent invocation analysis while excluding generated code.
    /// </summary>
    /// <param name="context">The analyzer registration context.</param>
public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>
    /// Classifies reflection invocations and reports the corresponding discovery diagnostic.
    /// </summary>
    /// <param name="context">The invocation-expression analysis context.</param>
    /// <remarks>
    /// Calls inside an incremental generator or inside a syntax ancestor annotated with the embedded
    /// legacy marker are exempt. <c>GetMethods</c> is reported only when an argument's source text
    /// contains <c>BindingFlags</c>.
    /// </remarks>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol == null)
            return;

        var methodName = methodSymbol.Name;
        var containingType = methodSymbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Determine which rule applies
        DiagnosticDescriptor? rule = null;
        string? targetName = null;

        if (methodName == "GetAssemblies" &&
            containingType == "global::System.AppDomain")
        {
            rule = Aero010;
            targetName = "AppDomain.CurrentDomain.GetAssemblies()";
        }
        else if (methodName == "GetTypes" &&
                 containingType == "global::System.Reflection.Assembly")
        {
            rule = Aero011;
            targetName = "Assembly.GetTypes()";
        }
        else if (methodName == "GetMethods" &&
                 containingType is "global::System.Type" or "global::System.Reflection.TypeInfo")
        {
            // Only flag GetMethods with BindingFlags — indicates discovery intent
            if (HasBindingFlagsArgument(invocation))
            {
                rule = Aero012;
                targetName = "Type.GetMethods(BindingFlags)";
            }
        }

        if (rule == null)
            return;

        // Check if the containing member or type has [LegacyReflectionDiscovery]
        if (IsAllowedByAnnotation(context, invocation))
            return;

        // Check if we're inside a source generator class (allowed)
        if (IsInsideSourceGenerator(context, invocation))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            rule,
            invocation.GetLocation(),
            targetName));
    }

    /// <summary>
    /// Detects a binding-flags argument using source-text matching.
    /// </summary>
    /// <param name="invocation">The <c>GetMethods</c> invocation to inspect.</param>
    /// <returns><see langword="true"/> when any argument text contains <c>BindingFlags</c>.</returns>
    private static bool HasBindingFlagsArgument(InvocationExpressionSyntax invocation)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.ToString().IndexOf("BindingFlags", StringComparison.Ordinal) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Searches containing declarations for the generated legacy-reflection exemption attribute.
    /// </summary>
    /// <returns><see langword="true"/> when an inspected method, constructor, or type is annotated.</returns>
    /// <remarks>
    /// Although the embedded attribute can target assemblies, this syntax walk does not inspect the
    /// assembly symbol, so assembly-level exemptions are not honored by the current implementation.
    /// </remarks>
    private static bool IsAllowedByAnnotation(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        // Walk up the syntax tree to find containing method, class, or assembly
        var current = node.Parent;
        while (current != null)
        {
            if (current is MethodDeclarationSyntax or ConstructorDeclarationSyntax or
                ClassDeclarationSyntax or StructDeclarationSyntax or
                RecordDeclarationSyntax or InterfaceDeclarationSyntax)
            {
                ISymbol? declaredSymbol = null;
                if (current is MethodDeclarationSyntax m)
                    declaredSymbol = context.SemanticModel.GetDeclaredSymbol(m, context.CancellationToken);
                else if (current is ConstructorDeclarationSyntax ctor)
                    declaredSymbol = context.SemanticModel.GetDeclaredSymbol(ctor, context.CancellationToken);
                else if (current is ClassDeclarationSyntax cls)
                    declaredSymbol = context.SemanticModel.GetDeclaredSymbol(cls, context.CancellationToken);
                else if (current is StructDeclarationSyntax str)
                    declaredSymbol = context.SemanticModel.GetDeclaredSymbol(str, context.CancellationToken);
                else if (current is RecordDeclarationSyntax rec)
                    declaredSymbol = context.SemanticModel.GetDeclaredSymbol(rec, context.CancellationToken);
                else if (current is InterfaceDeclarationSyntax iface)
                    declaredSymbol = context.SemanticModel.GetDeclaredSymbol(iface, context.CancellationToken);

                if (declaredSymbol != null && HasLegacyReflectionDiscoveryAttribute(declaredSymbol))
                    return true;
            }

            current = current.Parent;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a symbol has the fully qualified legacy-reflection marker.
    /// </summary>
    /// <returns><see langword="true"/> when the marker attribute is present.</returns>
    private static bool HasLegacyReflectionDiscoveryAttribute(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrName = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (attrName == "global::" + LegacyReflectionDiscoveryAttr)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether an invocation is nested in a class implementing <see cref="IIncrementalGenerator"/>.
    /// </summary>
    /// <returns><see langword="true"/> when any containing class implements the generator interface.</returns>
    private static bool IsInsideSourceGenerator(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        // Walk up to find containing class and check if it implements IIncrementalGenerator
        var current = node.Parent;
        while (current != null)
        {
            if (current is ClassDeclarationSyntax classDecl)
            {
                var declaredSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken);
                if (declaredSymbol is INamedTypeSymbol typeSymbol)
                {
                    foreach (var iface in typeSymbol.AllInterfaces)
                    {
                        var ifaceName = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        if (ifaceName == "global::Microsoft.CodeAnalysis.IIncrementalGenerator")
                            return true;
                    }
                }
            }

            current = current.Parent;
        }

        return false;
    }
}
