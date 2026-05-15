using Aero.Cms.Abstractions.Blocks;
using Aero.Cms.Abstractions.Blocks.Common;
using Aero.Cms.Abstractions.Blocks.Editor;
using Aero.Cms.Abstractions.Blocks.Neo;

namespace Aero.Cms.Ui.Hyper.Blocks.Sections;

public sealed class Sections2EditorBlockDefinition : IPageEditorBlockDefinition
{
    public string CatalogId => "hyper.sections.2";
    public string DisplayName => "Sections 2";
    public string? Description => "4-column grid with text left (col-span-1), image right (col-span-3).";
    public string Category => "Hyper";
    public string Kind => "Block";
    public string IconName => "columns";
    public int SortOrder => 78;
    public bool PublicStaticSsrSafe => true;
    public Type? PreviewComponentType => typeof(Sections2BlockEditorPreview);
    public Type? PropertyEditorComponentType => typeof(Sections2BlockEditor);

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
        return Sections2BlockMapper.ToNode(block);
    }

    public BlockBase? ToBlockBase(EditorBlock editorBlock) => ToSectionsBlock(editorBlock);

    private static Sections2Block ToSectionsBlock(EditorBlock editorBlock)
    {
        return new Sections2Block
        {
            Title = FirstNonEmpty(editorBlock.MainText, editorBlock.Title, editorBlock.PageTitle, "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
            Description = FirstNonEmpty(editorBlock.Description, editorBlock.SubText, editorBlock.PageDescription, "Lorem ipsum dolor sit amet consectetur adipisicing elit."),
            ImageUrl = FirstNonEmpty(editorBlock.Src, "https://images.unsplash.com/photo-1731690415686-e68f78e2b5bd?auto=format&fit=crop&q=80&w=1160")
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
