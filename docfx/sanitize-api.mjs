import { readdir, readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const apiDirectory = fileURLToPath(new URL('./api/', import.meta.url));
const files = (await readdir(apiDirectory)).filter((name) => name.endsWith('.yml'));
let removedEntries = 0;

for (const file of files) {
  const path = join(apiDirectory, file);
  const lines = (await readFile(path, 'utf8')).split(/\r?\n/);
  const output = [];
  let skippingSyntheticClone = false;

  for (const line of lines) {
    if (line.startsWith('- uid:')) {
      if (line.includes('{Clone}$')) {
        skippingSyntheticClone = true;
        removedEntries += 1;
        continue;
      }

      skippingSyntheticClone = false;
    }

    if (skippingSyntheticClone) {
      continue;
    }

    if (/^\s+- .*{Clone}\$$/.test(line)) {
      removedEntries += 1;
      continue;
    }

    output.push(line);
  }

  await writeFile(path, output.join('\n'), 'utf8');
}

console.log(`Removed ${removedEntries} synthetic record-clone entries from DocFX metadata.`);
