import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const scriptPath = process.argv[2];
assert.ok(scriptPath, 'The setup handoff script path is required.');

const attributes = new Map();
const createElement = () => ({
    hidden: true,
    inert: false,
    textContent: '',
    addEventListener() { },
    focus() { },
    setAttribute(name, value) { attributes.set(name, value); },
    removeAttribute(name) { attributes.delete(name); }
});

const elementIds = [
    'setup-handoff',
    'setup-interactive-content',
    'setup-handoff-status',
    'setup-handoff-title',
    'setup-handoff-detail',
    'setup-handoff-error',
    'setup-handoff-actions',
    'setup-handoff-check',
    'setup-handoff-return'
];
const elements = new Map(elementIds.map(id => [id, createElement()]));
const storage = new Map();
const requests = [];

globalThis.window = globalThis;
globalThis.window.location = {
    origin: 'https://localhost:333',
    replace() { assert.fail('The homepage must not open before runtime readiness.'); }
};
globalThis.document = {
    body: { style: {} },
    getElementById(id) { return elements.get(id) ?? null; },
    addEventListener() { }
};
globalThis.sessionStorage = {
    getItem(key) { return storage.get(key) ?? null; },
    setItem(key, value) { storage.set(key, String(value)); },
    removeItem(key) { storage.delete(key); }
};
globalThis.fetch = async url => {
    requests.push(String(url));
    return {
        ok: true,
        status: 200,
        url: 'https://localhost:333/setup/status',
        async json() {
            return { state: 'Configured', setupComplete: false, seedComplete: false };
        }
    };
};

const unhandled = [];
process.on('unhandledRejection', error => unhandled.push(error));

vm.runInThisContext(fs.readFileSync(scriptPath, 'utf8'), { filename: scriptPath });

globalThis.AeroSetupHandoff.begin();
await new Promise(resolve => setTimeout(resolve, 25));
assert.equal(storage.get('aero-setup-handoff'), 'pending');
assert.ok(requests.includes('/setup/status'), 'begin() must immediately poll setup status.');

const requestCount = requests.length;
globalThis.AeroSetupHandoff.resumeIfPending();
await new Promise(resolve => setTimeout(resolve, 25));
assert.ok(requests.length > requestCount, 'A pending handoff must resume polling after reload.');
assert.equal(unhandled.length, 0, 'Restarting a pending poll must not leak an abort rejection.');

process.exit(0);
