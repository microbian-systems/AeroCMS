const TIPTAP_VERSION = '3.27.4';
const TIPTAP_CORE_URL = `https://esm.sh/@tiptap/core@${TIPTAP_VERSION}`;
const TIPTAP_STARTER_KIT_URL = `https://esm.sh/@tiptap/starter-kit@${TIPTAP_VERSION}`;
const TIPTAP_LINK_URL = `https://esm.sh/@tiptap/extension-link@${TIPTAP_VERSION}`;

type MarkdownTiptapChain = {
  focus(): MarkdownTiptapChain;
  undo(): MarkdownTiptapChain;
  redo(): MarkdownTiptapChain;
  setParagraph(): MarkdownTiptapChain;
  toggleHeading(attributes: { level: number }): MarkdownTiptapChain;
  toggleBulletList(): MarkdownTiptapChain;
  toggleOrderedList(): MarkdownTiptapChain;
  toggleBlockquote(): MarkdownTiptapChain;
  toggleCodeBlock(): MarkdownTiptapChain;
  setHorizontalRule(): MarkdownTiptapChain;
  toggleBold(): MarkdownTiptapChain;
  toggleItalic(): MarkdownTiptapChain;
  toggleStrike(): MarkdownTiptapChain;
  toggleCode(): MarkdownTiptapChain;
  setLink(attributes: { href: string }): MarkdownTiptapChain;
  unsetLink(): MarkdownTiptapChain;
  run(): boolean;
};

type MarkdownTiptapEditor = {
  chain(): MarkdownTiptapChain;
  getHTML(): string;
  destroy(): void;
};

type ConfigurableExtension = {
  configure(options: Record<string, unknown>): unknown;
};

type MarkdownTiptapEditorConstructor = new (
  options: Record<string, unknown>,
) => MarkdownTiptapEditor;

const editors = new Map<string, MarkdownTiptapEditor>();

async function loadTiptap(): Promise<{
  Editor: MarkdownTiptapEditorConstructor;
  StarterKit: ConfigurableExtension;
  Link: ConfigurableExtension;
}> {
  const [coreModule, starterKitModule, linkModule] = await Promise.all([
    import(TIPTAP_CORE_URL),
    import(TIPTAP_STARTER_KIT_URL),
    import(TIPTAP_LINK_URL),
  ]);

  return {
    Editor: coreModule.Editor as MarkdownTiptapEditorConstructor,
    StarterKit: (starterKitModule.StarterKit ?? starterKitModule.default) as ConfigurableExtension,
    Link: (linkModule.Link ?? linkModule.default) as ConfigurableExtension,
  };
}

export async function initialize(element: HTMLElement, content: string): Promise<string> {
  const { Editor, StarterKit, Link } = await loadTiptap();
  const editor = new Editor({
    element,
    content,
    autofocus: 'end',
    extensions: [
      StarterKit.configure({ link: false }),
      Link.configure({
        autolink: true,
        linkOnPaste: true,
        openOnClick: false,
      }),
    ],
    editorProps: {
      attributes: { class: 'aero-tiptap-markdown-prosemirror' },
    },
  });

  const handle = crypto.randomUUID();
  editors.set(handle, editor);
  return handle;
}

export function execute(handle: string, command: string, argument?: string): boolean {
  const editor = requireEditor(handle);
  const chain = editor.chain().focus();

  switch (command) {
    case 'undo': return chain.undo().run();
    case 'redo': return chain.redo().run();
    case 'paragraph': return chain.setParagraph().run();
    case 'heading2': return chain.toggleHeading({ level: 2 }).run();
    case 'heading3': return chain.toggleHeading({ level: 3 }).run();
    case 'bulletList': return chain.toggleBulletList().run();
    case 'orderedList': return chain.toggleOrderedList().run();
    case 'blockquote': return chain.toggleBlockquote().run();
    case 'codeBlock': return chain.toggleCodeBlock().run();
    case 'horizontalRule': return chain.setHorizontalRule().run();
    case 'bold': return chain.toggleBold().run();
    case 'italic': return chain.toggleItalic().run();
    case 'strike': return chain.toggleStrike().run();
    case 'code': return chain.toggleCode().run();
    case 'link':
      return argument?.trim()
        ? chain.setLink({ href: argument.trim() }).run()
        : chain.unsetLink().run();
    case 'unlink': return chain.unsetLink().run();
    default: throw new Error(`Unknown Tiptap Markdown command '${command}'.`);
  }
}

export function getHtml(handle: string): string {
  return requireEditor(handle).getHTML();
}

export function dispose(handle: string): void {
  const editor = editors.get(handle);
  editor?.destroy();
  editors.delete(handle);
}

function requireEditor(handle: string): MarkdownTiptapEditor {
  const editor = editors.get(handle);
  if (!editor) {
    throw new Error('The Tiptap Markdown editor instance is not available.');
  }

  return editor;
}
