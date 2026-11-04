// Validate authored built-in tutor content before it becomes an application asset.
// This is deliberately separate from provider-course import: built-in IDs and
// pathway membership remain intact. No authored JavaScript is executed here.
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';

const FORMAT = 'statsdirect-builtin-tutor';
const FILES = ['lessons.json', 'questions.json', 'tracks.json', 'bank.mjs'];
export const authoredOutputs = ['StatsDirectUI/Assets/Learn/app.js', 'StatsDirectUI/Assets/Tutor/conversation.js', 'StatsDirectUI/Assets/Learn/lessons.json'];
const digest = value => crypto.createHash('sha256').update(value).digest('hex');
const identifier = value => typeof value === 'string' && /^[A-Za-z0-9_-]{1,100}$/.test(value);
const text = (value, maximum, required = true) => typeof value === 'string' && value.length <= maximum && (!required || value.trim().length > 0);
const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const canonical = value => Array.isArray(value) ? value.map(canonical) : object(value) ? Object.fromEntries(Object.keys(value).sort().map(key => [key, canonical(value[key])])) : value;
const same = (a, b) => JSON.stringify(canonical(a)) === JSON.stringify(canonical(b));
const difficultyLevels = new Set(['beginner', 'intermediate', 'advanced']);
const learnerGoalIDs = new Set(['undergraduateMedicine', 'undergraduateHealthScience', 'postgraduateMedicine', 'postgraduateAlliedHealth', 'publicHealth', 'healthcareBusinessAnalyst', 'mastersStatistics', 'usMedicalBoards']);
function requireThat(test, message) { if (!test) throw Error('Authored tutor: ' + message); }
function validateQuestionTargets(question) {
  requireThat(question.difficulty == null || difficultyLevels.has(question.difficulty), 'a question difficulty must be beginner, intermediate or advanced when assigned: ' + question.id);
  requireThat(question.learnerGoals == null || Array.isArray(question.learnerGoals) && question.learnerGoals.length <= 8 && question.learnerGoals.every(goal => learnerGoalIDs.has(goal)) && new Set(question.learnerGoals).size === question.learnerGoals.length, 'question learnerGoals must be at most eight unique recognised goal IDs: ' + question.id);
  requireThat(question.learnerGoalNotes == null || text(question.learnerGoalNotes, 3000, false), 'question learnerGoalNotes must be text of at most 3000 characters: ' + question.id);
}
function localPath(repoRoot, relative) {
  requireThat(typeof relative === 'string' && relative.length > 0 && !path.isAbsolute(relative), 'expected a repository-relative path.');
  const full = path.resolve(repoRoot, relative), base = path.resolve(repoRoot);
  requireThat(full.startsWith(base + path.sep), 'a recorded input/output path is outside the repository.');
  return full;
}
async function exists(file) { try { await fs.access(file); return true; } catch (error) { if (error.code === 'ENOENT') return false; throw error; } }
async function readOwnedFile(file, maximum = 5_000_000) {
  const info = await fs.lstat(file);
  requireThat(info.isFile() && !info.isSymbolicLink() && info.size <= maximum, 'invalid or oversized source file: ' + path.basename(file));
  return fs.readFile(file);
}
function json(bytes, name) { try { return JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes)); } catch { throw Error('Authored tutor: invalid UTF-8 JSON in ' + name); } }
function unique(records, label) {
  requireThat(Array.isArray(records) && records.every(row => object(row) && identifier(row.id)) && new Set(records.map(row => row.id)).size === records.length, label + ' require unique stable IDs.');
}

// Only three JSON-valued exports and comments are accepted. The module is data,
// rather than a program that the validator imports or evaluates.
export function parseStaticBank(source) {
  let remaining = source.replaceAll('\r\n', '\n');
  const exports = {};
  for (const name of ['bankVersion', 'tracks', 'questions']) {
    remaining = remaining.replace(/^(?:\s|\/\/[^\n]*(?:\n|$))*/, '');
    const start = remaining.match(new RegExp('^export const ' + name + '\\s*=\\s*'));
    requireThat(start, 'bank.mjs must contain only the three static JSON exports.');
    remaining = remaining.slice(start[0].length);
    let quoted = false, escaped = false, depth = 0, end = -1;
    for (let index = 0; index < remaining.length; index++) {
      const char = remaining[index];
      if (quoted) { if (escaped) escaped = false; else if (char === '\\') escaped = true; else if (char === '"') quoted = false; }
      else if (char === '"') quoted = true;
      else if (char === '[' || char === '{') depth++;
      else if (char === ']' || char === '}') depth--;
      else if (char === ';' && depth === 0) { end = index; break; }
      requireThat(depth >= 0, 'bank.mjs contains malformed JSON.');
    }
    requireThat(end >= 0 && !quoted && depth === 0, 'bank.mjs has an incomplete static export.');
    try { exports[name] = JSON.parse(remaining.slice(0, end)); } catch { throw Error('Authored tutor: bank.mjs export is not JSON: ' + name); }
    remaining = remaining.slice(end + 1);
  }
  requireThat(remaining.replace(/(?:\s|\/\/[^\n]*(?:\n|$))*/g, '') === '', 'bank.mjs contains executable or unexpected content.');
  return exports;
}

function validateData(manifest, lessons, questions, tracks) {
  unique(lessons, 'Lessons'); unique(questions, 'Questions');
  requireThat(lessons.length > 0 && lessons.length <= 100 && questions.length <= 300, 'lesson/question counts exceed authoring limits.');
  requireThat(object(tracks) && Object.keys(tracks).length > 0 && Object.keys(tracks).every(id => identifier(id) && text(tracks[id]?.title, 300)), 'pathway definitions are invalid.');
  requireThat(['foundation', 'medicine', 'publicHealth', 'analyst', 'researcher'].every(id => Object.hasOwn(tracks, id)), 'retain all original pathway IDs so new and saved learning records remain compatible.');
  requireThat(manifest.lessonCount === lessons.length && manifest.questionCount === questions.length && manifest.trackCount === Object.keys(tracks).length, 'manifest counts do not match its content.');
  const lessonIds = new Set(lessons.map(row => row.id)), questionIds = new Set(questions.map(row => row.id));
  const questionMap = new Map(questions.map(row => [row.id, row]));
  for (const lesson of lessons) {
    requireThat(text(lesson.title, 200) && text(lesson.objective, 3000) && text(lesson.summary, 30000), 'a lesson is missing its title, objective or teaching content: ' + lesson.id);
    requireThat(!lesson.prerequisites || Array.isArray(lesson.prerequisites) && lesson.prerequisites.every(id => lessonIds.has(id)), 'a lesson has an unknown prerequisite: ' + lesson.id);
    requireThat(!lesson.assessmentIds || Array.isArray(lesson.assessmentIds) && lesson.assessmentIds.every(id => questionIds.has(id)), 'a lesson points to an unknown question: ' + lesson.id);
    for (const nested of lesson.practiceQuestions || []) requireThat(same(nested, questionMap.get(nested.id)), 'an embedded question differs from the published bank: ' + nested.id);
  }
  const visit = (id, ancestors = []) => {
    requireThat(!ancestors.includes(id), 'lesson prerequisites contain a cycle.');
    for (const next of lessons.find(row => row.id === id).prerequisites || []) visit(next, [...ancestors, id]);
  };
  for (const lesson of lessons) visit(lesson.id);
  for (const question of questions) {
    validateQuestionTargets(question);
    requireThat(Number.isInteger(question.version) && question.version > 0 && text(question.stem, 6000) && text(question.explanation, 10000), 'a question is missing a version, stem or explanation: ' + question.id);
    requireThat(['topic', 'hint', 'lesson', 'help'].every(key => typeof question[key] === 'string'), 'a built-in question needs string topic, hint, lesson and help fields for practice and saved-record compatibility: ' + question.id);
    requireThat(question.track === 'core' || Object.hasOwn(tracks, question.track), 'a question has an unknown pathway: ' + question.id);
    const options = question.options;
    requireThat(Array.isArray(options) && options.length >= 2 && options.length <= 8 && options.every(row => object(row) && /^[A-H]$/.test(row.id) && text(row.text, 3000) && typeof row.feedback === 'string') && new Set(options.map(row => row.id)).size === options.length && options.some(row => row.id === question.correct), 'a question has invalid options, feedback or answer key: ' + question.id);
    if (question.lessonId) requireThat(lessonIds.has(question.lessonId), 'a question links to an unknown lesson: ' + question.id);
  }
  requireThat(manifest.reviewGatePassed === true && Array.isArray(manifest.unreviewedChangedItems) && manifest.unreviewedChangedItems.length === 0 && Array.isArray(manifest.reviewedChangedItems), 'changed material has not passed the author-review publication gate.');
  for (const review of manifest.reviewedChangedItems) {
    requireThat(object(review) && ['lessons', 'questions'].includes(review.kind) && identifier(review.id) && text(review.reviewer, 200) && text(review.notes, 10000) && /^\d{4}-\d{2}-\d{2}$/.test(review.date), 'a changed-item review record is incomplete.');
    const row = (review.kind === 'lessons' ? lessons : questions).find(item => item.id === review.id);
    requireThat(row && /^Reviewed/i.test(row.review?.status || '') && !/draft/i.test(row.review.status) && row.review.reviewer === review.reviewer && row.review.date === review.date && row.review.notes === review.notes, 'a changed item does not match its stated author review: ' + review.id);
  }
}

export async function resolveAuthoredTutor(repoRoot) {
  repoRoot = path.resolve(repoRoot);
  const marker = path.join(repoRoot, 'Learning/.AuthoredTutor.publishing'), root = path.join(repoRoot, 'Learning/AuthoredTutor');
  requireThat(!await exists(marker), 'publication is in progress; wait for it to finish before building.');
  const manifestPath = path.join(root, 'manifest.json');
  if (!await exists(manifestPath)) return null;
  requireThat(!(await fs.lstat(root)).isSymbolicLink(), 'the authored source directory must not be linked.');
  const manifestBytes = await readOwnedFile(manifestPath), manifest = json(manifestBytes, 'manifest.json');
  requireThat(object(manifest) && manifest.schemaVersion === 1 && manifest.format === FORMAT && text(manifest.bankVersion, 200) && /^[0-9a-f]{64}$/i.test(manifest.sourceCourseSha256 || ''), 'manifest format/provenance is invalid.');
  requireThat(Array.isArray(manifest.files) && manifest.files.length === 4 && new Set(manifest.files.map(row => row.path)).size === 4 && FILES.every(name => manifest.files.some(row => row.path === name)), 'manifest must name exactly the four built-in content files.');
  const contents = {}, texts = {};
  for (const entry of manifest.files) {
    requireThat(object(entry) && /^[0-9a-f]{64}$/i.test(entry.sha256 || '') && Number.isInteger(entry.bytes) && entry.bytes > 0, 'invalid source hash/size in manifest.');
    const bytes = await readOwnedFile(path.join(root, entry.path));
    requireThat(bytes.length === entry.bytes && digest(bytes) === entry.sha256.toLowerCase(), 'source hash/size mismatch: ' + entry.path);
    texts[entry.path] = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    if (entry.path.endsWith('.json')) contents[entry.path] = json(bytes, entry.path);
  }
  validateData(manifest, contents['lessons.json'], contents['questions.json'], contents['tracks.json']);
  const bank = parseStaticBank(texts['bank.mjs']);
  requireThat(bank.bankVersion === manifest.bankVersion && same(bank.tracks, contents['tracks.json']) && same(bank.questions, contents['questions.json']), 'static bank exports do not match the validated JSON content.');
  requireThat(!await exists(marker) && digest(await readOwnedFile(manifestPath)) === digest(manifestBytes), 'authored sources changed while validating; retry the build.');
  return { repoRoot, root, manifest, texts, fingerprint: digest(manifestBytes), bankPath: path.join(root, 'bank.mjs'), lessonsPath: path.join(root, 'lessons.json') };
}

async function walk(directory) {
  const files = [];
  for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
    if (entry.isDirectory() && entry.name !== 'node_modules') files.push(...await walk(path.join(directory, entry.name)));
    else if (entry.isFile() && /\.(mjs|json)$/.test(entry.name)) files.push(path.join(directory, entry.name));
  }
  return files;
}
async function fileRecords(repoRoot, paths) {
  const records = [];
  for (const relative of [...new Set(paths)].sort()) {
    const file = localPath(repoRoot, relative), bytes = await fs.readFile(file);
    records.push({ path: relative.replaceAll(path.sep, '/'), sha256: digest(bytes), bytes: bytes.length });
  }
  return records;
}
export async function authoredBuildInputs(repoRoot, authored) {
  const learning = path.join(repoRoot, 'Learning'), paths = [];
  for (const entry of await fs.readdir(learning, { withFileTypes: true })) if (entry.isFile() && (/\.mjs$/.test(entry.name) || ['package.json', 'pnpm-lock.yaml', 'upstream.lock.json'].includes(entry.name))) paths.push(path.join(learning, entry.name));
  paths.push(...await walk(path.join(learning, 'upstream/Learn')));
  for (const file of ['node_modules/.modules.yaml', 'node_modules/.pnpm/lock.yaml', 'upstream/Learn/node_modules/.modules.yaml']) if (await exists(path.join(learning, file))) paths.push(path.join(learning, file));
  if (authored) paths.push(...['manifest.json', ...FILES].map(file => path.join(authored.root, file)));
  else if (await exists(path.join(repoRoot, 'StatsDirectUI/Assets/Learn/lessons.json'))) paths.push(path.join(repoRoot, 'StatsDirectUI/Assets/Learn/lessons.json'));
  const records = await fileRecords(repoRoot, paths.map(file => path.relative(repoRoot, file)));
  return { records, fingerprint: digest(JSON.stringify(records)) };
}
const receiptPath = repoRoot => path.join(repoRoot, 'Learning/obj/authored-tutor-build.json');
export async function authoredBuildIsCurrent(repoRoot, authored, inputs) {
  if (!authored) return false;
  try {
    const receipt = json(await fs.readFile(receiptPath(repoRoot)), 'build receipt');
    if (receipt.schemaVersion !== 1 || receipt.format !== FORMAT || receipt.manifestSha256 !== authored.fingerprint || receipt.inputFingerprint !== inputs.fingerprint || !Array.isArray(receipt.dependencies) || !Array.isArray(receipt.outputs) || receipt.outputs.length !== authoredOutputs.length) return false;
    if (!authoredOutputs.every(file => receipt.outputs.some(row => row.path === file))) return false;
    return same(await fileRecords(repoRoot, receipt.dependencies.map(row => row.path)), receipt.dependencies) && same(await fileRecords(repoRoot, authoredOutputs), receipt.outputs);
  } catch { return false; }
}
export async function assertAuthoredBuildInputsUnchanged(repoRoot, authored, inputs) {
  const current = await resolveAuthoredTutor(repoRoot);
  requireThat(current?.fingerprint === authored?.fingerprint && (await authoredBuildInputs(repoRoot, current)).fingerprint === inputs.fingerprint, 'build inputs changed while bundling; retry before shipping any assets.');
}
export async function assertConsumedBuildInputsUnchanged(repoRoot, records) {
  requireThat(same(await fileRecords(repoRoot, records.map(row => row.path)), records), 'a consumed dependency changed while bundling; retry before shipping any assets.');
}
export async function recordAuthoredBuild(repoRoot, authored, inputs, dependencies = [], consumedRecords) {
  if (!authored) return;
  // Preserve the hashes of the bytes actually bundled. Rereading after output
  // promotion could certify an older bundle against a concurrently edited file.
  const records = consumedRecords ?? await fileRecords(repoRoot, dependencies);
  requireThat(same([...new Set(dependencies.map(file => file.replaceAll(path.sep, '/')))].sort(), records.map(row => row.path).sort()), 'consumed dependency records do not match the build inputs.');
  const receipt = { schemaVersion: 1, format: FORMAT, builtAtUtc: new Date().toISOString(), bankVersion: authored.manifest.bankVersion, manifestSha256: authored.fingerprint, inputFingerprint: inputs.fingerprint, inputs: inputs.records, dependencies: records, outputs: await fileRecords(repoRoot, authoredOutputs) };
  await atomicWrite(receiptPath(repoRoot), Buffer.from(JSON.stringify(receipt, null, 2) + '\n'));
}
export async function atomicWrite(file, bytes) {
  await fs.mkdir(path.dirname(file), { recursive: true });
  const temporary = path.join(path.dirname(file), '.tutor-build-' + crypto.randomUUID() + '.tmp');
  try { await fs.writeFile(temporary, bytes, { flag: 'wx' }); await fs.rename(temporary, file); }
  finally { await fs.rm(temporary, { force: true }); }
}

// Stage a complete generated build before replacing destinations. Prior bytes
// exist only in bounded temporary files during this transaction and are removed
// on both success and failure; no persistent rollback build is retained.
export async function promoteBuildOutputs(replacements, complete = async () => {}, validate = async () => {}) {
  requireThat(new Set(replacements.map(row => path.resolve(row.path))).size === replacements.length, 'generated output destinations must be unique.');
  const staged = [], replaced = [];
  try {
    for (const row of replacements) {
      await fs.mkdir(path.dirname(row.path), { recursive: true });
      const state = { path: row.path, next: path.join(path.dirname(row.path), '.tutor-build-' + crypto.randomUUID() + '.tmp'), prior: null };
      staged.push(state);
      try {
        const info = await fs.lstat(row.path);
        requireThat(info.isFile() && !info.isSymbolicLink(), 'a generated output destination is not a regular file.');
        state.prior = path.join(path.dirname(row.path), '.tutor-build-' + crypto.randomUUID() + '.tmp');
        await fs.writeFile(state.prior, await fs.readFile(row.path), { flag: 'wx' });
      } catch (error) { if (error.code !== 'ENOENT') throw error; }
      await fs.writeFile(state.next, row.bytes, { flag: 'wx' });
    }
    await validate();
    for (const state of staged) { await fs.rename(state.next, state.path); replaced.push(state); }
    await complete();
  } catch (error) {
    const failures = [];
    for (const state of replaced.reverse()) {
      try { if (state.prior) await fs.rename(state.prior, state.path); else await fs.rm(state.path, { force: true }); }
      catch (restoreError) { failures.push(restoreError); }
    }
    if (failures.length) throw new AggregateError([error, ...failures], 'The tutor build failed and an output could not be restored. Do not ship this build.');
    throw error;
  } finally {
    for (const state of staged) for (const file of [state.next, state.prior]) if (file) await fs.rm(file, { force: true });
  }
}
