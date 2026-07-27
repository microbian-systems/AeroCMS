import { access, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const docsRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = path.resolve(docsRoot, '..');
const manifest = JSON.parse(
  await readFile(path.join(docsRoot, 'documentation-manifest.json'), 'utf8'),
);
const errors = [];
const canonical = new Set();
const forbiddenRoots = /^(Aero|AeroDB|AeroVault|NeoUI|craft\.js|hyperui|tiptap-dotnet|ui)(\/|$)/i;

for (const entry of manifest.entries) {
  if (canonical.has(entry.canonical_path)) {
    errors.push(`Duplicate canonical path: ${entry.canonical_path}`);
  }
  canonical.add(entry.canonical_path);
}

for (const entry of manifest.entries) {
  const documentPath = path.join(docsRoot, 'src', 'content', 'docs', entry.document);
  try {
    await access(documentPath);
  } catch {
    errors.push(`Missing document: ${entry.document}`);
    continue;
  }

  for (const sourceFile of entry.source_files) {
    if (forbiddenRoots.test(sourceFile.replaceAll('\\', '/'))) {
      errors.push(`Submodule provenance is forbidden: ${sourceFile}`);
      continue;
    }
    try {
      await access(path.join(repoRoot, sourceFile));
    } catch {
      errors.push(`Missing provenance source: ${sourceFile}`);
    }
  }

  const markdown = await readFile(documentPath, 'utf8');
  if (!markdown.startsWith('---')) {
    errors.push(`Missing frontmatter: ${entry.document}`);
  }
  if (!markdown.includes(`title: ${entry.title}`) && !markdown.includes(`title: "${entry.title}"`)) {
    errors.push(`Manifest title does not match frontmatter: ${entry.document}`);
  }

  for (const match of markdown.matchAll(/\]\((\/[^)#?]+)(?:[)#?][^)]*)?\)/g)) {
    const target = match[1].replace(/\/$/, '') || '/';
    if (target.startsWith('/api/')) continue;
    if (!canonical.has(target)) {
      errors.push(`Broken canonical link in ${entry.document}: ${match[1]}`);
    }
  }
}

const requiredAreas = [
  'getting-started',
  'architecture',
  'tenancy-security',
  'pages-rendering',
  'content-modeling',
  'identity-access',
  'ai-mcp',
  'operations',
  'security',
  'troubleshooting',
];
const areas = new Set(manifest.entries.map((entry) => entry.feature_area));
for (const area of requiredAreas) {
  if (!areas.has(area)) errors.push(`Required feature area is not represented: ${area}`);
}

if (manifest.last_verified_commit !== '35ec154fb3b57e838d4fe6211f9d9f193e53d812') {
  errors.push('Manifest baseline commit does not match this documentation release.');
}

if (errors.length > 0) {
  console.error(errors.join('\n'));
  process.exitCode = 1;
} else {
  console.log(`Validated ${manifest.entries.length} pages and ${canonical.size} canonical paths.`);
}
