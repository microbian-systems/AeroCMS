import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const docsRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const manifestPath = path.join(docsRoot, 'documentation-manifest.json');
const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));

function normalizeMarkdown(source) {
  return source
    .replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, '')
    .replace(/^import\s+.+?;\r?\n/gm, '')
    .replace(/<Mermaid[\s\S]*?\/>/g, '[Architecture diagram: see the canonical web page.]')
    .replace(/<span[^>]*>(.*?)<\/span>/g, '$1')
    .replace(/\r\n/g, '\n')
    .trim();
}

const publicEntries = manifest.entries.filter((entry) => entry.audience === 'public');
const mapLines = [
  '# AeroCMS documentation',
  '',
  `> Verified against commit ${manifest.last_verified_commit}.`,
  '> Audience: public. Security-sensitive operational values and design-history material are excluded.',
  '',
];
const corpus = [
  '# AeroCMS normalized documentation corpus',
  '',
  `Baseline commit: ${manifest.last_verified_commit}`,
  'Trust classification: public documentation',
  'Excluded: Git submodules, generated artifacts, design history, credentials, connection strings, customer data, and PII.',
  '',
];

for (const entry of publicEntries) {
  const sourcePath = path.join(docsRoot, 'src', 'content', 'docs', entry.document);
  const source = normalizeMarkdown(await readFile(sourcePath, 'utf8'));
  mapLines.push(`- [${entry.title}](${entry.canonical_path}) — ${entry.feature_area}; maturity: ${entry.maturity}.`);
  corpus.push(
    `## ${entry.title}`,
    '',
    `Canonical path: ${entry.canonical_path}`,
    `Feature area: ${entry.feature_area}`,
    `Maturity: ${entry.maturity}`,
    `Source provenance: ${entry.source_files.join(', ')}`,
    '',
    source,
    '',
  );
}

const llms = `${mapLines.join('\n')}\n`;
const full = `${corpus.join('\n').trimEnd()}\n`;
const publicDir = path.join(docsRoot, 'public');
await mkdir(publicDir, { recursive: true });
await writeFile(path.join(docsRoot, 'llms.txt'), llms);
await writeFile(path.join(docsRoot, 'llms-aero-full.txt'), full);
await writeFile(path.join(publicDir, 'llms.txt'), llms);
await writeFile(path.join(publicDir, 'llms-aero-full.txt'), full);
await writeFile(
  path.join(publicDir, 'documentation-manifest.json'),
  `${JSON.stringify(manifest, null, 2)}\n`,
);

console.log(`Generated ${publicEntries.length} public documentation entries.`);
