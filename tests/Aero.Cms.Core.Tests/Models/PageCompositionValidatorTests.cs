using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Validators;
using Shouldly;

namespace Aero.Cms.Core.Tests.Models;

public sealed class PageCompositionValidatorTests
{
    [Test]
    public async Task Validate_accepts_provider_only_list_and_rejects_unsupported_virtual_query_features()
    {
        var fixture = CreateContent();
        var valid = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = fixture.Scope.NodeId,
                    ContentEntryProvider = "view:catalog",
                    ContentTypeAlias = "catalog",
                    TemplateRootNodeId = fixture.Template.NodeId,
                    Query = new PageContentListQuery { PageSize = 12 }
                }
            ]
        };

        (await new PageCompositionValidator(fixture.Content).ValidateAsync(valid)).IsValid.ShouldBeTrue();

        var invalid = valid with
        {
            ContentLists =
            [
                valid.ContentLists[0] with
                {
                    Query = new PageContentListQuery
                    {
                        PageSize = 12,
                        SortField = "title",
                        Filters = [new PageContentFilter { FieldName = "title", Operator = PageContentFilterOperator.Equals, Value = "sample" }]
                    }
                }
            ]
        };
        var result = await new PageCompositionValidator(fixture.Content).ValidateAsync(invalid);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(error => error.ErrorMessage.Contains("does not support sorting", StringComparison.Ordinal));
        result.Errors.ShouldContain(error => error.ErrorMessage.Contains("Contains search filter", StringComparison.Ordinal));
    }
    [Test]
    public async Task Validate_accepts_bounded_scope_query_and_descendant_field_binding()
    {
        var fixture = CreateContent();
        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = fixture.Scope.NodeId,
                    ContentTypeId = 101,
                    ContentTypeAlias = "articles",
                    TemplateRootNodeId = fixture.Template.NodeId,
                    Query = new PageContentListQuery
                    {
                        PageSize = 12,
                        SortField = "publishedOn",
                        SortDirection = PageContentSortDirection.Descending,
                        Filters =
                        [
                            new PageContentFilter
                            {
                                FieldName = "category",
                                Operator = PageContentFilterOperator.Equals,
                                Value = "news"
                            }
                        ]
                    }
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = fixture.Target.NodeId,
                    ScopeNodeId = fixture.Scope.NodeId,
                    FieldName = "title",
                    Target = PageFieldBindingTarget.TextContent
                }
            ]
        };

        var result = await new PageCompositionValidator(fixture.Content)
            .ValidateAsync(composition);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_rejects_duplicate_scopes_unbounded_queries_and_cross_scope_bindings()
    {
        var fixture = CreateContent();
        var outsideNode = HtmlNode.CreateElement("p");
        fixture.Content.Root.Children.Add(outsideNode);
        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = fixture.Scope.NodeId,
                    ContentTypeId = 101,
                    ContentTypeAlias = "articles",
                    TemplateRootNodeId = fixture.Template.NodeId,
                    Query = new PageContentListQuery { PageSize = 0 }
                }
            ],
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = fixture.Scope.NodeId,
                    ContentTypeId = 101,
                    ContentTypeAlias = "articles",
                    ContentItemId = 202
                }
            ],
            FieldBindings =
            [
                new PageFieldBinding
                {
                    NodeId = outsideNode.NodeId,
                    ScopeNodeId = fixture.Scope.NodeId,
                    FieldName = "title",
                    Target = PageFieldBindingTarget.TextContent
                }
            ]
        };

        var result = await new PageCompositionValidator(fixture.Content)
            .ValidateAsync(composition);
        var messages = result.Errors.Select(error => error.ErrorMessage).ToArray();

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(messages.Any(message => message.Contains("more than one content scope"))).IsTrue();
        await Assert.That(messages.Any(message => message.Contains("page size must be between"))).IsTrue();
        await Assert.That(messages.Any(message => message.Contains("must be inside scope"))).IsTrue();
    }

    [Test]
    public async Task Validate_rejects_more_than_the_bounded_filter_count()
    {
        var fixture = CreateContent();
        var composition = new PageCompositionDocument
        {
            ContentLists =
            [
                new PageContentListScope
                {
                    NodeId = fixture.Scope.NodeId,
                    ContentTypeId = 101,
                    ContentTypeAlias = "articles",
                    TemplateRootNodeId = fixture.Template.NodeId,
                    Query = new PageContentListQuery
                    {
                        Filters = Enumerable.Range(0, PageContentListQuery.MaximumFilterCount + 1)
                            .Select(index => new PageContentFilter
                            {
                                FieldName = $"field{index}",
                                Operator = PageContentFilterOperator.IsNotEmpty
                            })
                            .ToArray()
                    }
                }
            ]
        };

        var result = await new PageCompositionValidator(fixture.Content)
            .ValidateAsync(composition);

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Any(error =>
            error.ErrorMessage.Contains("cannot contain more than"))).IsTrue();
    }

    [Test]
    public async Task Validate_accepts_one_bounded_rendered_fragment_on_an_html_element()
    {
        var fixture = CreateContent();
        var fragmentNode = HtmlNode.CreateElement("section");
        fixture.Content.Root.Children.Add(fragmentNode);
        var composition = new PageCompositionDocument
        {
            RenderedFragments =
            [
                new PageRenderedFragment
                {
                    NodeId = fragmentNode.NodeId,
                    Kind = PageRenderedFragmentKind.Markdown,
                    Source = "# Release notes"
                }
            ]
        };

        var result = await new PageCompositionValidator(fixture.Content)
            .ValidateAsync(composition);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Validate_rejects_duplicate_oversized_and_scope_owning_rendered_fragments()
    {
        var fixture = CreateContent();
        var composition = new PageCompositionDocument
        {
            ContentItems =
            [
                new PageContentItemScope
                {
                    NodeId = fixture.Scope.NodeId,
                    ContentTypeId = 101,
                    ContentTypeAlias = "articles",
                    ContentItemId = 202
                }
            ],
            RenderedFragments =
            [
                new PageRenderedFragment
                {
                    NodeId = fixture.Scope.NodeId,
                    Kind = PageRenderedFragmentKind.Markdown,
                    Source = new string('x', PageRenderedFragment.MaximumSourceLength + 1)
                },
                new PageRenderedFragment
                {
                    NodeId = fixture.Scope.NodeId,
                    Kind = PageRenderedFragmentKind.CustomHtml,
                    Source = "<p>Duplicate</p>"
                }
            ]
        };

        var result = await new PageCompositionValidator(fixture.Content)
            .ValidateAsync(composition);
        var messages = result.Errors.Select(error => error.ErrorMessage).ToArray();

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(messages.Any(message => message.Contains("more than one rendered fragment"))).IsTrue();
        await Assert.That(messages.Any(message => message.Contains("cannot exceed"))).IsTrue();
        await Assert.That(messages.Any(message => message.Contains("both a content scope"))).IsTrue();
    }

    private static CompositionFixture CreateContent()
    {
        var scope = HtmlNode.CreateElement("section");
        var template = HtmlNode.CreateElement("article");
        var target = HtmlNode.CreateElement("h2");
        target.Children.Add(HtmlNode.CreateText("Placeholder"));
        template.Children.Add(target);
        scope.Children.Add(template);
        var content = new HtmlPageContent();
        content.Root.Children.Add(scope);
        return new CompositionFixture(content, scope, template, target);
    }

    private sealed record CompositionFixture(
        HtmlPageContent Content,
        HtmlNode Scope,
        HtmlNode Template,
        HtmlNode Target);
}
