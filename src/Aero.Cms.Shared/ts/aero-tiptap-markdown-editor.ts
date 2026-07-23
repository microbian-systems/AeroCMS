const TIPTAP_VERSION = '3.27.4';
const TIPTAP_CORE_URL = `https://esm.sh/@tiptap/core@${TIPTAP_VERSION}`;
const TIPTAP_STARTER_KIT_URL = `https://esm.sh/@tiptap/starter-kit@${TIPTAP_VERSION}`;
const TIPTAP_LINK_URL = `https://esm.sh/@tiptap/extension-link@${TIPTAP_VERSION}`;
const TIPTAP_IMAGE_URL = `https://esm.sh/@tiptap/extension-image@${TIPTAP_VERSION}`;
const TIPTAP_TABLE_URL = `https://esm.sh/@tiptap/extension-table@${TIPTAP_VERSION}`;

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
  setImage(attributes: { src: string; alt: string; title?: string }): MarkdownTiptapChain;
  insertTable(attributes: { rows: number; cols: number; withHeaderRow: boolean }): MarkdownTiptapChain;
  addRowBefore(): MarkdownTiptapChain;
  addRowAfter(): MarkdownTiptapChain;
  deleteRow(): MarkdownTiptapChain;
  addColumnBefore(): MarkdownTiptapChain;
  addColumnAfter(): MarkdownTiptapChain;
  deleteColumn(): MarkdownTiptapChain;
  deleteTable(): MarkdownTiptapChain;
  run(): boolean;
};

type MarkdownTiptapEditor = {
  chain(): MarkdownTiptapChain;
  commands: {
    setContent(content: string, options?: { emitUpdate?: boolean }): boolean;
  };
  getHTML(): string;
  isActive(name: string, attributes?: Record<string, unknown>): boolean;
  destroy(): void;
};

type MarkdownFormattingState = {
  paragraph: boolean;
  heading2: boolean;
  heading3: boolean;
  bulletList: boolean;
  orderedList: boolean;
  blockquote: boolean;
  codeBlock: boolean;
  bold: boolean;
  italic: boolean;
  strike: boolean;
  code: boolean;
  link: boolean;
  table: boolean;
};

type DotNetCallback = {
  invokeMethodAsync(method: string, payload?: unknown): Promise<void>;
};

type MarkdownTiptapEditorEvent = {
  editor: MarkdownTiptapEditor;
};

type ConfigurableExtension = {
  configure(options: Record<string, unknown>): unknown;
};

type MarkdownTiptapEditorConstructor = new (
  options: Record<string, unknown>,
) => MarkdownTiptapEditor;

type EditorEntry = {
  editor: MarkdownTiptapEditor;
  callback?: DotNetCallback;
  lastFormattingState: string | null;
};

const editors = new Map<string, EditorEntry>();

async function loadTiptap(): Promise<{
  Editor: MarkdownTiptapEditorConstructor;
  StarterKit: ConfigurableExtension;
  Link: ConfigurableExtension;
  Image: ConfigurableExtension;
  TableKit: ConfigurableExtension;
}> {
  const [coreModule, starterKitModule, linkModule, imageModule, tableModule] = await Promise.all([
    import(TIPTAP_CORE_URL),
    import(TIPTAP_STARTER_KIT_URL),
    import(TIPTAP_LINK_URL),
    import(TIPTAP_IMAGE_URL),
    import(TIPTAP_TABLE_URL),
  ]);

  return {
    Editor: coreModule.Editor as MarkdownTiptapEditorConstructor,
    StarterKit: (starterKitModule.StarterKit ?? starterKitModule.default) as ConfigurableExtension,
    Link: (linkModule.Link ?? linkModule.default) as ConfigurableExtension,
    Image: (imageModule.Image ?? imageModule.default) as ConfigurableExtension,
    TableKit: (tableModule.TableKit ?? tableModule.default) as ConfigurableExtension,
  };
}

export async function initialize(
  element: HTMLElement,
  content: string,
  dotNetCallback?: DotNetCallback,
): Promise<string> {
  const { Editor, StarterKit, Link, Image, TableKit } = await loadTiptap();
  let editor: MarkdownTiptapEditor;
  let entry: EditorEntry;
  let lastFormattingState: string | null = null;

  const reportFormattingState = ({ editor: currentEditor }: MarkdownTiptapEditorEvent): void => {
    if (!dotNetCallback) {
      return;
    }

    const state = readFormattingState(currentEditor);
    const serialized = JSON.stringify(state);
    if (serialized === lastFormattingState) {
      return;
    }

    lastFormattingState = serialized;
    void dotNetCallback
      .invokeMethodAsync('OnTiptapFormattingStateChanged', state)
      .catch((error: unknown) => console.error('Aero Markdown formatting-state update failed.', error));
  };

  const reportContentChanged = (): void => {
    if (!dotNetCallback) {
      return;
    }

    void dotNetCallback
      .invokeMethodAsync('OnTiptapContentChanged')
      .catch((error: unknown) => console.error('Aero Markdown content update failed.', error));
  };

  editor = new Editor({
    element,
    content,
    autofocus: 'end',
    extensions: [
      StarterKit.configure({ link: false }),
      Link.configure({
        autolink: true,
        linkOnPaste: true,
        openOnClick: false,
        HTMLAttributes: {
          target: null,
          rel: null,
        },
      }),
      Image.configure({
        inline: true,
        allowBase64: false,
      }),
      TableKit,
    ],
    editorProps: {
      attributes: {
        class: 'aero-tiptap-markdown-prosemirror blog-article-content',
        role: 'textbox',
        'aria-multiline': 'true',
        'aria-label': 'Markdown editor',
      },
    },
    onCreate: reportFormattingState,
    onSelectionUpdate: reportFormattingState,
    onTransaction: reportFormattingState,
    onUpdate: (event: MarkdownTiptapEditorEvent): void => {
      reportFormattingState(event);
      reportContentChanged();
    },
  });

  const handle = crypto.randomUUID();
  entry = {
    editor,
    callback: dotNetCallback,
    lastFormattingState,
  };
  editors.set(handle, entry);
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
    case 'addRowBefore': return chain.addRowBefore().run();
    case 'addRowAfter': return chain.addRowAfter().run();
    case 'deleteRow': return chain.deleteRow().run();
    case 'addColumnBefore': return chain.addColumnBefore().run();
    case 'addColumnAfter': return chain.addColumnAfter().run();
    case 'deleteColumn': return chain.deleteColumn().run();
    case 'deleteTable': return chain.deleteTable().run();
    default: throw new Error(`Unknown Tiptap Markdown command '${command}'.`);
  }
}

export function insertImage(
  handle: string,
  source: string,
  alternativeText: string,
  title?: string,
): boolean {
  const src = source.trim();
  if (!src) {
    throw new Error('An image URL is required.');
  }
  if (!isSafeImageSource(src)) {
    throw new Error('Image URLs must use HTTP, HTTPS, or a site-relative path.');
  }

  const attributes: { src: string; alt: string; title?: string } = {
    src,
    alt: alternativeText.trim(),
  };
  const normalizedTitle = title?.trim();
  if (normalizedTitle) {
    attributes.title = normalizedTitle;
  }

  return requireEditor(handle).chain().focus().setImage(attributes).run();
}

export function insertTable(handle: string, rows: number, columns: number): boolean {
  if (!Number.isInteger(rows) || !Number.isInteger(columns)
      || rows < 2 || rows > 10 || columns < 1 || columns > 10) {
    throw new Error('Table dimensions must be between 2 and 10 rows and 1 and 10 columns.');
  }

  return requireEditor(handle)
    .chain()
    .focus()
    .insertTable({ rows, cols: columns, withHeaderRow: true })
    .run();
}

export function getHtml(handle: string): string {
  return normalizeMarkdownHtml(requireEditor(handle).getHTML());
}

export function setHtml(handle: string, content: string): boolean {
  const entry = requireEntry(handle);
  const updated = entry.editor.commands.setContent(content, { emitUpdate: false });
  if (entry.callback) {
    entry.lastFormattingState = null;
    const state = readFormattingState(entry.editor);
    entry.lastFormattingState = JSON.stringify(state);
    void entry.callback
      .invokeMethodAsync('OnTiptapFormattingStateChanged', state)
      .catch((error: unknown) => console.error('Aero Markdown formatting-state update failed.', error));
  }

  return updated;
}

export function dispose(handle: string): void {
  const entry = editors.get(handle);
  entry?.editor.destroy();
  editors.delete(handle);
}

function requireEditor(handle: string): MarkdownTiptapEditor {
  return requireEntry(handle).editor;
}

function requireEntry(handle: string): EditorEntry {
  const entry = editors.get(handle);
  if (!entry) {
    throw new Error('The Tiptap Markdown editor instance is not available.');
  }

  return entry;
}

function readFormattingState(editor: MarkdownTiptapEditor): MarkdownFormattingState {
  return {
    paragraph: editor.isActive('paragraph'),
    heading2: editor.isActive('heading', { level: 2 }),
    heading3: editor.isActive('heading', { level: 3 }),
    bulletList: editor.isActive('bulletList'),
    orderedList: editor.isActive('orderedList'),
    blockquote: editor.isActive('blockquote'),
    codeBlock: editor.isActive('codeBlock'),
    bold: editor.isActive('bold'),
    italic: editor.isActive('italic'),
    strike: editor.isActive('strike'),
    code: editor.isActive('code'),
    link: editor.isActive('link'),
    table: editor.isActive('table'),
  };
}

function isSafeImageSource(source: string): boolean {
  try {
    const url = new URL(source, document.baseURI);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

export function normalizeMarkdownHtml(html: string): string {
  const template = document.createElement('template');
  template.innerHTML = html;

  template.content.querySelectorAll('p').forEach((paragraph) => {
    const hasMeaningfulElement = Array.from(paragraph.children)
      .some((child) => child.tagName.toLowerCase() !== 'br');
    if (!hasMeaningfulElement && !(paragraph.textContent ?? '').trim()) {
      paragraph.remove();
    }
  });

  template.content.querySelectorAll('li, th, td').forEach((container) => {
    const directParagraphs = Array.from(container.children)
      .filter((child) => child.tagName.toLowerCase() === 'p');
    if (directParagraphs.length !== 1) {
      return;
    }

    const paragraph = directParagraphs[0];
    if (!paragraph) {
      return;
    }

    paragraph.replaceWith(...Array.from(paragraph.childNodes));
  });

  template.content.querySelectorAll('s').forEach((strike) => {
    const deletion = document.createElement('del');
    for (const attribute of Array.from(strike.attributes)) {
      deletion.setAttribute(attribute.name, attribute.value);
    }
    deletion.replaceChildren(...Array.from(strike.childNodes));
    strike.replaceWith(deletion);
  });

  // CommonMark doesn't permit boundary whitespace inside emphasis delimiters.
  // Tiptap can retain that whitespace when a user formats an existing selection,
  // so canonicalize it before the strict Markdown round-trip boundary sees it.
  Array.from(template.content.querySelectorAll('strong, em, del'))
    .reverse()
    .forEach((mark) => normalizeMarkBoundaryWhitespace(mark));

  template.content.querySelectorAll('a').forEach((link) => {
    link.removeAttribute('target');
    link.removeAttribute('rel');
  });

  template.content.querySelectorAll('table').forEach((table) => {
    table.removeAttribute('style');
    table.querySelectorAll('colgroup').forEach((colgroup) => colgroup.remove());

    const rows: HTMLTableRowElement[] = [];
    Array.from(table.children).forEach((section) => {
      const tagName = section.tagName.toLowerCase();
      if (tagName === 'tr') {
        rows.push(section as HTMLTableRowElement);
        return;
      }

      if (tagName === 'thead' || tagName === 'tbody') {
        Array.from(section.children)
          .filter((child) => child.tagName.toLowerCase() === 'tr')
          .forEach((row) => rows.push(row as HTMLTableRowElement));
      }
    });

    rows.forEach((row) => {
      row.querySelectorAll('th, td').forEach((cell) => {
        if (cell.getAttribute('colspan') === '1') {
          cell.removeAttribute('colspan');
        }
        if (cell.getAttribute('rowspan') === '1') {
          cell.removeAttribute('rowspan');
        }
        cell.removeAttribute('colwidth');
      });
    });

    const firstRow = rows[0];
    const headerCells = firstRow
      ? Array.from(firstRow.children).filter((cell) => cell.tagName.toLowerCase() === 'th')
      : [];
    const hasCanonicalHeader = firstRow
      && headerCells.length > 0
      && headerCells.length === firstRow.children.length;
    const hasCanonicalBody = rows.slice(1).every((row) =>
      Array.from(row.children).every((cell) => cell.tagName.toLowerCase() === 'td'));

    if (hasCanonicalHeader && hasCanonicalBody) {
      const head = document.createElement('thead');
      const body = document.createElement('tbody');
      head.append(firstRow);
      rows.slice(1).forEach((row) => body.append(row));
      table.replaceChildren(head, body);
    }
  });

  template.content.querySelectorAll('pre > code:only-child').forEach((code) => {
    const normalizedText = (code.textContent ?? '')
      .replace(/\r\n?/g, '\n');
    code.textContent = normalizedText.endsWith('\n')
      ? normalizedText
      : `${normalizedText}\n`;
  });

  return template.innerHTML;
}

function normalizeMarkBoundaryWhitespace(mark: Element): void {
  if (mark.attributes.length > 0) {
    return;
  }

  const text = mark.textContent ?? '';
  const hasMeaningfulDescendant = Array.from(mark.querySelectorAll('*'))
    .some((descendant) =>
      !isMarkdownInlineMark(descendant)
      || descendant.attributes.length > 0);

  if (!text && !hasMeaningfulDescendant) {
    mark.remove();
    return;
  }

  if (/^[\t\n\f\r ]+$/.test(text) && !hasMeaningfulDescendant) {
    mark.replaceWith(document.createTextNode(text));
    return;
  }

  const leadingWhitespace = readBoundaryWhitespace(mark, false);
  const trailingWhitespace = readBoundaryWhitespace(mark, true);

  if (leadingWhitespace) {
    consumeBoundaryText(mark, leadingWhitespace.length, false);
    mark.before(document.createTextNode(leadingWhitespace));
  }

  if (trailingWhitespace) {
    consumeBoundaryText(mark, trailingWhitespace.length, true);
    mark.after(document.createTextNode(trailingWhitespace));
  }
}

function readBoundaryWhitespace(root: Element, fromEnd: boolean): string {
  const childNodes = Array.from(root.childNodes);
  if (fromEnd) {
    childNodes.reverse();
  }

  let whitespace = '';
  for (const child of childNodes) {
    if (child.nodeType === Node.TEXT_NODE) {
      const text = child.textContent ?? '';
      const match = fromEnd
        ? text.match(/[\t\n\f\r ]+$/)?.[0] ?? ''
        : text.match(/^[\t\n\f\r ]+/)?.[0] ?? '';
      whitespace = fromEnd
        ? `${match}${whitespace}`
        : `${whitespace}${match}`;
      if (match.length !== text.length) {
        break;
      }
      continue;
    }

    if (child instanceof Element
      && isMarkdownInlineMark(child)
      && child.attributes.length === 0) {
      const nestedWhitespace = readBoundaryWhitespace(child, fromEnd);
      whitespace = fromEnd
        ? `${nestedWhitespace}${whitespace}`
        : `${whitespace}${nestedWhitespace}`;
      if (nestedWhitespace.length !== (child.textContent ?? '').length) {
        break;
      }
      continue;
    }

    break;
  }

  return whitespace;
}

function isMarkdownInlineMark(element: Element): boolean {
  const tagName = element.tagName.toLowerCase();
  return tagName === 'strong' || tagName === 'em' || tagName === 'del';
}

function consumeBoundaryText(
  root: Element,
  characterCount: number,
  fromEnd: boolean,
): void {
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const textNodes: Text[] = [];
  let current = walker.nextNode();
  while (current) {
    textNodes.push(current as Text);
    current = walker.nextNode();
  }

  if (fromEnd) {
    textNodes.reverse();
  }

  let remaining = characterCount;
  for (const textNode of textNodes) {
    if (remaining === 0) {
      break;
    }

    const consumed = Math.min(remaining, textNode.data.length);
    textNode.data = fromEnd
      ? textNode.data.slice(0, textNode.data.length - consumed)
      : textNode.data.slice(consumed);
    if (!textNode.data) {
      textNode.remove();
    }
    remaining -= consumed;
  }
}
