using System.Collections.Concurrent;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Core.Security;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Aero.Cms.Core.Content.Templating;

/// <summary>
/// Validates, resource-limits, renders, and sanitizes CMS-authored Scriban templates.
/// </summary>
/// <remarks>
/// Parsed templates are cached by definition identity, version, and template text. Every
/// render creates a new <see cref="TemplateContext"/> and cloned import scopes. This describes
/// the implementation's isolation strategy and is not a blanket thread-safety guarantee for
/// caller-supplied objects.
/// </remarks>
public sealed class SecureScribanRenderer : ISecureScribanRenderer
{
    private readonly SecureScribanTemplateOptions options;
    private readonly ScribanTemplateValidator validator;
    private readonly IHtmlSanitizer htmlSanitizer;
    private readonly ConcurrentDictionary<TemplateCacheKey, Template> templateCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureScribanRenderer"/> class.
    /// </summary>
    /// <remarks>Uses default guardrails and a new <see cref="HtmlSanitizer"/>.</remarks>
    public SecureScribanRenderer()
        : this(new SecureScribanTemplateOptions(), new HtmlSanitizer())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureScribanRenderer"/> class.
    /// </summary>
    /// <param name="options">The validation and rendering limits.</param>
    /// <remarks>Uses a new <see cref="HtmlSanitizer"/>.</remarks>
    public SecureScribanRenderer(SecureScribanTemplateOptions options)
        : this(options, new HtmlSanitizer())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureScribanRenderer"/> class.
    /// </summary>
    /// <param name="options">The validation and rendering limits.</param>
    /// <param name="htmlSanitizer">The sanitizer applied to successful output.</param>
    public SecureScribanRenderer(SecureScribanTemplateOptions options, IHtmlSanitizer htmlSanitizer)
    {
        this.options = options;
        this.htmlSanitizer = htmlSanitizer;
        validator = new ScribanTemplateValidator(options);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Template and data validation occur before execution. Includes, dynamic evaluation,
    /// relaxed CLR access, and template loading are disabled. Both caller cancellation and
    /// expiration of <see cref="SecureScribanTemplateOptions.RenderTimeout"/> are converted
    /// to a timeout result rather than rethrown. Scriban runtime and invalid-operation
    /// failures during execution are converted to validation results.
    /// </remarks>
    public async Task<Result<string, AeroError>> RenderAsync(
        ScribanRenderDefinition definition,
        ScribanContentRenderModel model,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, ScriptObject>? imports = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(model);

        var validationResult = validator.Validate(definition.Template, definition.DataSchema);
        if (validationResult is Result<NoneType, AeroError>.Failure validationFailure)
        {
            return validationFailure.Error;
        }

        var dataValidationResult = validator.ValidateData(model.Fields, definition.DataSchema);
        if (dataValidationResult is Result<NoneType, AeroError>.Failure dataValidationFailure)
        {
            return dataValidationFailure.Error;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.RenderTimeout);

        try
        {
            var template = GetOrAddTemplate(definition);
            var context = CreateContext(model, imports, timeoutCts.Token);
            var output = await template.RenderAsync(context);
            if (output.Length > options.MaxOutputLength)
            {
                return AeroError.ValidationError(
                    [$"Dynamic template output exceeds the {options.MaxOutputLength} character limit."]);
            }

            return Prelude.Ok<string, AeroError>(htmlSanitizer.Sanitize(output));
        }
        catch (OperationCanceledException)
        {
            return AeroError.TimeoutError("Dynamic template rendering exceeded the configured timeout.");
        }
        catch (ScriptRuntimeException ex)
        {
            return AeroError.ValidationError([ex.Message]);
        }
        catch (InvalidOperationException ex)
        {
            return AeroError.ValidationError([ex.Message]);
        }
    }

    private Template GetOrAddTemplate(ScribanRenderDefinition definition)
    {
        var key = new TemplateCacheKey(definition.Identity, definition.Version, definition.Template);

        return templateCache.GetOrAdd(
            key,
            _ => Template.Parse(definition.Template));
    }

    private TemplateContext CreateContext(
        ScribanContentRenderModel model,
        IReadOnlyDictionary<string, ScriptObject>? imports,
        CancellationToken cancellationToken)
    {
        var context = new TemplateContext(CreateSafeBuiltinObject())
        {
            StrictVariables = options.StrictVariables,
            LoopLimit = options.LoopLimit,
            LoopLimitQueryable = options.LoopLimit,
            RecursiveLimit = options.RecursiveLimit,
            RegexTimeOut = options.RegexTimeout,
            LimitToString = options.MaxOutputLength,
            ObjectRecursionLimit = options.MaxInputDepth,
            CancellationToken = cancellationToken,
            EnableRelaxedMemberAccess = false,
            EnableRelaxedTargetAccess = false,
            EnableRelaxedFunctionAccess = false,
            EnableRelaxedIndexerAccess = false,
            MemberFilter = static _ => false,
            TemplateLoader = null
        };

        context.PushGlobal(JsonToScribanMapper.CreateGlobals(model, options.MaxInputDepth, imports));
        return context;
    }

    private static ScriptObject CreateSafeBuiltinObject()
    {
        var builtins = (ScriptObject)TemplateContext.GetDefaultBuiltinObject().Clone(deep: true);
        var objectFunctions = builtins.GetSafeValue<ScriptObject>("object")
            ?? throw new InvalidOperationException("Scriban's object built-ins are unavailable.");

        // Both functions parse and execute a new template string at runtime.
        // Removing them keeps every executable AST behind validation, template
        // size limits, and the renderer's resource boundaries.
        objectFunctions.Remove("eval");
        objectFunctions.Remove("eval_template");

        return builtins;
    }

    private readonly record struct TemplateCacheKey(long DefinitionId, int Version, string Template);
}
