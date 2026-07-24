using System.Collections.Immutable;
using System.Text.Json;
using Aero.Cms.Abstractions.Content;
using Aero.Cms.Abstractions.Enums;
using Aero.Cms.Core.Content.Services;
using Aero.Core;
using Aero.Core.Railway;
using AeroDB.Sable;
using NSubstitute;
using Shouldly;

namespace Aero.Cms.Core.Tests.Content;

public sealed class ContentHierarchyTests
{
    [Test]
    public async Task Query_returns_a_site_culture_scoped_immutable_tree()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            Item(10, title: "Root", sortOrder: 0, fields: Fields(("summary", "root"), ("private", "hidden"))),
            Item(11, title: "Second", parentId: 10, sortOrder: 20, fields: Fields(("summary", "second"))),
            Item(12, title: "First", parentId: 10, sortOrder: 10, fields: Fields(("summary", "first"))),
            Item(13, title: "Grandchild", parentId: 12, sortOrder: 0, fields: Fields(("summary", "grandchild"))),
            Item(14, title: "Draft", parentId: 10, sortOrder: 0, publicationState: ContentPublicationState.Draft),
            Item(15, title: "Other site", siteId: 2),
            Item(16, title: "Other culture", culture: "fr-FR"));
        await harness.Session.SaveChangesAsync();

        var service = new ContentHierarchyQueryService(
            harness.Session,
            ContentTypeService(HierarchicalType()));
        var result = await service.QueryAsync(new ContentQueryRequest(
            "navigation",
            1,
            1,
            "category",
            "en-US",
            ContentTraversal.RootsWithDescendants,
            MaximumDepth: 4,
            MaximumItems: 20,
            Projection: ["summary"]));

        var success = result as Result<ContentQueryResult>.Ok;
        await Assert.That(success).IsNotNull();
        await Assert.That(success!.Value.TotalItems).IsEqualTo(4);
        await Assert.That(success.Value.WasTruncated).IsFalse();
        await Assert.That(success.Value.Roots).HasCount(1);
        await Assert.That(success.Value.Roots[0].Id).IsEqualTo("10");
        await Assert.That(success.Value.Roots[0].Fields.Keys).IsEquivalentTo(["summary"]);
        await Assert.That(success.Value.Roots[0].Children.Select(node => node.Id))
            .IsEquivalentTo(["12", "11"]);
        await Assert.That(success.Value.Roots[0].Children[0].Children[0].Id)
            .IsEqualTo("13");
    }

    [Test]
    public async Task Query_enforces_the_requested_depth_and_marks_truncation()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            Item(20, title: "Root"),
            Item(21, title: "Child", parentId: 20),
            Item(22, title: "Grandchild", parentId: 21));
        await harness.Session.SaveChangesAsync();

        var service = new ContentHierarchyQueryService(
            harness.Session,
            ContentTypeService(HierarchicalType()));
        var result = await service.QueryAsync(new ContentQueryRequest(
            "tree",
            1,
            1,
            "category",
            "en-US",
            ContentTraversal.RootsWithDescendants,
            MaximumDepth: 1,
            MaximumItems: 20));

        var success = result as Result<ContentQueryResult>.Ok;
        await Assert.That(success).IsNotNull();
        await Assert.That(success!.Value.TotalItems).IsEqualTo(2);
        await Assert.That(success.Value.WasTruncated).IsTrue();
        await Assert.That(success.Value.Roots[0].Children[0].Children).IsEmpty();
    }

    [Test]
    public async Task Query_rejects_fields_not_declared_by_the_content_type()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();

        var service = new ContentHierarchyQueryService(
            harness.Session,
            ContentTypeService(HierarchicalType()));
        var result = await service.QueryAsync(new ContentQueryRequest(
            "tree",
            1,
            1,
            "category",
            "en-US",
            ContentTraversal.Roots,
            Projection: ["not-declared"]));

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task Ancestor_query_honors_the_depth_bound_and_marks_truncation()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            Item(24, title: "Root"),
            Item(25, title: "Parent", parentId: 24),
            Item(26, title: "Current", parentId: 25));
        await harness.Session.SaveChangesAsync();

        var service = new ContentHierarchyQueryService(
            harness.Session,
            ContentTypeService(HierarchicalType()));
        var result = await service.QueryAsync(new ContentQueryRequest(
            "breadcrumb",
            1,
            1,
            "category",
            "en-US",
            ContentTraversal.Ancestors,
            RootId: 26,
            MaximumDepth: 1,
            MaximumItems: 20));

        var success = result as Result<ContentQueryResult>.Ok;
        await Assert.That(success).IsNotNull();
        await Assert.That(success!.Value.Roots.Select(node => node.Id))
            .IsEquivalentTo(["25"]);
        await Assert.That(success.Value.WasTruncated).IsTrue();
    }

    [Test]
    public async Task Descendant_query_does_not_lose_a_branch_after_five_hundred_other_items()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var unrelated = Enumerable.Range(0, 500)
            .Select(index => Item(
                1_000 + index,
                title: $"Unrelated {index:D3}",
                sortOrder: index))
            .ToArray();
        harness.Session.Store(unrelated);
        harness.Session.Store(
            Item(2_000, title: "Requested root", sortOrder: 10_000),
            Item(2_001, title: "Requested child", parentId: 2_000));
        await harness.Session.SaveChangesAsync();

        var service = new ContentHierarchyQueryService(
            harness.Session,
            ContentTypeService(HierarchicalType()));
        var result = await service.QueryAsync(new ContentQueryRequest(
            "branch",
            1,
            1,
            "category",
            "en-US",
            ContentTraversal.Descendants,
            RootId: 2_000,
            MaximumDepth: 4,
            MaximumItems: 20));

        var success = result.ShouldBeOfType<Result<ContentQueryResult>.Ok>();
        success.Value.Roots.Select(node => node.Id).ShouldBe(["2001"]);
        success.Value.WasTruncated.ShouldBeFalse();
    }

    [Test]
    public async Task Validator_rejects_a_parent_for_a_flat_content_type()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var validator = new ContentHierarchyValidator(
            harness.Session,
            Substitute.For<IContentTypeService>());

        var result = await validator.ValidateAsync(
            Item(30, parentId: 29),
            new ContentTypeDefinition
            {
                Id = 1,
                SiteId = 1,
                Alias = "category",
                Structure = ContentStructure.Flat
            },
            ContentValidationMode.Draft);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task Validator_enforces_singleton_cardinality_per_site_and_culture()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(Item(40, title: "Existing"));
        await harness.Session.SaveChangesAsync();
        var validator = new ContentHierarchyValidator(
            harness.Session,
            Substitute.For<IContentTypeService>());

        var result = await validator.ValidateAsync(
            Item(0, title: "Second"),
            new ContentTypeDefinition
            {
                Id = 1,
                SiteId = 1,
                Alias = "category",
                Cardinality = ContentCardinality.Singleton,
                Structure = ContentStructure.Flat
            },
            ContentValidationMode.Draft);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task Validator_requires_published_ancestors_when_publishing()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(Item(
            50,
            title: "Draft parent",
            publicationState: ContentPublicationState.Draft));
        await harness.Session.SaveChangesAsync();
        var validator = new ContentHierarchyValidator(
            harness.Session,
            Substitute.For<IContentTypeService>());

        var result = await validator.ValidateAsync(
            Item(
                51,
                title: "Published child",
                parentId: 50,
                publicationState: ContentPublicationState.Published),
            HierarchicalType(),
            ContentValidationMode.Publish);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task Validator_rejects_a_move_that_pushes_existing_descendants_past_maximum_depth()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        harness.Session.Store(
            Item(60, title: "Root"),
            Item(61, title: "Deep parent", parentId: 60),
            Item(62, title: "Moving root"),
            Item(63, title: "Existing child", parentId: 62));
        await harness.Session.SaveChangesAsync();
        var validator = new ContentHierarchyValidator(
            harness.Session,
            Substitute.For<IContentTypeService>());
        var type = HierarchicalType();
        type.HierarchyRules = new ContentHierarchyRules { MaximumDepth = 2 };

        var result = await validator.ValidateAsync(
            Item(62, title: "Moving root", parentId: 61),
            type,
            ContentValidationMode.Draft);

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task Validator_applies_the_parent_type_allowlist_only_to_the_direct_parent()
    {
        await using var harness = new SableTestHarness()
            .WithSchema<ContentItem>(SchemaMode.Flexible);
        await harness.InitializeAsync();
        var department = Item(70, title: "Department");
        department.ContentTypeAlias = "department";
        var category = Item(71, title: "Category", parentId: 70);
        category.ContentTypeAlias = "category";
        harness.Session.Store(department, category);
        await harness.Session.SaveChangesAsync();

        var typeService = Substitute.For<IContentTypeService>();
        typeService.GetByAliasAsync(
                1,
                "category",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(new ContentTypeDefinition
                {
                    Id = 2,
                    SiteId = 1,
                    Alias = "category",
                    Structure = ContentStructure.Hierarchical
                })));
        var validator = new ContentHierarchyValidator(harness.Session, typeService);
        var childType = new ContentTypeDefinition
        {
            Id = 3,
            SiteId = 1,
            Alias = "article",
            Structure = ContentStructure.Hierarchical,
            HierarchyRules = new ContentHierarchyRules
            {
                RequireSameTypeParent = false,
                AllowedParentContentTypeIds = [2],
                MaximumDepth = 8
            }
        };
        var child = Item(72, title: "Article", parentId: 71);
        child.ContentTypeAlias = "article";

        var result = await validator.ValidateAsync(
            child,
            childType,
            ContentValidationMode.Draft);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    private static IContentTypeService ContentTypeService(ContentTypeDefinition definition)
    {
        var service = Substitute.For<IContentTypeService>();
        service.GetByIdAsync(
                definition.SiteId,
                definition.Id,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<ContentTypeDefinition, AeroError>>(
                new Result<ContentTypeDefinition, AeroError>.Ok(definition)));
        return service;
    }

    private static ContentTypeDefinition HierarchicalType()
        => new()
        {
            Id = 1,
            SiteId = 1,
            Alias = "category",
            Name = "Category",
            Structure = ContentStructure.Hierarchical,
            HierarchyRules = new ContentHierarchyRules { MaximumDepth = 8 },
            Fields =
            [
                new ContentFieldDefinition { Name = "summary", FieldType = "text" },
                new ContentFieldDefinition { Name = "private", FieldType = "text" }
            ]
        };

    private static ContentItem Item(
        long id,
        string title = "Item",
        long siteId = 1,
        string culture = "en-US",
        long? parentId = null,
        int sortOrder = 0,
        ContentPublicationState publicationState = ContentPublicationState.Published,
        Dictionary<string, JsonElement>? fields = null)
        => new()
        {
            Id = id,
            SiteId = siteId,
            ContentTypeAlias = "category",
            Culture = culture,
            Slug = title.ToLowerInvariant().Replace(' ', '-'),
            Title = title,
            ParentId = parentId,
            SortOrder = sortOrder,
            PublicationState = publicationState,
            Fields = fields ?? []
        };

    private static Dictionary<string, JsonElement> Fields(
        params (string Name, string Value)[] fields)
        => fields.ToDictionary(
            field => field.Name,
            field => JsonSerializer.SerializeToElement(field.Value),
            StringComparer.OrdinalIgnoreCase);
}
