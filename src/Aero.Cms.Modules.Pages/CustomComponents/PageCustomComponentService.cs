using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;
using Aero.Cms.Abstractions.Http.Clients;
using Aero.Cms.Core.Entities;
using Aero.Core;
using Aero.Core.Http;
using Aero.Core.Railway;
using Aero.Models;
using FluentValidation;

namespace Aero.Cms.Modules.Pages.CustomComponents;

public sealed class PageCustomComponentService(
    IDocumentSession session,
    ISiteContext siteContext,
    IValidator<SavePageCustomComponentRequest> validator)
    : IPageCustomComponentService
{
    public async Task<Result<IReadOnlyList<PageCustomComponent>, AeroError>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var components = await session.Query<PageCustomComponent>()
            .Where(component => component.SiteId == siteContext.SiteId)
            .OrderBy(component => component.Name)
            .ToListAsync(cancellationToken);
        return Prelude.Ok<IReadOnlyList<PageCustomComponent>, AeroError>(components);
    }

    public async Task<Result<PageCustomComponent, AeroError>> SaveAsync(
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return AeroError.ValidationError(
                validation.Errors.Select(error => error.ErrorMessage));
        }

        if (await NameExistsAsync(request.Name, null, cancellationToken))
        {
            return AeroError.ConflictError(
                $"A custom component named '{request.Name.Trim()}' already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var root = CustomComponentTemplate.Capture(request.Root);
        var component = new PageCustomComponent
        {
            Id = Snowflake.NewId(),
            SiteId = siteContext.SiteId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Category = request.Category.Trim(),
            Tags = (request.Tags ?? [])
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Root = root,
            ReferencedCatalogIds = CustomComponentTemplate.GetReferencedCatalogIds(root).ToList(),
            CreatedAt = now,
            UpdatedAt = now
        };

        session.Store(component);
        await session.SaveChangesAsync(cancellationToken);
        return component;
    }

    public async Task<Result<NeoPageNode, AeroError>> CreateInstanceAsync(
        long componentId,
        CancellationToken cancellationToken = default)
    {
        var component = await FindOwnedAsync(componentId, cancellationToken);
        if (component is null)
        {
            return AeroError.NotFoundError(
                $"Custom component '{componentId}' was not found.");
        }

        return CustomComponentTemplate.CreateInstance(component.Root);
    }

    public async Task<Result<PageCustomComponent, AeroError>> UpdateAsync(
        long componentId,
        SavePageCustomComponentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return AeroError.ValidationError(
                validation.Errors.Select(error => error.ErrorMessage));
        }

        var component = await FindOwnedAsync(componentId, cancellationToken);
        if (component is null)
        {
            return AeroError.NotFoundError(
                $"Custom component '{componentId}' was not found.");
        }

        if (await NameExistsAsync(request.Name, componentId, cancellationToken))
        {
            return AeroError.ConflictError(
                $"A custom component named '{request.Name.Trim()}' already exists.");
        }

        ApplyRequest(component, request);
        component.UpdatedAt = DateTimeOffset.UtcNow;
        session.Store(component);
        await session.SaveChangesAsync(cancellationToken);
        return component;
    }

    public async Task<Result<bool, AeroError>> DeleteAsync(
        long componentId,
        CancellationToken cancellationToken = default)
    {
        var component = await FindOwnedAsync(componentId, cancellationToken);
        if (component is null)
        {
            return AeroError.NotFoundError(
                $"Custom component '{componentId}' was not found.");
        }

        session.Delete(component);
        await session.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<PageCustomComponent?> FindOwnedAsync(
        long componentId,
        CancellationToken cancellationToken) =>
        session.Query<PageCustomComponent>()
            .FirstOrDefaultAsync(
                component =>
                    component.Id == componentId &&
                    component.SiteId == siteContext.SiteId,
                cancellationToken);

    private Task<bool> NameExistsAsync(
        string name,
        long? excludedComponentId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        var query = session.Query<PageCustomComponent>()
            .Where(component =>
                component.SiteId == siteContext.SiteId &&
                component.Name == normalizedName);

        return excludedComponentId is { } componentId
            ? query.AnyAsync(
                component => component.Id != componentId,
                cancellationToken)
            : query.AnyAsync(cancellationToken);
    }

    private static void ApplyRequest(
        PageCustomComponent component,
        SavePageCustomComponentRequest request)
    {
        var root = CustomComponentTemplate.Capture(request.Root);
        component.Name = request.Name.Trim();
        component.Description = request.Description?.Trim();
        component.Category = request.Category.Trim();
        component.Tags = (request.Tags ?? [])
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        component.Root = root;
        component.ReferencedCatalogIds =
            CustomComponentTemplate.GetReferencedCatalogIds(root).ToList();
    }
}
