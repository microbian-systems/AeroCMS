using System.Collections.Concurrent;
using System.Text.Json;
using Aero.Core;
using Aero.Core.Railway;
using Aero.Core.Security;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Syntax;

namespace Aero.Cms.Core.Blocks.Dynamic;

public sealed class SecureScribanRenderer : ISecureScribanRenderer
{
    private readonly SecureScribanTemplateOptions options;
    private readonly DynamicTemplateValidator validator;
    private readonly IHtmlSanitizer htmlSanitizer;
    private readonly ConcurrentDictionary<TemplateCacheKey, Template> templateCache = new();

    public SecureScribanRenderer()
        : this(new SecureScribanTemplateOptions(), new HtmlSanitizer())
    {
    }

    public SecureScribanRenderer(SecureScribanTemplateOptions options)
        : this(options, new HtmlSanitizer())
    {
    }

    public SecureScribanRenderer(SecureScribanTemplateOptions options, IHtmlSanitizer htmlSanitizer)
    {
        this.options = options;
        this.htmlSanitizer = htmlSanitizer;
        validator = new DynamicTemplateValidator(options);
    }

    public async Task<Result<string, AeroError>> RenderAsync(
        DynamicBlockDefinition definition,
        JsonDocument? data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var validationResult = validator.Validate(definition.ScribanTemplate, definition.DataSchema);
        if (validationResult is Result<NoneType, AeroError>.Failure validationFailure)
        {
            return validationFailure.Error;
        }

        var dataValidationResult = validator.ValidateData(data, definition.DataSchema);
        if (dataValidationResult is Result<NoneType, AeroError>.Failure dataValidationFailure)
        {
            return dataValidationFailure.Error;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.RenderTimeout);

        try
        {
            var template = GetOrAddTemplate(definition);
            var context = CreateContext(data, timeoutCts.Token);
            var output = await template.RenderAsync(context);
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

    private Template GetOrAddTemplate(DynamicBlockDefinition definition)
    {
        var key = new TemplateCacheKey(definition.Id, definition.Version, definition.ScribanTemplate);

        return templateCache.GetOrAdd(
            key,
            _ => Template.Parse(definition.ScribanTemplate));
    }

    private TemplateContext CreateContext(JsonDocument? data, CancellationToken cancellationToken)
    {
        var context = new TemplateContext
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
            TemplateLoader = null
        };

        context.PushGlobal(JsonToScribanMapper.CreateGlobals(data, options.MaxInputDepth));
        return context;
    }

    private readonly record struct TemplateCacheKey(long DefinitionId, int Version, string Template);
}
