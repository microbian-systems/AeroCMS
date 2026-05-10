using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aero.Cms.SourceGenerators.Analyzers;

/// <summary>
/// AERO002: Wolverine handler missing [WolverineHandler] attribute.
///
/// Reports a diagnostic when a class directly declares <c>IWolverineHandler</c>
/// in its base list but does not have the <c>[WolverineHandler]</c> attribute.
///
/// This analyzer uses a narrow, efficient check (base list only) rather than
/// scanning <c>AllInterfaces</c> on every class in the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Aero002WolverineHandlerRequired : DiagnosticAnalyzer
{
    private const string IWolverineHandlerName = "Wolverine.IWolverineHandler";
    private const string WolverineHandlerAttributeName = "Wolverine.Attributes.WolverineHandlerAttribute";

    private static readonly DiagnosticDescriptor MissingWolverineHandlerAttribute = new(
        id: "AERO002",
        title: "Wolverine handler missing [WolverineHandler] attribute",
        messageFormat: "Handler class '{0}' implements IWolverineHandler but is missing the [WolverineHandler] attribute. " +
                       "Add [WolverineHandler] to enable source-generated discovery.",
        category: "AeroCMS.WolverineHandlers",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All Wolverine handlers intended for source-generated discovery must be decorated " +
                     "with [WolverineHandler]. Source generation uses ForAttributeWithMetadataName for " +
                     "performance — it does not scan for IWolverineHandler through AllInterfaces.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MissingWolverineHandlerAttribute];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        // Quick bail-out: no base type list at all
        if (classDecl.BaseList is null)
            return;

        // Check base types for IWolverineHandler (direct declaration only)
        var hasIWolverineHandler = false;
        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(baseType.Type, context.CancellationToken);
            if (typeInfo.Type is not INamedTypeSymbol typeSymbol)
                continue;

            var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Check if this base type itself is IWolverineHandler
            if (fullName == "global::" + IWolverineHandlerName)
            {
                hasIWolverineHandler = true;
                break;
            }

            // Also check if this base type's interfaces include IWolverineHandler
            // (e.g., if there's adapter interface like ICustomHandler : IWolverineHandler)
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                if (iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::" + IWolverineHandlerName)
                {
                    hasIWolverineHandler = true;
                    break;
                }
            }

            if (hasIWolverineHandler)
                break;
        }

        if (!hasIWolverineHandler)
            return;

        // Found class that directly or indirectly declares IWolverineHandler
        // Now check if it has [WolverineHandler]
        var hasAttribute = false;
        var declaredSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken);
        if (declaredSymbol is not null)
        {
            foreach (var attr in declaredSymbol.GetAttributes())
            {
                var attrFullName = attr.AttributeClass?.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat);
                if (attrFullName == "global::" + WolverineHandlerAttributeName)
                {
                    hasAttribute = true;
                    break;
                }
            }
        }

        if (hasAttribute)
            return;

        // Report diagnostic
        var className = classDecl.Identifier.Text;
        context.ReportDiagnostic(Diagnostic.Create(
            MissingWolverineHandlerAttribute,
            classDecl.Identifier.GetLocation(),
            className));
    }
}
