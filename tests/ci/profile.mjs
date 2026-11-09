import { appendFile, readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const object = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const repoName = value => typeof value === 'string' && /^[a-z0-9-]+\/[a-z0-9_.-]+$/i.test(value);
const login = value => typeof value === 'string' && /^[a-z0-9-]+(?:\[bot\])?$/i.test(value);
const dependabot = value => /^dependabot(?:-preview)?(?:\[bot\])?$/i.test(value);

function repositoryInfo(value, label, partial = false) {
  if (partial && value == null) return {};
  if (!object(value)) throw new Error(`${label} must be a repository object.`);
  if ((value.full_name != null && !repoName(value.full_name)) ||
      (value.fork != null && typeof value.fork !== 'boolean'))
    throw new Error(`${label} has malformed repository metadata.`);
  if (!partial && (!repoName(value.full_name) || typeof value.fork !== 'boolean'))
    throw new Error(`${label} must contain a repository name and boolean fork flag.`);
  return value;
}

const completeRepo = value => repoName(value.full_name) && typeof value.fork === 'boolean';
function optionalObject(value, label) {
  if (value == null) return {};
  if (!object(value)) throw new Error(`${label} must be an object.`);
  return value;
}

// Eligibility for a licensed build is not possession of a licence. This selector
// never reads credentials; the normal trusted build must still require its key.
export function profileForRun({ eventName, event, repository, actor } = {}) {
  if (!['push', 'pull_request', 'workflow_dispatch'].includes(eventName))
    throw new Error('Unsupported CI event; refusing to select a licensed profile.');
  if (!repoName(repository) || !login(actor))
    throw new Error('CI event, repository and actor metadata are required.');
  const isPR = eventName === 'pull_request';
  if (isPR && event == null) event = {};
  if (!object(event)) throw new Error('CI event must be an object.');
  const current = repositoryInfo(event.repository, 'Event repository', isPR);
  if (current.full_name != null && current.full_name.toLowerCase() !== repository.toLowerCase())
    throw new Error('Event repository does not match GITHUB_REPOSITORY.');

  let author;
  let head;
  let missingPRMetadata = false;
  if (isPR) {
    const pr = optionalObject(event.pull_request, 'Pull request');
    const base = repositoryInfo(optionalObject(pr.base, 'Pull request base').repo, 'Pull request base', true);
    head = repositoryInfo(optionalObject(pr.head, 'Pull request head').repo, 'Pull request head', true);
    author = optionalObject(pr.user, 'Pull request author').login;
    if ((base.full_name != null && base.full_name.toLowerCase() !== repository.toLowerCase()) ||
        (base.fork != null && current.fork != null && base.fork !== current.fork) ||
        (author != null && !login(author)))
      throw new Error('Pull request base or author metadata is inconsistent.');
    missingPRMetadata = !completeRepo(current) || !completeRepo(base) || !completeRepo(head) || author == null;
  }

  let forced = false;
  if (eventName === 'workflow_dispatch' && event.inputs != null) {
    if (!object(event.inputs)) throw new Error('Manual inputs must be an object.');
    const value = event.inputs.unlicensed;
    if (value !== undefined && ![true, false, 'true', 'false'].includes(value))
      throw new Error('Manual unlicensed input must be true or false.');
    forced = value === true || value === 'true';
  }

  if (missingPRMetadata)
    return { licensed: false, reason: 'Pull request trust metadata is unavailable; use unlicensed checks.' };
  if (current.fork) return { licensed: false, reason: 'The workflow repository is a fork.' };
  if (dependabot(actor) || (author && dependabot(author)))
    return { licensed: false, reason: 'Dependabot runs use the unlicensed profile, including human reruns.' };
  if (head && (head.fork || head.full_name.toLowerCase() !== repository.toLowerCase()))
    return { licensed: false, reason: 'The pull request comes from another repository or a fork.' };
  if (forced) {
    if (actor.toLowerCase() !== repository.split('/')[0].toLowerCase())
      throw new Error('Only the repository owner can request a manual unlicensed run.');
    return { licensed: false, reason: 'The repository owner requested a manual unlicensed run.' };
  }
  return { licensed: true, reason: 'Repository CI is eligible for the licensed profile; a configured licence is still required.' };
}

async function main() {
  const { GITHUB_EVENT_NAME: eventName, GITHUB_EVENT_PATH: eventPath,
    GITHUB_REPOSITORY: repository, GITHUB_ACTOR: actor,
    GITHUB_OUTPUT: output, GITHUB_STEP_SUMMARY: summary } = process.env;
  if (!eventPath || !output || !summary) throw new Error('GitHub event and output file paths are required.');
  let event;
  try {
    const text = await readFile(eventPath, 'utf8');
    event = eventName === 'pull_request' && !text.trim() ? {} : JSON.parse(text);
  }
  catch { throw new Error('Could not read a JSON object from GITHUB_EVENT_PATH.'); }
  const profile = profileForRun({ eventName, event, repository, actor });
  const coverage = profile.licensed
    ? 'The normal build must supply its licence and pass the full configured checks. This selector does not obtain or verify credentials.'
    : 'No SpreadsheetGear licence is supplied. Headless checks remain required; licensed native-grid and UI checks are omitted and need a reviewed trusted run.';
  await appendFile(summary, `### Windows CI profile: ${profile.licensed ? 'licensed' : 'headless, no licence'}\n\n${profile.reason}\n\n${coverage}\n`, 'utf8');
  await appendFile(output, `licensed=${profile.licensed}\n`, 'utf8');
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().catch(error => { console.error(`Windows CI profile failed: ${error.message}`); process.exitCode = 1; });
}
