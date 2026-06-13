using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

public sealed class TeamSection2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.team-sections.2";

    public string DisplayName => "Team Sections 2";

    public string? Description => "Three-column team grid with LinkedIn icon and member description.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "users";

    public int SortOrder => 112;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(TeamSection2BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(TeamSection2BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Our Team",
            Description = "Meet the people behind our success.",
            TeamMembers = TeamSection2Block.DefaultMembers.Select(ToEditorMember).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToTeamSectionBlock(editorBlock);
        return TeamSection2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToTeamSectionBlock(editorBlock);

    private static TeamSection2Block ToTeamSectionBlock(EditorBlock editorBlock)
    {
        return new TeamSection2Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Our Team"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Meet the people behind our success."),
            Members = editorBlock.TeamMembers.Count > 0
                ? editorBlock.TeamMembers.Select(ToTeamMember).ToList()
                : TeamSection2Block.DefaultMembers.Select(CloneMember).ToList()
        };
    }

    private static AeroTeamMember ToEditorMember(TeamMember2 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl,
        Description = m.Description,
        LinkedInUrl = m.LinkedInUrl
    };

    private static TeamMember2 ToTeamMember(AeroTeamMember m) => new()
    {
        Name = m.Name ?? string.Empty,
        Role = m.Role ?? string.Empty,
        AvatarUrl = m.AvatarUrl ?? string.Empty,
        Description = m.Description ?? string.Empty,
        LinkedInUrl = string.IsNullOrWhiteSpace(m.LinkedInUrl) ? "#" : m.LinkedInUrl!
    };

    private static TeamMember2 CloneMember(TeamMember2 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl,
        Description = m.Description,
        LinkedInUrl = m.LinkedInUrl
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
