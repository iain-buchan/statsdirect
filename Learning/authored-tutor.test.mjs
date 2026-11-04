import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import { build } from 'esbuild';
import { fileURLToPath } from 'node:url';
import { resolveAuthoredTutor, parseStaticBank, authoredBuildInputs, authoredBuildIsCurrent, assertAuthoredBuildInputsUnchanged, recordAuthoredBuild, authoredOutputs, atomicWrite, promoteBuildOutputs } from './authored-tutor.mjs';

const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
async function fixture(action) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'StatsDirect-authored-check-'));
  const authored = path.join(root, 'Learning/AuthoredTutor');
  const write = async (name, text) => { const file = path.join(root, name); await fs.mkdir(path.dirname(file), { recursive: true }); await fs.writeFile(file, text); };
  const question = { id: 'Q1', version: 2, track: 'medicine', topic: 'Denominators', hint: '', lesson: '', help: '', stem: 'What proportion is 72/90?', options: [{ id: 'A', text: '80%', feedback: '72/90=80%.' }, { id: 'B', text: '20%', feedback: 'This is the complement.' }], correct: 'A', explanation: 'Use the people with the condition as denominator.', review: { status: 'Reviewed by course author', reviewer: 'Tutor', date: '2026-11-03', notes: 'Checked the denominator and key.' } };
  const lessons = [{ id: 'L1', title: 'Denominators', objective: 'Choose the denominator.', summary: 'Original fictional example.', prerequisites: [], assessmentIds: ['Q1'], practiceQuestions: [structuredClone(question)] }];
  const tracks = { foundation: { title: 'Medical undergraduate' }, medicine: { title: 'Postgraduate medicine', description: 'Interpret evidence.' }, publicHealth: { title: 'Public health postgraduate' }, analyst: { title: 'Healthcare analyst', description: 'Interpret services.' }, researcher: { title: 'Researcher' } };
  const files = { 'lessons.json': lessons, 'questions.json': [question], 'tracks.json': tracks };
  const manifest = { schemaVersion: 1, format: 'statsdirect-builtin-tutor', sourceCourseSha256: 'a'.repeat(64), bankVersion: 'test-bank-2', lessonCount: 1, questionCount: 1, trackCount: 5, reviewGatePassed: true, reviewedChangedItems: [{ kind: 'questions', id: 'Q1', reviewer: 'Tutor', date: '2026-11-03', notes: question.review.notes }], unreviewedChangedItems: [] };
  const publish = async () => {
    const texts = Object.fromEntries(Object.entries(files).map(([name, value]) => [name, JSON.stringify(value, null, 2) + '\n']));
    texts['bank.mjs'] = '// Generated static data.\nexport const bankVersion = ' + JSON.stringify(manifest.bankVersion) + ';\nexport const tracks = ' + JSON.stringify(tracks, null, 2) + ';\nexport const questions = ' + JSON.stringify(files['questions.json'], null, 2) + ';\n';
    manifest.files = Object.entries(texts).map(([name, text]) => ({ path: name, sha256: hash(text), bytes: Buffer.byteLength(text) }));
    for (const [name, text] of Object.entries(texts)) await write('Learning/AuthoredTutor/' + name, text);
    await write('Learning/AuthoredTutor/manifest.json', JSON.stringify(manifest, null, 2));
  };
  await write('Learning/upstream/Learn/app.mjs', '// original upstream entry\n');
  await write('Learning/upstream/Learn/lessons.json', '[]\n');
  for (const name of ['build.mjs', 'authored-tutor.mjs', 'selection-port.mjs', 'recovery-port.mjs', 'conversation.mjs', 'conversation-view.mjs']) await write('Learning/' + name, '// fixture source\n');
  await write('Learning/package.json', '{"type":"module"}\n'); await write('Learning/pnpm-lock.yaml', 'lockfileVersion: 9\n'); await write('Learning/upstream.lock.json', '{}\n');
  await write('Learning/node_modules/vendor/index.js', 'export default "v1";\n');
  for (const file of authoredOutputs) await write(file, 'original generated output\n');
  try { await publish(); await action({ root, authored, write, publish, manifest, files, lessons, question, tracks }); }
  finally {
    // Only this freshly created, bounded fixture tree is removed.
    assert.equal(path.dirname(path.resolve(root)), path.resolve(os.tmpdir()));
    assert.ok(path.basename(root).startsWith('StatsDirect-authored-check-'));
    await fs.rm(root, { recursive: true, force: true });
  }
}

test('valid authored data preserves static question identity, pathways and frozen source text', () => fixture(async f => {
  const data = await resolveAuthoredTutor(f.root);
  assert.equal(data.manifest.questionCount, 1);
  assert.equal(JSON.parse(data.texts['questions.json'])[0].track, 'medicine');
  assert.equal(parseStaticBank(data.texts['bank.mjs']).tracks.analyst.title, 'Healthcare analyst');
  await f.write('Learning/AuthoredTutor/lessons.json', 'later change');
  assert.equal(JSON.parse(data.texts['lessons.json'])[0].id, 'L1');
}));

test('optional question classifications preserve multiple goals without changing the existing pathway or legacy records', () => fixture(async f => {
  let state = await resolveAuthoredTutor(f.root);
  assert.equal(Object.hasOwn(JSON.parse(state.texts['questions.json'])[0], 'difficulty'), false);
  await recordAuthoredBuild(f.root, state, await authoredBuildInputs(f.root, state));
  for (const difficulty of ['beginner', 'intermediate', 'advanced']) {
    Object.assign(f.question, { difficulty, learnerGoals: ['undergraduateMedicine', 'mastersStatistics', 'usMedicalBoards'], learnerGoalNotes: 'Author-estimated difficulty; tailor the specialty and exam stage before use.' });
    f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish(); state = await resolveAuthoredTutor(f.root);
    const rootQuestion = JSON.parse(state.texts['questions.json'])[0], bankQuestion = parseStaticBank(state.texts['bank.mjs']).questions[0];
    assert.deepEqual(rootQuestion, f.question); assert.deepEqual(bankQuestion, f.question);
    assert.deepEqual(JSON.parse(state.texts['lessons.json'])[0].practiceQuestions[0], f.question);
    assert.equal(bankQuestion.track, 'medicine');
    assert.equal(await authoredBuildIsCurrent(f.root, state, await authoredBuildInputs(f.root, state)), false, 'classification-only edits must invalidate the prior compiled-content receipt');
  }
  Object.assign(f.question, { learnerGoals: ['undergraduateMedicine', 'undergraduateHealthScience', 'postgraduateMedicine', 'postgraduateAlliedHealth', 'publicHealth', 'healthcareBusinessAnalyst', 'mastersStatistics', 'usMedicalBoards'], learnerGoalNotes: 'x'.repeat(3000) });
  f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  assert.equal(parseStaticBank((await resolveAuthoredTutor(f.root)).texts['bank.mjs']).questions[0].learnerGoals.length, 8);
  Object.assign(f.question, { difficulty: null, learnerGoals: null, learnerGoalNotes: null });
  f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  assert.equal(parseStaticBank((await resolveAuthoredTutor(f.root)).texts['bank.mjs']).questions[0].difficulty, null);
  Object.assign(f.question, { learnerGoals: [], learnerGoalNotes: '' });
  f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  assert.deepEqual(parseStaticBank((await resolveAuthoredTutor(f.root)).texts['bank.mjs']).questions[0].learnerGoals, []);
}));

test('malformed classification shapes, unknown goals, duplicates and excessive notes fail before outputs change', () => fixture(async f => {
  const prior = await Promise.all(authoredOutputs.map(file => fs.readFile(path.join(f.root, file))));
  for (const [key, values, pattern] of [
    ['difficulty', ['', 'Advanced', 'beginner\n', 2, {}, []], /question difficulty/],
    ['learnerGoals', ['undergraduateMedicine', {}, [42], ['unknownGoal'], ['publicHealth', 'publicHealth'], Array(9).fill('publicHealth')], /question learnerGoals/],
    ['learnerGoalNotes', [42, {}, [], 'x'.repeat(3001)], /question learnerGoalNotes/]
  ]) {
    for (const value of values) {
      f.question[key] = value; f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
      await assert.rejects(resolveAuthoredTutor(f.root), pattern, key + ': ' + JSON.stringify(value)); delete f.question[key];
      assert.deepEqual(await Promise.all(authoredOutputs.map(file => fs.readFile(path.join(f.root, file)))), prior);
    }
  }
}));

test('actual provider conversion, learner attempts, restore and JSON record exports retain question classifications', () => fixture(async f => {
  Object.assign(f.question, { difficulty: 'advanced', learnerGoals: ['healthcareBusinessAnalyst', 'publicHealth'], learnerGoalNotes: 'Case-mix reasoning for a local health-service course.' });
  f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  const bundle = await build({ stdin: { contents: "export {createSession,recordAnswer} from './upstream/Learn/quiz.mjs'; export {newPortfolio,restorePortfolio,reviewRecord,reviewText} from './upstream/Learn/portfolio.mjs'; export {courseQuestions} from './upstream/Learn/provider-course.mjs';", resolveDir: fileURLToPath(new URL('./', import.meta.url)), sourcefile: 'classification-runtime-check.mjs' }, bundle: true, write: false, format: 'esm', platform: 'node', plugins: [{ name: 'isolated-authored-bank', setup(b) { b.onResolve({ filter: /^\.\/bank\.mjs$/ }, () => ({ path: path.join(f.authored, 'bank.mjs') })); } }] });
  const runtime = await import('data:text/javascript;base64,' + Buffer.from(bundle.outputFiles[0].contents).toString('base64'));
  const expected = { difficulty: f.question.difficulty, learnerGoals: f.question.learnerGoals, learnerGoalNotes: f.question.learnerGoalNotes };
  const classifications = question => Object.fromEntries(Object.keys(expected).map(key => [key, question[key]]));
  const provider = { schemaVersion: 2, id: 'local-health', version: '1', title: 'Local health course', questions: [f.question] };
  const converted = runtime.courseQuestions(provider);
  assert.deepEqual(classifications(converted[0]), expected); assert.equal(converted[0].track, 'core');
  for (const pack of [null, { version: '1', questions: converted, course: { key: 'local-health:1', title: 'Local health course', version: '1', provider: 'Local author' } }]) {
    const session = runtime.createSession('medicine', 'learn', crypto.randomUUID(), pack);
    assert.deepEqual(classifications(session.questionSnapshots[0]), expected);
    runtime.recordAnswer(session, session.questionIds[0], 'A', 'fairly');
    const portfolio = runtime.newPortfolio(); portfolio.profile = 'medicine'; portfolio.quiz = session;
    const restored = runtime.restorePortfolio(JSON.parse(JSON.stringify(portfolio)));
    assert.deepEqual(classifications(restored.quiz.questionSnapshots[0]), expected);
    const exported = JSON.parse(JSON.stringify(runtime.reviewRecord(restored)));
    assert.deepEqual(classifications(exported.attempts[0].questionSnapshots[0]), expected);
    assert.equal(exported.attempts[0].result.score, 1);
    // Existing readable reports enumerate selected fields; JSON is the complete
    // classification-preserving evidence route until a learner UI is requested.
    assert.ok(!runtime.reviewText(exported).includes(expected.learnerGoalNotes));
  }
}));
test('no manifest retains ordinary upstream fallback and an active publication never falls back', () => fixture(async f => {
  await fs.unlink(path.join(f.authored, 'manifest.json'));
  assert.equal(await resolveAuthoredTutor(f.root), null);
  await f.write('Learning/.AuthoredTutor.publishing', 'in progress');
  await assert.rejects(resolveAuthoredTutor(f.root), /publication is in progress/);
}));
test('tampered data is rejected without changing consumed output files', () => fixture(async f => {
  const before = await Promise.all(authoredOutputs.map(file => fs.readFile(path.join(f.root, file))));
  await fs.appendFile(path.join(f.authored, 'questions.json'), ' ');
  await assert.rejects(resolveAuthoredTutor(f.root), /hash\/size mismatch/);
  assert.deepEqual(await Promise.all(authoredOutputs.map(file => fs.readFile(path.join(f.root, file)))), before);
}));
test('manifest path traversal and duplicate file entries are rejected', () => fixture(async f => {
  f.manifest.files[0].path = '../elsewhere.json';
  await f.write('Learning/AuthoredTutor/manifest.json', JSON.stringify(f.manifest));
  await assert.rejects(resolveAuthoredTutor(f.root), /exactly the four/);
  await f.publish(); f.manifest.files[0] = f.manifest.files[1];
  await f.write('Learning/AuthoredTutor/manifest.json', JSON.stringify(f.manifest));
  await assert.rejects(resolveAuthoredTutor(f.root), /exactly the four/);
}));
test('unreviewed changed items and inconsistent named reviews block activation', () => fixture(async f => {
  f.manifest.unreviewedChangedItems = [{ kind: 'questions', id: 'Q1' }]; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /author-review publication gate/);
  f.manifest.unreviewedChangedItems = []; f.question.review.status = 'Draft'; f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /does not match its stated author review/);
  f.question.review.status = 'Reviewed by course author'; f.question.review.reviewer = 'Other reviewer'; f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /does not match its stated author review/);
  f.question.review.reviewer = 'Tutor'; f.manifest.reviewedChangedItems[0].notes = 'Different review notes'; f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /does not match its stated author review/);
}));
test('unchanged inherited teaching drafts may be preserved without inventing a human review', () => fixture(async f => {
  f.manifest.reviewedChangedItems = []; f.question.review = { status: 'AI-assisted editorial and numerical checks' }; f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  assert.ok(await resolveAuthoredTutor(f.root));
}));
test('unknown pathway, invalid key and duplicate question IDs are refused', () => fixture(async f => {
  f.question.track = 'missing'; f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /unknown pathway/);
  f.question.track = 'medicine'; f.question.correct = 'H'; f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /answer key/);
  f.question.correct = 'A'; f.files['questions.json'].push(structuredClone(f.question)); f.manifest.questionCount = 2; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /unique stable IDs/);
}));
test('original pathway IDs cannot be removed even when no current question uses one', () => fixture(async f => {
  delete f.tracks.foundation; f.manifest.trackCount = 4; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /retain all original pathway IDs/);
}));
test('nested question mismatch, broken assessment links and prerequisite cycles are refused', () => fixture(async f => {
  f.lessons[0].practiceQuestions[0].stem = 'Divergent copy'; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /embedded question differs/);
  f.lessons[0].practiceQuestions = [structuredClone(f.question)]; f.lessons[0].assessmentIds = ['missing']; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /unknown question/);
  f.lessons[0].assessmentIds = ['Q1']; f.lessons[0].prerequisites = ['L1']; await f.publish();
  await assert.rejects(resolveAuthoredTutor(f.root), /prerequisites contain a cycle/);
}));
test('bank validation accepts JSON escaping and rejects executable or divergent exports', () => fixture(async f => {
  f.question.stem = 'A quote "; } // and literal <script> & unicode α'; f.lessons[0].practiceQuestions = [structuredClone(f.question)]; await f.publish();
  const bank = parseStaticBank((await resolveAuthoredTutor(f.root)).texts['bank.mjs']); assert.equal(bank.questions[0].stem, f.question.stem);
  assert.throws(() => parseStaticBank('process.exit(1);\n' + (JSON.stringify(bank))), /three static JSON exports/);
  const original = (await resolveAuthoredTutor(f.root)).texts['bank.mjs'];
  assert.throws(() => parseStaticBank(original + '\nthrow Error("executed");'), /unexpected content/);
  const altered = original.replace('test-bank-2', 'different-bank');
  await f.write('Learning/AuthoredTutor/bank.mjs', altered); const row = f.manifest.files.find(row => row.path === 'bank.mjs'); row.sha256 = hash(altered); row.bytes = Buffer.byteLength(altered); await f.write('Learning/AuthoredTutor/manifest.json', JSON.stringify(f.manifest));
  await assert.rejects(resolveAuthoredTutor(f.root), /static bank exports do not match/);
}));
test('receipt skips only matching source, consumed dependency and every generated output hash', () => fixture(async f => {
  const authored = await resolveAuthoredTutor(f.root), inputs = await authoredBuildInputs(f.root, authored);
  assert.equal(await authoredBuildIsCurrent(f.root, authored, inputs), false);
  await recordAuthoredBuild(f.root, authored, inputs, ['Learning/node_modules/vendor/index.js']);
  assert.equal(await authoredBuildIsCurrent(f.root, authored, await authoredBuildInputs(f.root, authored)), true);
  for (const file of authoredOutputs) {
    const original = await fs.readFile(path.join(f.root, file)); await f.write(file, 'manual stale/tampered output');
    assert.equal(await authoredBuildIsCurrent(f.root, authored, await authoredBuildInputs(f.root, authored)), false, file);
    await f.write(file, original);
  }
  await f.write('Learning/node_modules/vendor/index.js', 'export default "changed";');
  assert.equal(await authoredBuildIsCurrent(f.root, authored, inputs), false);
}));
test('every build source and lockfile change invalidates a receipt; metadata-only mtime changes do not', () => fixture(async f => {
  const authored = await resolveAuthoredTutor(f.root), inputs = await authoredBuildInputs(f.root, authored);
  await recordAuthoredBuild(f.root, authored, inputs, []);
  for (const file of ['Learning/upstream/Learn/app.mjs', 'Learning/build.mjs', 'Learning/authored-tutor.mjs', 'Learning/recovery-port.mjs', 'Learning/package.json', 'Learning/pnpm-lock.yaml', 'Learning/upstream.lock.json']) {
    const original = await fs.readFile(path.join(f.root, file)); await f.write(file, Buffer.concat([original, Buffer.from(' ')]));
    assert.equal(await authoredBuildIsCurrent(f.root, authored, await authoredBuildInputs(f.root, authored)), false, file);
    await f.write(file, original);
  }
  const now = new Date(); await fs.utimes(path.join(f.root, 'Learning/build.mjs'), now, now);
  assert.equal(await authoredBuildIsCurrent(f.root, authored, await authoredBuildInputs(f.root, authored)), true);
}));
test('changed publication inputs are rejected before promotion and receipt traversal cannot cause a skip', () => fixture(async f => {
  const authored = await resolveAuthoredTutor(f.root), inputs = await authoredBuildInputs(f.root, authored);
  await f.write('Learning/selection-port.mjs', '// changed while build was running');
  await assert.rejects(assertAuthoredBuildInputsUnchanged(f.root, authored, inputs), /inputs changed while bundling/);
  const fresh = await authoredBuildInputs(f.root, authored); await recordAuthoredBuild(f.root, authored, fresh, []);
  const receiptPath = path.join(f.root, 'Learning/obj/authored-tutor-build.json'); const receipt = JSON.parse(await fs.readFile(receiptPath, 'utf8'));
  receipt.dependencies = [{ path: '../../outside.txt', sha256: 'a'.repeat(64), bytes: 1 }]; await fs.writeFile(receiptPath, JSON.stringify(receipt));
  assert.equal(await authoredBuildIsCurrent(f.root, authored, fresh), false);
}));
test('atomic writes replace a complete fixture output and leave no temporary files', () => fixture(async f => {
  const file = path.join(f.root, 'StatsDirectUI/Assets/Learn/app.js'); await atomicWrite(file, Buffer.from('complete new output'));
  assert.equal(await fs.readFile(file, 'utf8'), 'complete new output');
  assert.equal((await fs.readdir(path.dirname(file))).filter(name => name.startsWith('.tutor-build-')).length, 0);
}));

test('generated output promotion restores prior bytes on receipt or final-input failure and cleans staging', () => fixture(async f => {
  const files = authoredOutputs.map(file => path.join(f.root, file)), prior = await Promise.all(files.map(file => fs.readFile(file)));
  const replacements = files.map(file => ({ path: file, bytes: Buffer.from('new verified fixture output') }));
  await assert.rejects(promoteBuildOutputs(replacements, async () => { throw Error('Receipt is not writable'); }), /Receipt is not writable/);
  assert.deepEqual(await Promise.all(files.map(file => fs.readFile(file))), prior);
  await assert.rejects(promoteBuildOutputs(replacements, undefined, async () => { throw Error('Inputs changed while staging'); }), /Inputs changed while staging/);
  assert.deepEqual(await Promise.all(files.map(file => fs.readFile(file))), prior);
  for (const directory of new Set(files.map(file => path.dirname(file)))) assert.equal((await fs.readdir(directory)).filter(file => file.startsWith('.tutor-build-')).length, 0);
  await fs.unlink(files[2]);
  await assert.rejects(promoteBuildOutputs(replacements, async () => { throw Error('First-build receipt failure'); }), /First-build receipt failure/);
  await assert.rejects(fs.access(files[2]), /ENOENT/);
  assert.deepEqual(await Promise.all(files.slice(0, 2).map(file => fs.readFile(file))), prior.slice(0, 2));
}));
