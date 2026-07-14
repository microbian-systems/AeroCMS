const TIPTAP_VERSION = '3.27.4';
const TIPTAP_CORE_URL = `https://esm.sh/@tiptap/core@${TIPTAP_VERSION}`;
const TIPTAP_STARTER_KIT_URL = `https://esm.sh/@tiptap/starter-kit@${TIPTAP_VERSION}`;
const TIPTAP_LINK_URL = `https://esm.sh/@tiptap/extension-link@${TIPTAP_VERSION}`;
const editors = new Map();
async function loadTiptap() {
    const [coreModule, starterKitModule, linkModule] = await Promise.all([
        import(TIPTAP_CORE_URL),
        import(TIPTAP_STARTER_KIT_URL),
        import(TIPTAP_LINK_URL),
    ]);
    return {
        Editor: coreModule.Editor,
        StarterKit: (starterKitModule.StarterKit ?? starterKitModule.default),
        Link: (linkModule.Link ?? linkModule.default),
    };
}
export async function initialize(element, content) {
    const { Editor, StarterKit, Link } = await loadTiptap();
    let editor;
    editor = new Editor({
        element,
        content,
        autofocus: 'end',
        extensions: [
            StarterKit.configure({
                blockquote: false,
                bulletList: false,
                code: false,
                codeBlock: false,
                heading: false,
                horizontalRule: false,
                listItem: false,
                orderedList: false,
                strike: false,
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
            handleKeyDown: (_view, event) => {
                if (event.key !== 'Enter') {
                    return false;
                }
                return editor.commands.setHardBreak();
            },
        },
    });
    const handle = crypto.randomUUID();
    editors.set(handle, editor);
    return handle;
}
export function execute(handle, command, argument) {
    const editor = requireEditor(handle);
    const chain = editor.chain().focus();
    switch (command) {
        case 'bold':
            return chain.toggleBold().run();
        case 'italic':
            return chain.toggleItalic().run();
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
export function getDocumentJson(handle) {
    return JSON.stringify(requireEditor(handle).getJSON());
}
export function dispose(handle) {
    const editor = editors.get(handle);
    editor?.destroy();
    editors.delete(handle);
}
function requireEditor(handle) {
    const editor = editors.get(handle);
    if (!editor) {
        throw new Error('The Tiptap editor instance is not available.');
    }
    return editor;
}
