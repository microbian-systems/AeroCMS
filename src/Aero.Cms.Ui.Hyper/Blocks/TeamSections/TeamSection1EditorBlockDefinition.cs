using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

/// <summary>
/// Represents a class for TeamSection1EditorBlockDefinition.
/// </summary>
public sealed class TeamSection1EditorBlockDefinition : IPageEditorBlockDefinition
{
        /// <summary>
    /// Gets or sets the Catalog Id.
    /// </summary>
public string CatalogId => "hyper.team-sections.1";

        /// <summary>
    /// Gets or sets the Display Name.
    /// </summary>
public string DisplayName => "Team Sections 1";

        /// <summary>
    /// Gets or sets the Description.
    /// </summary>
public string? Description => "Three-column team grid with LinkedIn icon support.";

        /// <summary>
    /// Gets or sets the Category.
    /// </summary>
public string Category => "Hyper";

        /// <summary>
    /// Gets or sets the Kind.
    /// </summary>
public string Kind => "Block";

        /// <summary>
    /// Gets or sets the Icon Name.
    /// </summary>
public string IconName => "users";

        /// <summary>
    /// Gets or sets the Sort Order.
    /// </summary>
public int SortOrder => 111;

        /// <summary>
    /// Gets or sets the Public Static Ssr Safe.
    /// </summary>
public bool PublicStaticSsrSafe => true;

        /// <summary>
    /// Gets or sets the Preview Component Type.
    /// </summary>
public Type? PreviewComponentType => typeof(TeamSection1BlockEditorPreview);

        /// <summary>
    /// Gets or sets the Property Editor Component Type.
    /// </summary>
public Type? PropertyEditorComponentType => typeof(TeamSection1BlockEditor);

        /// <summary>
    /// CreateDefaultEditorBlock method.
    /// </summary>
public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Our Team",
            Description = "Meet the people behind our success.",
            TeamMembers = TeamSection1Block.DefaultMembers.Select(ToEditorMember).ToList()
        };
    }

        /// <summary>
    /// ToNeoPageNode method.
    /// </summary>
public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToTeamSectionBlock(editorBlock);
        return TeamSection1BlockMapper.ToNode(block);
    }

        /// <summary>
    /// ToBlockBase method.
    /// </summary>
public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToTeamSectionBlock(editorBlock);

    private static TeamSection1Block ToTeamSectionBlock(EditorBlock editorBlock)
    {
        return new TeamSection1Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Our Team"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Meet the people behind our success."),
            Members = editorBlock.TeamMembers.Count > 0
                ? editorBlock.TeamMembers.Select(ToTeamMember).ToList()
                : TeamSection1Block.DefaultMembers.Select(CloneMember).ToList()
        };
    }

    private static AeroTeamMember ToEditorMember(TeamMember1 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl,
        LinkedInUrl = m.LinkedInUrl
    };

    private static TeamMember1 ToTeamMember(AeroTeamMember m) => new()
    {
        Name = m.Name ?? string.Empty,
        Role = m.Role ?? string.Empty,
        AvatarUrl = m.AvatarUrl ?? string.Empty,
        LinkedInUrl = string.IsNullOrWhiteSpace(m.LinkedInUrl) ? "#" : m.LinkedInUrl!
    };

    private static TeamMember1 CloneMember(TeamMember1 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl,
        LinkedInUrl = m.LinkedInUrl
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
