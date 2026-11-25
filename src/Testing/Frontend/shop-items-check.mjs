import fs from 'node:fs/promises';
import { createRequire } from 'node:module';
import assert from 'node:assert/strict';

// Rules of the shop item form; types only are imported, so the module runs without a bundler or browser.
const requireFrontend = createRequire(new URL('../../Frontend/package.json', import.meta.url));
const ts = requireFrontend('typescript');
const source = await fs.readFile(new URL('../../Frontend/src/features/content/pages/shopItems.ts', import.meta.url), 'utf8');
const js = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext } }).outputText;
const { MAX_DESCRIPTION, MAX_PRICE, MAX_TITLE, draftOf, priceLabel, problemOf, requestOf } =
  await import('data:text/javascript;base64,' + Buffer.from(js).toString('base64'));

const skin = { code: 'paddle.dragonruby', title: 'DRAGON RUBY', description: 'Rồng phương Đông.', collection: 'BỘ SƯU TẬP TINH ANH', premium: true };
const draft = draftOf(skin);
assert.deepEqual(draft, { ...skin, tokenpadPrice: 0, elitePrice: 0 });
assert.equal(draftOf().code, '');
assert.ok(problemOf(draft, true).includes('giá'));
assert.equal(problemOf({ ...draft, elitePrice: 800 }, true), null);
assert.equal(problemOf({ ...draft, tokenpadPrice: 900 }, true), null);
assert.equal(problemOf(draft, false), null);
console.log('PASS: choosing a skin fills the form; publishing needs a price, stopping a sale does not.');

const priced = { ...draft, elitePrice: 800 };
for (const [change, word] of [[{ code: '' }, 'vợt'], [{ title: ' ' }, 'Tên'], [{ title: 'a'.repeat(MAX_TITLE + 1) }, 'Tên'], [{ collection: '' }, 'Bộ sưu tập'],
  [{ description: '' }, 'Mô tả'], [{ description: 'a'.repeat(MAX_DESCRIPTION + 1) }, 'Mô tả'], [{ elitePrice: -1 }, 'Giá'], [{ elitePrice: 1.5 }, 'Giá'],
  [{ elitePrice: NaN }, 'Giá'], [{ tokenpadPrice: MAX_PRICE + 1 }, 'Giá']])
  assert.ok(problemOf({ ...priced, ...change }, true)?.includes(word), JSON.stringify(change));
console.log('PASS: the form rejects the values the server rejects.');

assert.equal(priceLabel({ tokenpadPrice: 900, elitePrice: 250 }), '900 TOKENPAD / 250 ELITE TOKEN');
assert.equal(priceLabel({ tokenpadPrice: 0, elitePrice: 800 }), '800 ELITE TOKEN');
assert.equal(priceLabel({ tokenpadPrice: 900, elitePrice: 0 }), '900 TOKENPAD');
assert.equal(priceLabel({ tokenpadPrice: 0, elitePrice: 0, state: 'Stopped' }), 'Không bán');
assert.equal(priceLabel({ tokenpadPrice: 0, elitePrice: 0, state: 'Default' }), 'Miễn phí');
const item = { cosmeticId: 4, ...priced, releaseOrder: 3, owners: 1, state: 'OnSale', issue: null, canEdit: true, canDelete: false };
assert.deepEqual(requestOf(item), priced);
console.log('PASS: the list shows the prices players pay and editing keeps every field.');
