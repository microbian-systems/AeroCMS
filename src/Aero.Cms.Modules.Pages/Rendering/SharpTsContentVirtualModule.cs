using System.Text.Json;
using Aero.Cms.Abstractions.Content;

namespace Aero.Cms.Modules.Pages.Rendering;

/// <summary>
/// Builds the single virtual module available to page-authored TypeScript.
/// The module contains only the immutable query snapshots resolved before rendering.
/// </summary>
internal static class SharpTsContentVirtualModule
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 12
    };

    public static string Build(IReadOnlyDictionary<string, ContentQueryResult> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var contentJson = JsonSerializer.Serialize(content, SerializerOptions);

        return $$"""
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

            const queries: { [name: string]: AeroContentQueryResult } = {{contentJson}};

            export function getQuery(name: string): AeroContentQueryResult | null {
                const query = queries[name];
                return query === undefined ? null : query;
            }

            export function findById(
                query: AeroContentQueryResult,
                id: string
            ): AeroContentNode | null {
                const nodes = flatten(query);
                for (const node of nodes) {
                    if (node.id === id) {
                        return node;
                    }
                }
                return null;
            }

            export function flatten(query: AeroContentQueryResult): AeroContentNode[] {
                const result: AeroContentNode[] = [];
                const visit = (nodes: AeroContentNode[]): void => {
                    for (const node of nodes) {
                        result.push(node);
                        visit(node.children);
                    }
                };
                visit(query.roots);
                return result;
            }
            """;
    }
}
