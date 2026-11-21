import fs from 'node:fs/promises';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';

// Run the actual Axios interceptors with a controlled adapter; no browser or network is needed.
const requireFrontend = createRequire(new URL('../../Frontend/package.json', import.meta.url));
const ts = requireFrontend('typescript');
const axiosUrl = new URL('../../Frontend/node_modules/axios/index.js', import.meta.url).href;
const { AxiosError } = await import(axiosUrl);
const entries = new Map();
globalThis.localStorage = { getItem: key => entries.get(key) ?? null, removeItem: key => entries.delete(key) };
globalThis.window = { location: { href: 'http://localhost/admin', pathname: '/' } };
const source = (await fs.readFile(new URL('../../Frontend/src/api/client.ts', import.meta.url), 'utf8'))
  .replace("from 'axios'", 'from ' + JSON.stringify(axiosUrl))
  .replace('import.meta.env.VITE_API_BASE_URL', 'undefined');
const js = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } }).outputText;
const { apiClient } = await import('data:text/javascript;base64,' + Buffer.from(js).toString('base64'));
function failure(config) {
  return new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, null, { status: 401, data: {}, headers: {}, config, statusText: 'Unauthorized' });
}
entries.set('pickleverse_token', 'device-A'); entries.set('pickleverse_user', 'A');
let rejectOld;
const old = apiClient.get('/old', { adapter: config => new Promise((resolve, reject) => {
  assert.equal(config.headers.Authorization, 'Bearer device-A');
  rejectOld = () => reject(failure(config));
}) });
await new Promise(resolve => setImmediate(resolve));
entries.set('pickleverse_token', 'device-B'); entries.set('pickleverse_user', 'B');
rejectOld(); await old.catch(() => {});
assert.equal(entries.get('pickleverse_token'), 'device-B');
assert.equal(entries.get('pickleverse_user'), 'B');
assert.equal(window.location.href, 'http://localhost/admin');
console.log('PASS: a late 401 from device A preserves the newer web session B.');
await apiClient.get('/current', { adapter: config => {
  assert.equal(config.headers.Authorization, 'Bearer device-B');
  return Promise.reject(failure(config));
} }).catch(() => {});
assert.equal(entries.size, 0); assert.equal(window.location.href, '/login');
console.log('PASS: a 401 for the current web token removes credentials and redirects to login.');
