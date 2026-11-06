// The shared Learning code is the Learning/ folder of iain-buchan/statisticalhelp, taken as the
// submodule Learning/Shared and pinned by Learning/upstream-manifest.json. This script records
// the pin (--write) and verifies it (--verify, also run by build.mjs before bundling):
//   the submodule's HEAD, the manifest's commit and the repository's gitlink agree;
//   the pinned commit carries one learning-v* tag (unless --allow-untagged);
//   every tracked file under Learning/ in the submodule has the recorded hash, and nothing there
//   is modified or untracked;
//   host-contract.json, the manifest and the host's constant in LearningView.cs agree;
//   the package's dependencies are present at the same versions in Learning/package.json.
import {readFile, writeFile, stat} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import path from 'node:path';

const learningRoot = fileURLToPath(new URL('./', import.meta.url));
export const repoRoot = path.resolve(learningRoot, '..');
export const submodulePath = 'Learning/Shared';
export const packagePath = submodulePath + '/Learning';
export const manifestPath = path.join(learningRoot, 'upstream-manifest.json');
const repository = 'https://github.com/iain-buchan/statisticalhelp.git';

function git(cwd, ...args) { return execFileSync('git', args, {cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe']}).trim(); }
async function sha256(file) { return createHash('sha256').update(await readFile(file)).digest('hex'); }
function fail(message) { throw new Error('Learning upstream: ' + message); }

export async function describeUpstream({allowUntagged = false, requireTag = true} = {}) {
  const submodule = path.join(repoRoot, submodulePath);
  try { await stat(path.join(submodule, 'Learning', 'app.mjs')); }
  catch { fail('the submodule ' + submodulePath + ' is empty; run git submodule update --init ' + submodulePath); }
  const commit = git(submodule, 'rev-parse', 'HEAD');
  // A shallow checkout (continuous integration) may hold no tags, so the tag is demanded when the pin is written, not when it is verified.
  const tags = git(submodule, 'tag', '--points-at', 'HEAD').split('\n').filter(tag => /^learning-v\d+\.\d+\.\d+$/.test(tag));
  if (requireTag && tags.length !== 1 && !allowUntagged) fail('the pinned commit ' + commit.slice(0, 8) + ' carries ' + tags.length + ' learning-v tags; pin a tagged commit or pass --allow-untagged');
  const dirty = git(submodule, 'status', '--porcelain', '--', 'Learning');
  if (dirty) fail('the package has modified or untracked files:\n' + dirty);
  const tracked = git(submodule, 'ls-files', '--', 'Learning').split('\n').filter(Boolean);
  const files = {};
  for (const relative of tracked.sort()) files[relative] = await sha256(path.join(submodule, relative));
  const contract = JSON.parse(await readFile(path.join(submodule, packagePath.slice(submodulePath.length + 1), 'host-contract.json'), 'utf8'));
  if (!Number.isInteger(contract.hostContract)) fail('host-contract.json does not declare an integer hostContract');
  return {repository, commit, tag: tags[0] ?? null, hostContract: contract.hostContract, files};
}

export async function hostContractConstant() {
  const source = await readFile(path.join(repoRoot, 'StatsDirectUI/UI/Learning/LearningView.cs'), 'utf8');
  const match = source.match(/internal const int HostContract = (\d+);/);
  if (!match) fail('LearningView.cs does not declare HostContract');
  return Number(match[1]);
}

// A disposable copy of the repository (the authored build tests) has no Git metadata for the
// submodule; with STATSDIRECT_LEARNING_UPSTREAM_NO_GIT=1 the recorded files are still hashed
// against the manifest, and the contract and dependency checks still apply.
async function describeWithoutGit() {
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  const submodule = path.join(repoRoot, submodulePath);
  const files = {};
  for (const relative of Object.keys(manifest.files ?? {}).sort()) files[relative] = await sha256(path.join(submodule, relative));
  const contract = JSON.parse(await readFile(path.join(submodule, 'Learning', 'host-contract.json'), 'utf8'));
  return {repository: manifest.repository, commit: manifest.commit, tag: manifest.tag ?? null, hostContract: contract.hostContract, files};
}

export async function verifyUpstream({allowUntagged = false} = {}) {
  const withoutGit = process.env.STATSDIRECT_LEARNING_UPSTREAM_NO_GIT === '1';
  const actual = withoutGit ? await describeWithoutGit() : await describeUpstream({allowUntagged, requireTag: false});
  const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  if (manifest.repository !== actual.repository) fail('the manifest names another repository');
  if (manifest.commit !== actual.commit) fail('the submodule is at ' + actual.commit.slice(0, 8) + ' but the manifest pins ' + String(manifest.commit).slice(0, 8));
  if (!withoutGit) {
    const gitlink = git(repoRoot, 'ls-files', '-s', '--', submodulePath).split(/\s+/)[1];
    if (gitlink !== actual.commit) fail('the repository records the submodule at ' + String(gitlink).slice(0, 8) + ', not ' + actual.commit.slice(0, 8) + '; stage the submodule with the manifest');
    if (actual.tag && (manifest.tag ?? null) !== actual.tag) fail('the manifest tag ' + manifest.tag + ' does not match the pinned commit\'s tag ' + actual.tag);
  }
  const recorded = Object.keys(manifest.files ?? {}).sort(), present = Object.keys(actual.files).sort();
  if (recorded.join('\n') !== present.join('\n')) fail('the package file list changed; rerun --write after reviewing the change');
  for (const file of present) if (manifest.files[file] !== actual.files[file]) fail('the package file ' + file + ' does not match its recorded hash');
  const constant = withoutGit ? actual.hostContract : await hostContractConstant(); // A disposable copy carries no host source.
  if (manifest.hostContract !== actual.hostContract || constant !== actual.hostContract)
    fail('host contract mismatch: package ' + actual.hostContract + ', manifest ' + manifest.hostContract + ', host ' + constant + '; adopt the package deliberately and update LearningView.HostContract');
  const upstreamPackage = JSON.parse(await readFile(path.join(repoRoot, packagePath, 'package.json'), 'utf8'));
  const hostPackage = JSON.parse(await readFile(path.join(learningRoot, 'package.json'), 'utf8'));
  const hostVersions = {...(hostPackage.dependencies ?? {}), ...(hostPackage.devDependencies ?? {})};
  for (const [name, version] of Object.entries(upstreamPackage.dependencies ?? {}))
    if (hostVersions[name] !== version) fail('Learning/package.json must carry ' + name + '@' + version + ' as the package does (found ' + hostVersions[name] + ')');
  return actual;
}

export async function verifyBundle(bundleFile, hostContract) {
  const text = await readFile(bundleFile, 'utf8');
  if (!new RegExp('hostContract\\s*:\\s*' + hostContract + '\\b').test(text)) fail('the bundle ' + path.relative(repoRoot, bundleFile) + ' does not carry hostContract ' + hostContract);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = process.argv.slice(2), allowUntagged = args.includes('--allow-untagged');
  if (args.includes('--write')) {
    const actual = await describeUpstream({allowUntagged});
    await writeFile(manifestPath, JSON.stringify(actual, null, 2) + '\n');
    console.log('Recorded ' + submodulePath + ' at ' + actual.commit.slice(0, 8) + (actual.tag ? ' (' + actual.tag + ')' : '') + ', host contract ' + actual.hostContract + ', ' + Object.keys(actual.files).length + ' files.');
  } else if (args.includes('--verify') || args.length === 0) {
    const actual = await verifyUpstream({allowUntagged});
    console.log('Verified ' + submodulePath + ' at ' + actual.commit.slice(0, 8) + (actual.tag ? ' (' + actual.tag + ')' : '') + ', host contract ' + actual.hostContract + ', ' + Object.keys(actual.files).length + ' files.');
  } else throw new Error('Use node Learning/verify-upstream.mjs [--write|--verify] [--allow-untagged].');
}
