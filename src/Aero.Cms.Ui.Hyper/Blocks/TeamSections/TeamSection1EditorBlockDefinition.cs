using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

public sealed class TeamSection1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.team-sections.1";

    public string DisplayName => "Team Sections 1";

    public string? Description => "Three-column team grid with LinkedIn icon support.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "users";

    public int SortOrder => 111;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(TeamSection1BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(TeamSection1BlockEditor);

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

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToTeamSectionBlock(editorBlock);
        return TeamSection1BlockMapper.ToNode(block);
    }

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
