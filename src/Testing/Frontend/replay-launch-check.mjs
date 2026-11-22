import fs from 'node:fs/promises';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';

const requireFrontend = createRequire(new URL('../../Frontend/package.json', import.meta.url));
const ts = requireFrontend('typescript');
const match = '879f2b5b-d5eb-4c7e-9790-20a3ee98ee79', id = match.replaceAll('-', ''), code = 'a'.repeat(64);
const fixture = () => ({ matchId: match, url: `pickleverse://replay/${id}?code=${code}`, expiresAt: new Date(Date.now() + 120000).toISOString() });
let request;
globalThis.replayLaunchPost = async (...args) => { request = args; return { data: fixture() }; };
const source = (await fs.readFile(new URL('../../Frontend/src/api/replayLaunchApi.ts', import.meta.url), 'utf8'))
  .replace("import { apiClient } from './client';", 'const apiClient = { post: globalThis.replayLaunchPost };');
const js = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } }).outputText;
const { validateReplayLaunch, createReplayLaunch } = await import('data:text/javascript;base64,' + Buffer.from(js).toString('base64'));
assert.equal(validateReplayLaunch(fixture(), match).matchId, match);
const invalid = [
  { url: 'javascript:alert(1)' }, { url: `https://replay/${id}?code=${code}` },
  { url: `pickleverse://replay.evil/${id}?code=${code}` }, { url: `pickleverse://user@replay/${id}?code=${code}` },
  { url: `pickleverse://replay:80/${id}?code=${code}` }, { url: `pickleverse://replay/${id}?code=${code}#extra` },
  { url: `pickleverse://replay/${id}?code=${code}&code=${code}` }, { url: `pickleverse://replay/${id}?code=${code}&api=https://other.invalid` },
  { url: `pickleverse://replay/${id}?code=short` }, { matchId: '00000000-0000-0000-0000-000000000001' },
  { url: `pickleverse://replay/foo/../${id}?code=${code}` }, { url: `pickleverse://replay/${id}?code=%61${code.slice(1)}` },
  { expiresAt: 'invalid' }, { expiresAt: new Date(Date.now() - 1000).toISOString() },
];
for (const fields of invalid) assert.throws(() => validateReplayLaunch({ ...fixture(), ...fields }, match));
const controller = new AbortController();
await createReplayLaunch(match, controller.signal);
assert.equal(request[0], `/api/replay-launch/${match}`); assert.equal(request[1], undefined); assert.equal(request[2].signal, controller.signal);
console.log(`SUCCESS: ${invalid.length + 2} replay-launch URL and API checks.`);
