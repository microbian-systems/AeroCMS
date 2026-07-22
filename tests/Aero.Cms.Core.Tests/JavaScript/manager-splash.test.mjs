import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const scriptPath = process.argv[2];
assert.ok(scriptPath, 'The manager splash script path is required.');

const classes = new Set();
const splash = {
    classList: {
        add(value) { classes.add(value); },
        remove(value) { classes.delete(value); }
    },
    parentNode: { removeChild() { } },
    addEventListener() { }
};
const title = { textContent: '' };
const detail = { textContent: '' };
const status = { setAttribute() { } };
const retry = { hidden: true, focus() { } };

globalThis.window = globalThis;
globalThis.document = {
    getElementById(id) { return id === 'app-splash' ? splash : null; },
    querySelector(selector) {
        if (selector.endsWith('.app-splash-title')) return title;
        if (selector.endsWith('.app-splash-text')) return detail;
        if (selector.endsWith('.app-splash-status')) return status;
        if (selector.endsWith('.app-splash-retry')) return retry;
        return null;
    }
};

let starts = 0;
globalThis.Blazor = {
    start() {
        starts += 1;
        return Promise.resolve();
    }
};

vm.runInThisContext(fs.readFileSync(scriptPath, 'utf8'), { filename: scriptPath });
await new Promise(resolve => setTimeout(resolve, 25));

assert.equal(typeof globalThis.startAeroApp, 'function');
assert.equal(starts, 1, 'The splash asset must start Blazor after defining its bootstrap function.');

await globalThis.startAeroApp();
assert.equal(starts, 1, 'Repeated script/navigation startup must reuse the existing Blazor start promise.');
assert.equal(classes.has('failed'), false);

process.exit(0);
