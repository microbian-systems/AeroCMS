import { readdir, readFile } from 'node:fs/promises';
import { extname, join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../dist/', import.meta.url));

async function walk(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const path = join(directory, entry.name);
    files.push(...(entry.isDirectory() ? await walk(path) : [path]));
  }

  return files;
}

const files = await walk(root);
const normalizedFiles = new Set(
  files.map((file) => `/${relative(root, file).split(sep).join('/')}`),
);
const htmlFiles = files.filter((file) => extname(file).toLowerCase() === '.html');
const idsByFile = new Map();

for (const file of htmlFiles) {
  const html = await readFile(file, 'utf8');
  idsByFile.set(
    `/${relative(root, file).split(sep).join('/')}`,
    new Set([...html.matchAll(/\s(?:id|name)=["']([^"']+)["']/gi)].map((match) => match[1])),
  );
}

function resolveTarget(pathname) {
  const decoded = decodeURIComponent(pathname);
  const candidates = [decoded];

  if (decoded.endsWith('/')) {
    candidates.push(`${decoded}index.html`);
  } else if (!extname(decoded)) {
    candidates.push(`${decoded}/index.html`, `${decoded}.html`);
  }

  return candidates.find((candidate) => normalizedFiles.has(candidate));
}

const failures = [];

for (const file of htmlFiles) {
  const source = `/${relative(root, file).split(sep).join('/')}`;
  const html = await readFile(file, 'utf8');

  for (const match of html.matchAll(/\shref=["']([^"']+)["']/gi)) {
    const href = match[1];
    if (
      href === '' ||
      href.startsWith('//') ||
      /^[a-z][a-z0-9+.-]*:/i.test(href)
    ) {
      continue;
    }

    const url = new URL(href, `https://docs.getaerocms.net${source}`);
    if (url.hash === '#top' && url.pathname === new URL(`https://docs.getaerocms.net${source}`).pathname) {
      continue;
    }

    const target = resolveTarget(url.pathname);
    if (!target) {
      failures.push(`${source} -> ${href} (target missing)`);
      continue;
    }

    if (url.hash) {
      const id = decodeURIComponent(url.hash.slice(1));
      if (id && !idsByFile.get(target)?.has(id)) {
        failures.push(`${source} -> ${href} (anchor missing)`);
      }
    }
  }
}

if (failures.length > 0) {
  console.error(`Found ${failures.length} broken internal links:`);
  const uniqueFailures = [...new Set(failures)];
  for (const failure of uniqueFailures.slice(0, 100)) {
    console.error(`- ${failure}`);
  }
  process.exitCode = 1;
} else {
  console.log(`Validated internal links and anchors across ${htmlFiles.length} HTML files.`);
}
