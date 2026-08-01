const TIPTAP_VERSION = '3.27.4';
const TIPTAP_CORE_URL = `https://esm.sh/@tiptap/core@${TIPTAP_VERSION}`;
const TIPTAP_STARTER_KIT_URL = `https://esm.sh/@tiptap/starter-kit@${TIPTAP_VERSION}`;
const TIPTAP_LINK_URL = `https://esm.sh/@tiptap/extension-link@${TIPTAP_VERSION}`;

type TiptapChain = {
  focus(): TiptapChain;
  toggleBold(): TiptapChain;
  toggleItalic(): TiptapChain;
  toggleStrike(): TiptapChain;
  toggleCode(): TiptapChain;
  undo(): TiptapChain;
  redo(): TiptapChain;
  setLink(attributes: { href: string }): TiptapChain;
  unsetLink(): TiptapChain;
  run(): boolean;
};

type TiptapEditor = {
  chain(): TiptapChain;
  commands: { setHardBreak(): boolean };
  getJSON(): unknown;
  isActive(name: string): boolean;
  destroy(): void;
};

type FormattingState = {
  bold: boolean;
  italic: boolean;
  strike: boolean;
  code: boolean;
};

type DotNetCallback = {
  invokeMethodAsync(method: string, state: FormattingState): Promise<void>;
};

type TiptapEditorEvent = {
  editor: TiptapEditor;
};

type ConfigurableExtension = {
  configure(options: Record<string, unknown>): unknown;
};

type TiptapEditorConstructor = new (options: Record<string, unknown>) => TiptapEditor;

const editors = new Map<string, TiptapEditor>();

async function loadTiptap(): Promise<{
  Editor: TiptapEditorConstructor;
  StarterKit: ConfigurableExtension;
  Link: ConfigurableExtension;
}> {
  const [coreModule, starterKitModule, linkModule] = await Promise.all([
    import(TIPTAP_CORE_URL),
    import(TIPTAP_STARTER_KIT_URL),
    import(TIPTAP_LINK_URL),
  ]);

  return {
    Editor: coreModule.Editor as TiptapEditorConstructor,
    StarterKit: (starterKitModule.StarterKit ?? starterKitModule.default) as ConfigurableExtension,
    Link: (linkModule.Link ?? linkModule.default) as ConfigurableExtension,
  };
}

export async function initialize(
  element: HTMLElement,
  content: string,
  dotNetCallback: DotNetCallback,
): Promise<string> {
  const { Editor, StarterKit, Link } = await loadTiptap();
  let editor: TiptapEditor;
  let lastFormattingState: string | null = null;

  const reportFormattingState = ({ editor: currentEditor }: TiptapEditorEvent): void => {
    const state: FormattingState = {
      bold: currentEditor.isActive('bold'),
      italic: currentEditor.isActive('italic'),
      strike: currentEditor.isActive('strike'),
      code: currentEditor.isActive('code'),
    };
    const serialized = JSON.stringify(state);
    if (serialized === lastFormattingState) {
      return;
    }

    lastFormattingState = serialized;
    void dotNetCallback
      .invokeMethodAsync('OnFormattingStateChanged', state)
      .catch((error: unknown) => console.error('Aero Tiptap formatting-state update failed.', error));
  };

  editor = new Editor({
    element,
    content,
    autofocus: 'end',
    extensions: [
      StarterKit.configure({
        blockquote: false,
        bulletList: false,
        codeBlock: false,
        heading: false,
        horizontalRule: false,
        link: false,
        listItem: false,
        orderedList: false,
      }),
      Link.configure({
        autolink: true,
        linkOnPaste: true,
        openOnClick: false,
      }),
    ],
    editorProps: {
      attributes: {
        class: 'aero-tiptap-prosemirror',
      },
      handleKeyDown: (_view: unknown, event: KeyboardEvent): boolean => {
        if (event.key !== 'Enter') {
          return false;
        }

        return editor.commands.setHardBreak();
      },
    },
    onCreate: reportFormattingState,
    onSelectionUpdate: reportFormattingState,
    onTransaction: reportFormattingState,
  });

  const handle = crypto.randomUUID();
  editors.set(handle, editor);
  return handle;
}

export function execute(handle: string, command: string, argument?: string): boolean {
  const editor = requireEditor(handle);
  const chain = editor.chain().focus();

  switch (command) {
    case 'bold':
      return chain.toggleBold().run();
    case 'italic':
      return chain.toggleItalic().run();
    case 'strike':
      return chain.toggleStrike().run();
    case 'code':
      return chain.toggleCode().run();
    case 'undo':
      return chain.undo().run();
    case 'redo':
      return chain.redo().run();
    case 'link':
      return argument?.trim()
        ? chain.setLink({ href: argument.trim() }).run()
        : chain.unsetLink().run();
    case 'unlink':
      return chain.unsetLink().run();
    default:
      throw new Error(`Unknown Tiptap command '${command}'.`);
  }
}

export function getDocumentJson(handle: string): string {
  return JSON.stringify(requireEditor(handle).getJSON());
}

export function dispose(handle: string): void {
  const editor = editors.get(handle);
  editor?.destroy();
  editors.delete(handle);
}

function requireEditor(handle: string): TiptapEditor {
  const editor = editors.get(handle);
  if (!editor) {
    throw new Error('The Tiptap editor instance is not available.');
  }

  return editor;
}
