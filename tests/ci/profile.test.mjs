import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { profileForRun } from './profile.mjs';

const canonical = 'iain-buchan/statsdirect';
const repo = (full_name = canonical, fork = false) => ({ full_name, fork });
const run = (eventName = 'push', event = {}, actor = 'iain-buchan', repository = canonical) =>
  ({ eventName, event: { repository: repo(repository), ...event }, repository, actor });
const pr = (head = repo(), author = 'contributor') => run('pull_request', {
  pull_request: { base: { repo: repo() }, head: { repo: head }, user: { login: author } }
});

test('normal repository pushes, same-repository PRs and manual runs are eligible', () => {
  for (const fixture of [run(), pr(), run('workflow_dispatch'), run('workflow_dispatch', { inputs: { unlicensed: false } }),
    run('workflow_dispatch', { inputs: { unlicensed: 'false' } })]) {
    const profile = profileForRun(fixture);
    assert.equal(profile.licensed, true);
    assert.match(profile.reason, /configured licence is still required/);
  }
  const fixture = pr();
  fixture.repository = canonical.toUpperCase();
  assert.equal(profileForRun(fixture).licensed, true);
});

test('external PRs and runs in fork-owned repositories never select a licence', () => {
  for (const head of [repo('someone/statsdirect', true), repo('someone/another-repo', false)])
    assert.equal(profileForRun(pr(head)).licensed, false);
  for (const eventName of ['push', 'workflow_dispatch']) {
    const fixture = run(eventName, { repository: repo('someone/statsdirect', true), inputs: { unlicensed: false } }, 'someone', 'someone/statsdirect');
    assert.equal(profileForRun(fixture).licensed, false);
  }
  const fixture = pr(repo('someone/statsdirect', true));
  fixture.event.repository = repo('someone/statsdirect', true);
  fixture.event.pull_request.base.repo = repo('someone/statsdirect', true);
  fixture.repository = 'someone/statsdirect';
  assert.equal(profileForRun(fixture).licensed, false);
});

test('Dependabot actors and PR authors remain unlicensed on human reruns', () => {
  for (const actor of ['dependabot[bot]', 'Dependabot[bot]', 'dependabot', 'dependabot-preview[bot]'])
    for (const eventName of ['push', 'workflow_dispatch'])
      assert.equal(profileForRun(run(eventName, {}, actor)).licensed, false);
  const humanRerun = pr(repo(), 'dependabot[bot]');
  humanRerun.actor = 'iain-buchan';
  humanRerun.event.sender = { login: 'iain-buchan' };
  assert.equal(profileForRun(humanRerun).licensed, false);
  const botActor = pr();
  botActor.actor = 'dependabot[bot]';
  assert.equal(profileForRun(botActor).licensed, false);
});

test('manual input can force no licence but cannot elevate forks or bots', () => {
  for (const unlicensed of [true, 'true']) {
    assert.equal(profileForRun(run('workflow_dispatch', { inputs: { unlicensed } })).licensed, false);
    assert.throws(() => profileForRun(run('workflow_dispatch', { inputs: { unlicensed } }, 'contributor')), /Only the repository owner/);
  }
  assert.equal(profileForRun(run('workflow_dispatch', { inputs: { unlicensed: true } }, 'Iain-Buchan')).licensed, false);
  assert.equal(profileForRun(run('workflow_dispatch', { inputs: { unlicensed: false, licensed: true } }, 'dependabot[bot]')).licensed, false);
  assert.equal(profileForRun(run('workflow_dispatch', { inputs: { unlicensed: true } }, 'dependabot[bot]')).licensed, false);
  assert.equal(profileForRun(run('workflow_dispatch', { repository: repo('someone/statsdirect', true), inputs: { unlicensed: true } }, 'contributor', 'someone/statsdirect')).licensed, false);
  for (const invalid of ['yes', '1', 1, {}, [], null])
    assert.throws(() => profileForRun(run('workflow_dispatch', { inputs: { unlicensed: invalid } })), /must be true or false/);
});

test('empty or partial PR trust metadata selects only unlicensed checks', () => {
  for (const event of [undefined, null, {}, { repository: null }, { repository: repo(), pull_request: null }])
    assert.equal(profileForRun({ ...pr(), event }).licensed, false);
  for (const mutate of [
    f => { delete f.event.repository; },
    f => { f.event.repository = {}; },
    f => { delete f.event.pull_request; },
    f => { f.event.pull_request.base = null; },
    f => { f.event.pull_request.head.repo = null; },
    f => { f.event.pull_request.head.repo.fork = null; },
    f => { delete f.event.pull_request.head.repo.fork; },
    f => { f.event.pull_request.user = {}; },
    f => { f.event.pull_request.user.login = null; }
  ]) {
    const fixture = pr(); mutate(fixture);
    const profile = profileForRun(fixture);
    assert.equal(profile.licensed, false);
    assert.match(profile.reason, /metadata is unavailable/);
  }
});

test('unsupported privileged events and malformed trust metadata fail closed', () => {
  for (const eventName of ['pull_request_target', 'workflow_run', 'repository_dispatch', '', null])
    assert.throws(() => profileForRun({ ...run(), eventName }), /Unsupported CI event/);
  for (const event of [null, [], { repository: repo('other/repository') }, { repository: { full_name: canonical } },
    { repository: { full_name: canonical, fork: 'false' } }])
    assert.throws(() => profileForRun({ ...run(), event }));
  for (const actor of ['', 'someone\nlicensed=true', null]) assert.throws(() => profileForRun({ ...run(), actor }));
  for (const mutate of [
    f => { f.event.pull_request = 'not-an-object'; },
    f => { f.event.pull_request.base = []; },
    f => { f.event.pull_request.head.repo = 5; },
    f => { f.event.pull_request.base.repo = repo('another/repository'); },
    f => { f.event.pull_request.base.repo.fork = true; },
    f => { f.event.pull_request.user.login = ''; },
    f => { f.event.pull_request.head.repo.fork = 'false'; },
    f => { f.event.pull_request.head.repo.full_name = 'invalid-repo-name'; },
    f => { f.event.pull_request.head.repo = null; f.event.pull_request.base.repo.full_name = 'another/repo'; }
  ]) { const fixture = pr(); mutate(fixture); assert.throws(() => profileForRun(fixture)); }
  assert.throws(() => profileForRun(run('workflow_dispatch', { inputs: [] })), /must be an object/);
});

function cli(fixture, rawEvent, paths = true) {
  const parent = resolve(tmpdir());
  const root = mkdtempSync(join(parent, 'statsdirect-ci-profile-'));
  const eventPath = join(root, 'event.json'), output = join(root, 'output'), summary = join(root, 'summary');
  writeFileSync(eventPath, rawEvent ?? JSON.stringify(fixture.event));
  const sentinel = 'TEST_LICENCE_MUST_NOT_BE_READ_OR_LOGGED';
  const env = { ...process.env, GITHUB_EVENT_NAME: fixture.eventName, GITHUB_EVENT_PATH: eventPath,
    GITHUB_REPOSITORY: fixture.repository, GITHUB_ACTOR: fixture.actor,
    GITHUB_OUTPUT: paths ? output : '', GITHUB_STEP_SUMMARY: paths ? summary : '',
    SPREADSHEETGEAR_LICENSE_STRING: sentinel };
  try {
    const result = spawnSync(process.execPath, [fileURLToPath(new URL('./profile.mjs', import.meta.url))], { env, encoding: 'utf8' });
    const read = path => { try { return readFileSync(path, 'utf8'); } catch { return ''; } };
    const content = { status: result.status, stdout: result.stdout, stderr: result.stderr, output: read(output), summary: read(summary) };
    assert.ok(!JSON.stringify(content).includes(sentinel));
    return content;
  } finally {
    assert.equal(dirname(root), parent);
    assert.ok(basename(root).startsWith('statsdirect-ci-profile-'));
    rmSync(root, { recursive: true, force: true });
  }
}

test('CLI writes exact outputs and clearly distinguishes actual coverage', () => {
  const headless = cli(pr(repo('someone/statsdirect', true)));
  assert.equal(headless.status, 0, headless.stderr);
  assert.equal(headless.output, 'licensed=false\n');
  assert.match(headless.summary, /Headless checks remain required/);
  assert.match(headless.summary, /licensed native-grid and UI checks are omitted/);
  const trusted = cli(run());
  assert.equal(trusted.status, 0, trusted.stderr);
  assert.equal(trusted.output, 'licensed=true\n');
  assert.match(trusted.summary, /does not obtain or verify credentials/);
  assert.equal(cli(run('workflow_dispatch', { inputs: { unlicensed: 'true' } })).output, 'licensed=false\n');
  assert.equal(cli(pr(repo(), 'dependabot[bot]')).output, 'licensed=false\n');
  for (const raw of ['', ' \t\r\n', '{}', 'null', '{"repository":null}', '{"repository":{"full_name":"iain-buchan/statsdirect","fork":false},"pull_request":{"head":null}}']) {
    const partial = cli(pr(), raw);
    assert.equal(partial.status, 0, partial.stderr);
    assert.equal(partial.output, 'licensed=false\n');
    assert.match(partial.summary, /metadata is unavailable/);
  }
});

test('CLI failures emit no licensed output or raw malformed event content', () => {
  for (const result of [cli({ ...run(), eventName: 'pull_request_target' }), cli(run(), ''), cli(run(), '{not-json:PRIVATE_EVENT_SENTINEL'), cli(pr(), '{not-json:PRIVATE_EVENT_SENTINEL'), cli(run('workflow_dispatch', { inputs: { unlicensed: true } }, 'contributor')), cli(run(), undefined, false)]) {
    assert.notEqual(result.status, 0);
    assert.equal(result.output, '');
    assert.equal(result.summary, '');
    assert.ok(!result.stderr.includes('PRIVATE_EVENT_SENTINEL'));
  }
});
