using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.TeamSections;

public sealed class TeamSection3EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.team-sections.3";

    public string DisplayName => "Team Sections 3";

    public string? Description => "Circular avatar grid in 6-column layout.";

    public string Category => "Hyper";

    public string Kind => "Block";

    public string IconName => "users";

    public int SortOrder => 113;

    public bool PublicStaticSsrSafe => true;

    public Type? PreviewComponentType => typeof(TeamSection3BlockEditorPreview);

    public Type? PropertyEditorComponentType => typeof(TeamSection3BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            Title = "Our Team",
            Description = "Meet the people behind our success.",
            TeamMembers = TeamSection3Block.DefaultMembers.Select(ToEditorMember).ToList()
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToTeamSectionBlock(editorBlock);
        return TeamSection3BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToTeamSectionBlock(editorBlock);

    private static TeamSection3Block ToTeamSectionBlock(EditorBlock editorBlock)
    {
        return new TeamSection3Block
        {
            Title = FirstNonEmpty(editorBlock.Title, editorBlock.MainText, editorBlock.PageTitle, "Our Team"),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Meet the people behind our success."),
            Members = editorBlock.TeamMembers.Count > 0
                ? editorBlock.TeamMembers.Select(ToTeamMember).ToList()
                : TeamSection3Block.DefaultMembers.Select(CloneMember).ToList()
        };
    }

    private static AeroTeamMember ToEditorMember(TeamMember3 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl
    };

    private static TeamMember3 ToTeamMember(AeroTeamMember m) => new()
    {
        Name = m.Name ?? string.Empty,
        Role = m.Role ?? string.Empty,
        AvatarUrl = m.AvatarUrl ?? string.Empty
    };

    private static TeamMember3 CloneMember(TeamMember3 m) => new()
    {
        Name = m.Name,
        Role = m.Role,
        AvatarUrl = m.AvatarUrl
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
