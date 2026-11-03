import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { build } from 'esbuild';
import { authoredReviewPort } from './authored-review-port.mjs';

const sourcePath = new URL('./upstream/Learn/lesson-guide.mjs', import.meta.url);
const source = await readFile(sourcePath, 'utf8');
// Compile the actual pinned module plus the guarded adaptation in memory. This
// exercises the same imported helper and rendering function as authored builds.
const bundle = await build({ stdin: { contents: authoredReviewPort(source), resolveDir: fileURLToPath(new URL('./upstream/Learn/', import.meta.url)), sourcefile: 'lesson-guide.mjs' }, bundle: true, write: false, format: 'esm', platform: 'node' });
const { lessonGuideHTML } = await import('data:text/javascript;base64,' + Buffer.from(bundle.outputFiles[0].contents).toString('base64'));
const esc = value => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;');
const render = lesson => lessonGuideHTML({ summary: 'Summary', steps: 'Teaching steps', ...lesson }, [], esc, url => /^https:\/\//.test(url) ? url : '');

test('authored human review and legacy AI-assisted status reflect the actual record', () => {
  const html = render({ sources: [{ title: 'Reference', url: 'https://example.org/', scope: 'Methods' }], review: { status: 'Reviewed by course author', reviewer: 'A. Tutor', date: '2026-11-03', authorship: 'Original fictional teaching example.' } });
  assert.match(html, /Review status: Reviewed by course author/);
  assert.match(html, /Reviewer: A\. Tutor/);
  assert.match(html, /Review date: 2026-11-03/);
  assert.match(html, /Original fictional teaching example\./);
  assert.doesNotMatch(html, /AI-assisted editorial review|Original StatsDirect teaching material/);
  const legacy = render({ review: { status: 'AI-assisted editorial and numerical checks', authorship: 'Original StatsDirect teaching material with fictional examples; not official examination content.', date: '2026-10-01' } });
  assert.match(legacy, /Review status: AI-assisted editorial and numerical checks/);
  assert.match(legacy, /not official examination content/);
});

test('draft status remains visible without references, while absent metadata adds no review claim', () => {
  const draft = render({ review: { status: 'Draft — author review required', date: '2026-11-03' } });
  assert.match(draft, /Review status: Draft — author review required/);
  assert.doesNotMatch(draft, /AI-assisted|Original StatsDirect|Reviewer:/);
  assert.equal(render({}), '<p>Summary</p><p>Teaching steps</p>');
});

test('review fields are escaped and template drift fails before bundling', () => {
  const html = render({ review: { status: '<script>bad()</script>', date: '<date>', reviewer: 'A & B', authorship: '<img src=x onerror=bad()>' } });
  assert.doesNotMatch(html, /<script|<img/);
  assert.match(html, /&lt;script&gt;bad\(\)&lt;\/script&gt;/);
  assert.match(html, /&lt;img src=x onerror=bad\(\)&gt;/);
  assert.match(html, /Reviewer: A &amp; B/);
  assert.match(html, /Review date: &lt;date&gt;/);
  assert.throws(() => authoredReviewPort(source.replace('AI-assisted editorial review', 'Different review wording')), /upstream lesson-guide\.mjs has changed/);
  assert.throws(() => authoredReviewPort(source + source), /upstream lesson-guide\.mjs has changed/);
});
