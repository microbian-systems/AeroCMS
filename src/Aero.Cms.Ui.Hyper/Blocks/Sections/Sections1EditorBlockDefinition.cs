using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Sections;

public sealed class Sections1EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.sections.1";
    public string DisplayName => "Sections 1";
    public string? Description => "2-column grid with text left, image right.";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "columns";
    public int SortOrder => 77;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(Sections1BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(Sections1BlockEditor);

    public EditorBlock CreateDefaultEditorBlock()
    {
        return new EditorBlock
        {
            Type = CatalogId,
            MainText = "Lorem ipsum dolor sit amet consectetur adipisicing elit.",
            Description = "Lorem ipsum dolor sit amet consectetur adipisicing elit. Tenetur doloremque saepe architecto maiores repudiandae amet perferendis repellendus, reprehenderit voluptas sequi."
        };
    }

    public NeoPageNode ToNeoPageNode(EditorBlock editorBlock)
    {
        var block = ToSectionsBlock(editorBlock);
        return Sections1BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToSectionsBlock(editorBlock);

    private static Sections1Block ToSectionsBlock(EditorBlock editorBlock)
    {
        return new Sections1Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1731690415686-e68f78e2b5bd?auto=format&fit=crop&q=80&w=1160")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
