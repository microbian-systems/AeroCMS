using Aero.Cms.Abstractions.Pages.Composition;
using Aero.Cms.Html;
using Aero.Cms.Modules.Pages.Validators;

namespace Aero.Cms.Core.Tests.Models;

public sealed class PageCompositionValidatorTests
{
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
