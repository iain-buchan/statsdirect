// Execute the real Learning build in a disposable repository. Never build or
// replace the developer checkout's application assets from these checks.
import test, { before, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import http from 'node:http';
import { spawn } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createRequire } from 'node:module';
import { authoredOutputs, resolveAuthoredTutor } from './authored-tutor.mjs';

const originalLearning = fileURLToPath(new URL('./', import.meta.url));
const originalRepo = path.dirname(originalLearning.replace(/[\\/]$/, ''));
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
let root, learning, authored, content, baseline, manifest, initialOutputs;
const runs = [];
const evidence = process.env.STATSDIRECT_AUTHORED_TEST_EVIDENCE;
const write = async (relative, bytes) => { const full = path.join(root, relative); await fs.mkdir(path.dirname(full), { recursive: true }); await fs.writeFile(full, bytes); };
const outputs = () => Promise.all(authoredOutputs.map(file => fs.readFile(path.join(root, file))));
async function run(args = ['--authored-if-needed'], preload) {
  const started = Date.now();
  const result = await new Promise((resolve, reject) => {
    const child = spawn(process.execPath, [...(preload ? ['--import', pathToFileURL(preload).href] : []), path.join(learning, 'build.mjs'), ...args], { cwd: root, windowsHide: true, env: { ...process.env, STATSDIRECT_LEARNING_UPSTREAM_NO_GIT: '1' } });
    let stdout = '', stderr = '';
    child.stdout.on('data', bytes => stdout += bytes); child.stderr.on('data', bytes => stderr += bytes);
    child.on('error', reject); child.on('close', code => resolve({ code, stdout, stderr }));
  });
  runs.push({ args, preload: preload ? path.basename(preload) : null, elapsedMs: Date.now() - started, ...result });
  return result;
}
async function publish() {
  const texts = Object.fromEntries(Object.entries(content).map(([file, value]) => [file, JSON.stringify(value, null, 2) + '\n']));
  texts['bank.mjs'] = '// Isolated authored fixture, never published.\nexport const bankVersion = ' + JSON.stringify(manifest.bankVersion) + ';\nexport const tracks = ' + JSON.stringify(content['tracks.json'], null, 2) + ';\nexport const questions = ' + JSON.stringify(content['questions.json'], null, 2) + ';\n';
  manifest.files = Object.entries(texts).map(([file, value]) => ({ path: file, bytes: Buffer.byteLength(value), sha256: sha(value) }));
  manifest.lessonCount = content['lessons.json'].length; manifest.questionCount = content['questions.json'].length; manifest.trackCount = Object.keys(content['tracks.json']).length;
  for (const [file, value] of Object.entries(texts)) await fs.writeFile(path.join(authored, file), value);
  await fs.writeFile(path.join(authored, 'manifest.json'), JSON.stringify(manifest, null, 2));
}
async function rejectedUnchanged(pattern, action) {
  const prior = await outputs();
  await action();
  const result = await run([]);
  assert.notEqual(result.code, 0, result.stdout + result.stderr);
  assert.match(result.stdout + result.stderr, pattern);
  assert.deepEqual(await outputs(), prior, 'A rejected build must preserve all three prior outputs.');
}
async function mutateDuringFinalCheck(mode, dependency) {
  const preload = path.join(root, 'mutation-' + mode + '.mjs');
  const change = mode === 'marker' ? "await fs.writeFile(path.resolve('Learning/.AuthoredTutor.publishing'),'test-only publication in progress');" : mode === 'dependency' ? `await fs.appendFile(${JSON.stringify(dependency)},'\\nglobalThis.__qaDependencyVersion="second";\\n');` : "await fs.appendFile(path.resolve('Learning/conversation.mjs'),'\\n// concurrent isolated author edit\\n');";
  await fs.writeFile(preload, `import fs from 'node:fs/promises';\nimport {syncBuiltinESMExports} from 'node:module';\nimport path from 'node:path';\nconst read=fs.readFile; let seen=0; fs.readFile=async function(file,...args){\nif(String(file).replaceAll('\\\\','/').endsWith('/AuthoredTutor/manifest.json')&&++seen===4){\n${change}\n}\nreturn read.call(this,file,...args);}; syncBuiltinESMExports();\n`);
  return preload;
}

before(async () => {
  root = await fs.mkdtemp(path.join(os.tmpdir(), 'StatsDirect-authored-build-')); learning = path.join(root, 'Learning'); authored = path.join(learning, 'AuthoredTutor');
  await fs.cp(originalLearning, learning, { recursive: true, dereference: true, filter: file => { const parts = path.relative(originalLearning, file).split(path.sep); return !parts.some(part => ['node_modules', 'obj', 'AuthoredTutor'].includes(part)) && !(parts[0] === 'Shared' && parts.length > 1 && parts[1] !== 'Learning'); } });
  await fs.mkdir(authored, { recursive: true });
  // Flatten these existing, pinned dependencies into the disposable repository;
  // no junction points back to the developer checkout and no network is used.
  for (const name of ['esbuild', 'dompurify', 'highlight.js', 'katex', 'marked']) await fs.cp(path.join(originalLearning, 'node_modules', name), path.join(learning, 'node_modules', name), { recursive: true, dereference: true });
  await fs.cp(path.join(originalLearning, 'node_modules/.pnpm/@esbuild+win32-x64@0.25.10/node_modules/@esbuild/win32-x64'), path.join(learning, 'node_modules/@esbuild/win32-x64'), { recursive: true, dereference: true });
  await fs.cp(path.join(originalRepo, 'StatsDirectUI/Assets/Learn'), path.join(root, 'StatsDirectUI/Assets/Learn'), { recursive: true, dereference: true });
  await fs.cp(path.join(originalRepo, 'StatsDirectUI/Assets/Brand'), path.join(root, 'StatsDirectUI/Assets/Brand'), { recursive: true, dereference: true });
  initialOutputs = authoredOutputs.map(file => Buffer.from('prior verified isolated asset: ' + file + '\n'));
  for (const [index, file] of authoredOutputs.entries()) await write(file, initialOutputs[index]);
  content = {};
  for (const file of ['lessons.json', 'questions.json', 'tracks.json']) content[file] = JSON.parse(await fs.readFile(path.join(originalLearning, 'AuthoredTutor', file), 'utf8'));
  baseline = structuredClone(content);
  const review = { status: 'Reviewed by course author', date: '2026-11-03', reviewer: 'Isolated QA author', notes: 'Fixture-only review, never published.', authorship: 'Original synthetic test material, not official examination content.' };
  content['lessons.json'][0] = { ...content['lessons.json'][0], title: 'Authored <clinical> & methods lesson', summary: 'Changed main tutor content: 12 / 60 = 20%.', review: structuredClone(review) };
  content['tracks.json'].qaHealth = { title: 'Isolated health & methods pathway' };
  content['questions.json'].unshift({ id: 'QA-NEW-01', version: 1, topic: 'Isolated denominators', objective: 'Use the correct denominator.', track: 'qaHealth', stem: 'In this fictional test fixture, what is 12 / 60?', options: [{ id: 'A', text: '20%', feedback: 'Correct denominator: 12 / 60 = 20%.' }, { id: 'B', text: '500%', feedback: 'This reverses the denominator: 60 / 12.' }], correct: 'A', explanation: 'Use 60 as the denominator; this is a proportion, not a rate.', hint: 'Count the group to which the numerator belongs.', lesson: 'Describe the numerator and denominator first.', help: '', review: structuredClone(review) });
  manifest = { schemaVersion: 1, format: 'statsdirect-builtin-tutor', sourceCourseSha256: 'a'.repeat(64), bankVersion: 'isolated-authored-build-1', reviewGatePassed: true, unreviewedChangedItems: [], reviewedChangedItems: [{ kind: 'lessons', id: content['lessons.json'][0].id, reviewer: review.reviewer, date: review.date, notes: review.notes }, { kind: 'questions', id: 'QA-NEW-01', reviewer: review.reviewer, date: review.date, notes: review.notes }] };
  await publish();
});
after(async () => {
  if (evidence) { await fs.mkdir(evidence, { recursive: true }); await fs.writeFile(path.join(evidence, 'build-runs.json'), JSON.stringify({ fixtureRoot: root, runs }, null, 2)); }
  if (root) { assert.equal(path.dirname(path.resolve(root)), path.resolve(os.tmpdir())); assert.ok(path.basename(root).startsWith('StatsDirect-authored-build-')); await fs.rm(root, { recursive: true, force: true }); }
});

test('real authored build compiles changed lessons, a new question and its pathway without double counting', async () => {
  const result = await run(); assert.equal(result.code, 0, result.stderr); assert.match(result.stdout, /12 lessons, 36 questions/);
  const nativeLessons = JSON.parse(await fs.readFile(path.join(root, authoredOutputs[2]), 'utf8'));
  assert.equal(nativeLessons[0].title, content['lessons.json'][0].title); assert.equal(nativeLessons.length, 12);
  const code = await fs.readFile(path.join(root, authoredOutputs[0]), 'utf8');
  assert.ok(code.includes('QA-NEW-01')); assert.ok(code.includes('qaHealth')); assert.ok(code.includes('Changed main tutor content'));
  const bank = await resolveAuthoredTutor(root);
  assert.equal(bank.manifest.questionCount, baseline['questions.json'].length + 1);
  assert.equal(new Set(content['questions.json'].map(q => q.id)).size, 36);
  for (const question of baseline['questions.json']) assert.equal(content['questions.json'].filter(q => q.id === question.id).length, 1);
  assert.notDeepEqual(await outputs(), initialOutputs);
});

test('unchanged incremental build skips, while each corrupted output is regenerated', async () => {
  let result = await run(); assert.equal(result.code, 0, result.stderr); assert.match(result.stdout, /assets are current/);
  for (const file of authoredOutputs) { const expected = await fs.readFile(path.join(root, file)); await write(file, 'corrupt isolated output'); result = await run(); assert.equal(result.code, 0, result.stderr); assert.match(result.stdout, /Built authored/); assert.deepEqual(await fs.readFile(path.join(root, file)), expected); }
});

test('consumed dependency changes invalidate the receipt and cannot be silently reused', async () => {
  const receipt = JSON.parse(await fs.readFile(path.join(learning, 'obj/authored-tutor-build.json'), 'utf8'));
  const dependency = receipt.dependencies.find(row => /node_modules\/dompurify\/.*\.(mjs|js)$/.test(row.path)); assert.ok(dependency, 'Actual esbuild metafile must record the sanitizer input.');
  const file = path.join(root, dependency.path), original = await fs.readFile(file);
  await fs.appendFile(file, '\n// isolated dependency edit\n');
  let result = await run(); assert.equal(result.code, 0, result.stderr); assert.match(result.stdout, /Built authored/);
  result = await run(); assert.equal(result.code, 0, result.stderr); assert.match(result.stdout, /assets are current/);
  await fs.writeFile(file, original); result = await run(); assert.equal(result.code, 0, result.stderr); assert.match(result.stdout, /Built authored/);
});

test('a syntax failure in the second bundle preserves all three prior outputs', async () => {
  const file = path.join(learning, 'conversation.mjs'), original = await fs.readFile(file);
  try { await rejectedUnchanged(/ERROR|Unexpected|Expected/, () => fs.appendFile(file, '\nconst broken = ;\n')); }
  finally { await fs.writeFile(file, original); }
});

test('bad hashes, executable bank suffixes and mismatched reviews fail before any asset replacement', async () => {
  try {
    await rejectedUnchanged(/hash\/size mismatch/, () => fs.appendFile(path.join(authored, 'questions.json'), ' ')); await publish();
    const bankFile = path.join(authored, 'bank.mjs'), bytes = Buffer.concat([await fs.readFile(bankFile), Buffer.from('\nthrow Error("must never execute");\n')]);
    await rejectedUnchanged(/executable or unexpected content/, async () => { await fs.writeFile(bankFile, bytes); const row = manifest.files.find(row => row.path === 'bank.mjs'); row.bytes = bytes.length; row.sha256 = sha(bytes); await fs.writeFile(path.join(authored, 'manifest.json'), JSON.stringify(manifest)); }); await publish();
    await rejectedUnchanged(/does not match its stated author review/, async () => { manifest.reviewedChangedItems[0].reviewer = 'Someone else'; await publish(); });
  } finally { manifest.reviewedChangedItems[0].reviewer = content['lessons.json'][0].review.reviewer; await publish(); }
});

test('a missing answer feedback or restore-required field is rejected before it can break practice', async () => {
  const question = content['questions.json'][0], original = structuredClone(question);
  try {
    await rejectedUnchanged(/feedback/, async () => { delete question.options[0].feedback; await publish(); });
    content['questions.json'][0] = structuredClone(original);
    await rejectedUnchanged(/saved-record compatibility/, async () => { delete content['questions.json'][0].hint; await publish(); });
  } finally { content['questions.json'][0] = original; await publish(); }
});

test('unknown/deleted question links and removal of a saved pathway cannot ship', async () => {
  const lesson = content['lessons.json'][0], original = structuredClone(lesson), track = content['tracks.json'].foundation;
  try {
    await rejectedUnchanged(/unknown question/, async () => { lesson.assessmentIds = ['QA-DELETED-01']; await publish(); });
    content['lessons.json'][0] = original;
    await rejectedUnchanged(/retain all original pathway IDs/, async () => { delete content['tracks.json'].foundation; await publish(); });
  } finally { content['lessons.json'][0] = original; content['tracks.json'].foundation = track; await publish(); }
});

test('publication markers and source edits arriving after bundling preserve all prior outputs', async () => {
  const port = path.join(learning, 'conversation.mjs'), original = await fs.readFile(port);
  for (const mode of ['marker', 'source']) {
    const prior = await outputs(), preload = await mutateDuringFinalCheck(mode);
    try { const result = await run([], preload); assert.notEqual(result.code, 0, result.stdout + result.stderr); assert.match(result.stderr, /changed while|publication is in progress|inputs changed/); assert.deepEqual(await outputs(), prior); }
    finally { await fs.rm(path.join(learning, '.AuthoredTutor.publishing'), { force: true }); await fs.writeFile(port, original); }
  }
});

test('a consumed dependency changing during compilation cannot certify stale generated outputs', async () => {
  const receipt = JSON.parse(await fs.readFile(path.join(learning, 'obj/authored-tutor-build.json'), 'utf8'));
  const dependency = receipt.dependencies.find(row => /node_modules\/dompurify\/.*\.(mjs|js)$/.test(row.path)); assert.ok(dependency);
  const file = path.join(root, dependency.path), original = await fs.readFile(file), prior = await outputs();
  try { const result = await run([], await mutateDuringFinalCheck('dependency', file)); assert.notEqual(result.code, 0, 'A mid-build dependency edit must fail rather than record its new hash against an old bundle.'); assert.match(result.stderr, /inputs changed|dependency.*changed/); assert.deepEqual(await outputs(), prior); }
  finally { await fs.writeFile(file, original); }
});

test('an output replacement failure preserves the prior complete three-file build', async () => {
  const prior = await outputs(), lesson = content['lessons.json'][0], oldTitle = lesson.title;
  const preload = path.join(root, 'output-lock.mjs');
  await fs.writeFile(preload, `import fs from 'node:fs/promises';\nconst rename=fs.rename;let failed=false;fs.rename=async function(from,to){if(!failed&&String(to).replaceAll('\\\\','/').endsWith('/Tutor/conversation.js')){failed=true;const error=Error('Isolated test simulates a destination locked by another process');error.code='EBUSY';throw error;}return rename.call(this,from,to);};\n`);
  try { lesson.title += ' — changed before replacement test'; await publish(); const result = await run([], preload); assert.notEqual(result.code, 0); assert.match(result.stderr, /locked by another process/); assert.deepEqual(await outputs(), prior, 'An output failure must not leave one new bundle beside old native lesson data.'); }
  finally { lesson.title = oldTitle; await publish(); }
});

test('compiled browser UI preserves authored pathways, counts, key, feedback and saved snapshots', { skip: !process.env.STATSDIRECT_PLAYWRIGHT_PATH }, async () => {
  const result = await run(); assert.equal(result.code, 0, result.stderr);
  const { chromium } = createRequire(import.meta.url)(process.env.STATSDIRECT_PLAYWRIGHT_PATH);
  const browser = await chromium.launch({ channel: process.env.STATSDIRECT_BROWSER_CHANNEL || 'msedge', headless: true });
  const base = path.join(root, 'StatsDirectUI/Assets'), errors = [], external = [];
  const server = http.createServer(async (request, response) => {
    const relative = decodeURIComponent(new URL(request.url, 'http://localhost').pathname).replace(/^\/+/, ''), file = path.resolve(base, relative);
    if (!file.startsWith(base + path.sep)) { response.writeHead(403).end(); return; }
    try { const body = await fs.readFile(file); response.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : file.endsWith('.css') ? 'text/css' : file.endsWith('.html') ? 'text/html' : 'application/octet-stream'); response.end(body); } catch { response.writeHead(404).end(); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve)); const origin = 'http://127.0.0.1:' + server.address().port;
  try {
    for (const choice of ['B', 'A']) {
      const context = await browser.newContext({ viewport: { width: 1100, height: 850 } });
      await context.route('**/*', async route => { if (route.request().url().startsWith(origin + '/')) await route.continue(); else { external.push(route.request().url()); await route.abort(); } });
      const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
      await page.goto(origin + '/Learn/index.html');
      await page.locator('#lessons button').first().click(); await page.locator('.lesson-guide').first().evaluate(node => node.open = true);
      assert.equal(await page.locator('#main h1').innerText(), content['lessons.json'][0].title);
      assert.ok((await page.locator('.lesson-guide').innerText()).includes('Reviewer: Isolated QA author'));
      if (evidence && choice === 'A') await page.screenshot({ path: path.join(evidence, 'compiled-authored-lesson.png'), fullPage: true });
      assert.equal(await page.locator('#lessons button').count(), 12);
      for (const track of Object.keys(content['tracks.json'])) { await page.locator('#profile').selectOption(track); const expected = content['questions.json'].filter(q => q.track === track || q.track === 'core').length; assert.equal(await page.locator('#practiceNav span').innerText(), String(expected), track); }
      await page.locator('#profile').selectOption('qaHealth'); await page.locator('#practiceNav').click(); await page.locator('#supported').click();
      assert.equal(await page.locator('.question').innerText(), content['questions.json'][0].stem);
      await page.locator(`input[name=answer][value=${choice}]`).check(); await page.locator('#confidence').selectOption('fairly'); await page.getByRole('button', { name: 'Submit first answer', exact: true }).click();
      const feedback = await page.locator('#feedback').innerText(); assert.ok(feedback.includes(content['questions.json'][0].options.find(o => o.id === choice).feedback)); assert.ok(feedback.includes(content['questions.json'][0].explanation));
      assert.equal(await page.locator('#feedback h2').innerText(), choice === 'A' ? 'Correct' : 'A useful point to revisit');
      if (evidence && choice === 'A') await page.screenshot({ path: path.join(evidence, 'compiled-authored-feedback.png'), fullPage: true });
      const saved = await page.evaluate(() => JSON.parse(localStorage.getItem('statsdirect-learning-preview'))); assert.equal(saved.quiz.questionIds.filter(id => id === 'QA-NEW-01').length, 1); assert.equal(saved.quiz.answers[0].correct, choice === 'A'); assert.equal(new Set(saved.quiz.questionIds).size, saved.quiz.questionIds.length);
      await page.reload(); assert.equal(await page.locator('#loadError').count(), 0); assert.ok((await page.locator('.question').innerText()).length > 0); assert.ok(!await page.locator('.question').innerText().then(text => text.includes('12 / 60')));
      if (evidence && choice === 'A') { await fs.mkdir(evidence, { recursive: true }); await page.screenshot({ path: path.join(evidence, 'compiled-practice.png'), fullPage: true }); }
      await context.close();
    }
    assert.deepEqual(errors, []); assert.deepEqual(external, []);
  } finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
});
