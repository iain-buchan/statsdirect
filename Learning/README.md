# Windows Learning and AI host

This Windows host reuses the Mac Learning interface and content. It
adds Help > Learning / Learning Options, provider courses, local learning records,
assessment exports, dockable script/report assistants, and guided R installation.
The existing single document toolbar is retained. Ordinary startup opens a blank
worksheet; a supplied file remains the startup document. No calculation algorithm
or released version number changes here. The later chart-alignment catch-up is
documented separately in `ReportEditor/chart-alignment-port.md`.

## Source and redistribution

The Learning page and its modules are the shared package `Learning/` of
`iain-buchan/statisticalhelp` (MIT), taken as the Git submodule `Learning/Shared`
and pinned by `Learning/upstream-manifest.json` (repository, full commit, its
`learning-v*` tag, the host contract number and the SHA-256 of every tracked file
under the package). The Mac application pins the same package in the same way; the
Help Editor publishes the lessons, questions and pathways separately. The original
`Content/Learn`, `Content/Tutor` and `Content/Brand` assets remain under
`StatsDirectUI/Assets`. Preserve the MIT notice and the bundled
`THIRD-PARTY-LICENSES.txt` for DOMPurify, KaTeX, marked and highlight.js.

`Learning/verify-upstream.mjs --verify` (run by `build.mjs` before every bundle, and by
the checks) confirms that the submodule's HEAD, the manifest and the repository's gitlink
agree, that the pinned commit carries one `learning-v*` tag, that every package file has
its recorded hash and none is modified or untracked, that `host-contract.json`, the
manifest and `LearningView.HostContract` agree, and that `Learning/package.json` carries
the package's dependencies at the same versions. The bundle must then carry that
contract number, which the page sends in `ready` and the host checks at load.

To move the pin: check out the new tagged commit in `Learning/Shared`, run
`node Learning/verify-upstream.mjs --write`, review the host contract (bump
`LearningView.HostContract` only when the package's number changed and the host
implements the new contract), rebuild, run the checks, and commit the gitlink with the
manifest. Learning code changes are made in `statisticalhelp/Learning`, never here.

The bundler remaps the two imports every host remaps: `../Content/Learn/lessons.json`
to the published lessons and `./bank.mjs` to the authored bank shim. The host registers
`window.statsDirectHost` (platform `windows`, `postMessage` through WebView2) before the
page runs. To rebuild, run `pnpm --dir Learning install --frozen-lockfile --ignore-scripts`
and `node Learning/build.mjs` from the repository root (a fresh clone first needs
`git submodule update --init Learning/Shared`), and commit the generated
`StatsDirectUI/Assets/Learn/app.js`. `conversation.mjs` builds the offline Script/Report AI
conversation bundle from the package's sanitized Markdown, KaTeX and R highlighting renderer.

Learning recovery keeps the native portfolio and latest reply event while a
failed WebView is unavailable. Reopen Learning installs a new restricted browser
and replays the existing reply without sending another question. Closing a failed
window keeps received partial text, marked incomplete. Bridge generations reject
callbacks from replaced browsers.

Learning's Open in R uses the same replay builder as Script AI, limited to the
chosen lesson and message/block. Approved full source snapshots and literal
worksheet reads are retained locally in the portfolio's Mac-compatible `rContexts`
metadata. They survive reopening, but are removed before restoring the web page
and excluded from tutor conversation text and learning-record exports. New
portfolios clear the context. Opening code never runs it or reads edited data.

## Windows/Mac parity, 14 October 2026

Report AI opens from the report toolbar (Help, AI, R) or a result's context menu.
Its fixed question snapshot includes displayed report text, focused-result text,
chart labels and bounded result metadata. Hidden run records, source paths and
original worksheet values are excluded unless displayed; separately selected
workbooks expose only bounded read tools. Script/Report AI cannot run analyses.
Large report context is read through the approved snapshot tool. C#/VB scripts
are labelled correctly and never included as source in Open in R.

Acceptance is held only in memory and shared by Script AI, Report AI and Learning.
Selecting a different data document revokes it and cancels active requests.
Changing report/script/Learning tabs, editing the same workbook, stopping a reply
or starting a new conversation does not revoke session acceptance. Edits still
invalidate that question's captured worksheet revision. Each Send retains its
own explicitly chosen document scope; Learning records each shared question,
including reused acceptance, with `document-sharing-v2` / `app-session`.

File Open now dispatches `.R` to the script editor, and CSV/TSV/TAB to data grids.
Delimited imports preserve quoted newlines, empty cells, leading-zero and long
identifiers, and literal formula-like strings. Ordinary numbers remain numeric.
They support Excel's full row limit and reject overflow explicitly. XLS/XLSX/XLSM
and Excel templates use SpreadsheetGear; XLSB imports cached values through
ExcelDataReader. Delimited files, XLSB and templates open as unsaved workbooks,
so Save cannot overwrite them with the wrong format.

RTF reports import through RtfPipe and the existing sanitized HTML/SVG path.
WMF/EMF charts become SVG; damaged pictures leave explicit placeholders. Imported
RTF reports save as HTML. MIT licences for RtfPipe and ExcelDataReader ship in
`Assets/Licenses` and are listed in the third-party notices.

RDS/RData/RDA use the Mac base-R exchange script with `--vanilla`. RDS saves the
current sheet; RData saves all sheets. Export uses row 1 for column names and
preserves imported types, factor levels/order, date precision and missing values.
Writes replace the destination only after R succeeds. Both hosts share the
Date/POSIXct NaN fix, retaining NaN distinctly from NA. Windows refuses R export
when edited imported columns, explicit row names or indistinguishable precise
timestamps make metadata mapping ambiguous; the workbook remains available for
Excel export or editing in R. Saving one sheet as RDS never marks a multi-sheet
workbook fully saved.

The changes retain Windows batch operations, C#/VB scripting, legacy XLS writing,
and grid/script printing. Help ends with Example workbook, Check for Updates,
and About StatsDirect. No numerical algorithm, release version or installer was
changed or rebuilt for this update.

Searchable PDF course import uses unmodified PdfPig 0.1.16, Apache-2.0, source
commit `a7bb35662bbbf405efddad50aedc9bcdcf515afc`. Its full licence and included
font notices ship in `Assets/Licenses/PdfPig`. OCR is not included.

## Authored built-in tutor content

The Help Editor can maintain the **main built-in tutor**, separately from optional
provider course packs. Its built-in publisher writes reviewed build inputs to
`Learning/AuthoredTutor`: `lessons.json`, `questions.json`, `tracks.json`, a static
`bank.mjs`, `manifest.json` and a short README. The manifest identifies the source
revision, bank version, file hashes/counts and the changed-item author reviews.
The original source snapshot has 12 lessons and 35 questions across five named
pathways, with shared `core` questions; publishing does not add `course:` IDs or
turn every question into `core`. The existing five pathway IDs must remain so
saved learning records stay compatible. Optional teaching/example packs are not
automatically added to the built-in content.

On a clean Windows x64 checkout, install Node.js and pnpm, then run
`pnpm --dir Learning install --frozen-lockfile --ignore-scripts` from the repository
root. Only `Learning/node_modules` is needed; do not install separately in the
pinned upstream copy. The frozen lock includes esbuild's matching Windows x64
optional executable package, which the real build uses without its postinstall
script. `--ignore-scripts` avoids pnpm's build-script approval error while keeping
the recorded dependency versions and integrity checks intact. The clean-install
check was run with Node 24.22.1 and pnpm 11.25.0.

`node Learning/build.mjs` validates an authored manifest before changing assets.
When present, its verified lesson and question data replace the corresponding
inputs to the normal Learning bundle. The native host's
`StatsDirectUI/Assets/Learn/lessons.json` is updated from the same validated
snapshot. The existing Windows selection/recovery adaptations and conversation
renderer remain in use. Both JavaScript bundles must build successfully before
the three generated assets are replaced. Missing, malformed, incomplete or
unreviewed authored content stops the build; it never silently selects older
checked-in material. Without an authored manifest, ordinary `npm run build` in
`Learning` retains the existing upstream-source route.

For the application build hook, `node Learning/build.mjs --authored-if-needed`
performs the same validation, then reuses generated assets only when their hashes
and the complete recorded build inputs still match. Its local receipt is
`Learning/obj/authored-tutor-build.json` (ignored by Git). Changing source,
adaptation scripts, dependency locks, a consumed dependency or any generated
output forces regeneration. A sibling `.AuthoredTutor.publishing` marker means
the editor has not finished promoting its source folder: builds stop until that
operation finishes. The helper never runs R, publishes a release or updates the
pinned `Learning/upstream` files or their historical provenance lock.

Authored builds also apply the guarded `authored-review-port.mjs` adaptation to
the pinned lesson guide. Its footer displays the lesson's recorded authorship,
review status, date and reviewer, escaped as text. It makes no default original
authorship or AI-review claim and shows draft status even without references.
An unexpected upstream template change stops the build for explicit review.

Run the ingestion checks with `node --test Learning/authored-tutor.test.mjs`.
Run the rendered review-label checks with `node --test Learning/authored-review-port.test.mjs`.
They use isolated fixtures and do not replace application assets. Existing shared
content/R checks remain important; a legitimate change to a worked example may
require an independently reviewed update to its expected numerical fixtures.
Neither a valid manifest nor an author-review label independently certifies the
statistical content, examination readiness or CPD accreditation.

`node --test Learning/authored-build.test.mjs` executes the real build in an
isolated temporary repository using the existing pinned local dependencies.
It checks content ingestion, pathway/question identity, invalid publication,
incremental rebuilding, compile failures and interrupted output replacement.
Source and dependency bytes are rechecked before promotion. A failed replacement
or receipt write restores the previous complete output using transient staging;
the staging files are removed after success or failure. No rollback build is kept.
To include the compiled browser MCQ/reload checks, set
`STATSDIRECT_PLAYWRIGHT_PATH` to an installed Playwright package and optionally
`STATSDIRECT_BROWSER_CHANNEL` (default `msedge`). The browser check is explicitly
skipped when the package path is absent. Set `STATSDIRECT_AUTHORED_TEST_EVIDENCE`
to retain the isolated build log and browser screenshots; generated test builds
are deleted. These checks never replace the checkout's application assets.

## First-use ChatGPT setup

Normal builds and installers do not bundle the 351 MB runtime and do not require
a preparatory download. On the first explicit **Use my ChatGPT** action, a setup
dialog automatically downloads the reviewed official OpenAI app-server 0.157.1
Windows x64 archive (122,840,599 bytes), shows download and verification progress,
and installs without administrator rights. Success closes setup before continuing
the existing account/sign-in flow. Cancel preserves the question and offers Retry;
closing the dialog waits for cleanup. Network/verification failures offer Retry or
Not now. Opening local lessons, Learning Options or the AI pane alone never downloads.

`UI/Learning/TutorRuntimeInstallation.cs` pins the archive SHA-256 and every
extracted file, restricts HTTPS redirects, bounds archive size and entries, rejects
unexpected paths/links/duplicates, and verifies the OpenAI Authenticode publisher.
Exact-tag Apache-2.0 LICENSE and NOTICE files ship as small application assets in
`Assets/Licenses/OpenAI`, with pinned hashes. Complete trees are atomically promoted
under `%LOCALAPPDATA%\StatsDirect\TutorRuntime\<version>-<hash>`; incomplete attempts
are removed. A file lock serialises setup across application processes. Existing
verified installations are reused offline, and corrupted files are repaired on
the next explicit setup attempt. Full file hashes are checked before process startup.
Account storage remains separately under `Learning\ChatGPT Tutor`.

The MSI explicitly excludes `TutorRuntime/**`, including stale copies in incremental
build folders. Old MSI-owned copies are handled by the normal major-upgrade removal.
The optional `./Learning/prepare-runtime.ps1` script still prepares an official local
fixture for tests; it is no longer a build prerequisite. Future runtime changes must
update and verify both the archive/file pins and exact-tag licence assets, then run
[the first-use checks](../tests/TutorRuntime/README.md). The package remains Windows
x64; Windows ARM64 uses x64 compatibility. No new Windows minimum is set.

R setup pins the CRAN Windows installer named in R/RInstallation.cs (currently R 4.6.2) and its SHA-256, verifies its Windows
publisher signature, downloads with cancellation/progress and starts the native
installer. It minimises the modal setup dialog while the installer runs: hiding
a modal WinForms form would end its caller prematurely. Installation is recognised
only after registry/binary discovery and a successful harmless Rscript probe.
Cancellation keeps the pending worksheet/script intact; Retry prepares a new
download. Run/Open in R resume only after readiness. Future R installer updates
must update and independently verify the pin in `R/RInstallation.cs`.

## Data, consent and local storage

Local records and provider course packs live under
`%LOCALAPPDATA%\StatsDirect\Learning`. Replaced packs are archived. Schema 2
portfolio records are validated by the shared UI before use; a failed restore
does not overwrite the source. Assessment TXT/JSON/HTML/PDF/DOCX exports use the
same portfolio schema. An email action creates an unsent `.eml` with TXT/JSON
attachments for the user's mail client; StatsDirect does not send mail.

The tutor uses the user's ChatGPT account and its Codex entitlement/allowance.
The runtime has a separate CODEX_HOME and working directory, with Windows
credential storage. Personal Codex configuration, API keys, plugins, filesystem
tools, shell commands, web search and arbitrary code execution are disabled.

### Windows administrator AI policy

Set the `AISecurityLevel` DWORD in `HKLM\Software\Policies\StatsDirect`
(64-bit registry view) before starting StatsDirect. It applies to Learning,
Script AI and Report AI.

| Value | Level | Behaviour |
| --- | --- | --- |
| `1` or absent | Unrestricted (default) | Online AI and the existing consent-based sharing controls are available. |
| `2` | Data sharing restricted | The tutor accepts the current question, with a bundled lesson where applicable and access to bundled method help. Workbooks, R source and console output, reports, imported course materials, learning preferences and earlier conversation stay local. Text typed or pasted into the question goes to OpenAI. |
| `3` | AI restricted | StatsDirect blocks its online AI connection, sign-in and requests. Local analysis, R, lessons, practice and help remain available. |

For example, to set level 2 from an administrator terminal:

```powershell
reg.exe add "HKLM\Software\Policies\StatsDirect" /v AISecurityLevel /t REG_DWORD /d 2 /reg:64 /f
```

An invalid level falls back to level 3. Existing nonzero `DisableAIDataSharing`
in HKLM and `DisableOnlineTutor` in HKLM or HKCU can only make the effective
policy stricter.

To require the Trust's ChatGPT Enterprise workspace at level 2, set the
`ChatGPTWorkspaceId` string (`REG_SZ`) under the same HKLM policy key to the
workspace's UUID. Deploy both values and restart StatsDirect.

```powershell
reg.exe add "HKLM\Software\Policies\StatsDirect" /v ChatGPTWorkspaceId /t REG_SZ /d "<ChatGPT workspace UUID>" /reg:64 /f
```

StatsDirect passes the restriction to its own ChatGPT process. That process
rejects new sign-ins and previously saved credentials for other workspaces,
including personal workspaces. The Trust controls membership and SSO in its
approved workspace. Machine-wide Codex requirements also apply to this process.
With the string absent, level 2 permits guidance using the normal ChatGPT sign-in.
A malformed value blocks the connection. Changing the effective workspace while
StatsDirect is open stops the existing connection from sending further requests
until StatsDirect restarts.

With standard sharing settings, every Send includes the question and bounded conversation history. Learning also
includes current learning preferences and bounded, cited course/lesson extracts.
Local identity and reviewer email are excluded from tutor context. Document
sharing starts off. Learning requires a fresh confirmation for each question,
records that confirmation before access, and ties worksheet access to revisions.
Script AI requires a fresh acknowledgment for its selected script/output/workbook
scope; collapsing the sharing panel does not grant another question access.
Previously sent conversation content can remain in later questions.

Worksheet reads are literal, not sampled: maximum 32 columns and 4,000 cells per
read, with 1-based rows and 0-based columns. These are AI context limits, not
worksheet size limits. Headings must be excluded explicitly. The source workbook
is never edited by AI tools. Script AI exposes only read/help tools; suggested R
code opens for review and is not executed automatically. Curated built-in lesson
R examples retain the app's explicit Run example behaviour.

## Direct Learning calculations

`statsdirect_run_analysis` accepts only a question-bound `datasetId` from an
approved read, an operation, and optional `agreement` / `studyType`. Supported
operations are TPaired, UnivariateSummary, Chi2by2, ExactFisher, Wilcoxon,
TUnpaired, Spearman, Agreement, Normality and QuickSummary. Other methods use the
normal analysis form. Values, file paths, expressions and executable code cannot
be supplied as tool arguments.

The native TemplateProcessor receives the unchanged operation definitions and
current confidence/format preferences. Numeric missing entries use the engine's
`Constant.MISSING`, retaining alignment for paired methods. Normality follows its
declared skip-missing mode. Tables require 2 × 2 whole counts and explicit study
design. Incomplete ranges, errors, manual-calculation/stale-formula status,
unsupported dimensions and nonnumeric text are rejected.

Calculation runs off the UI thread with cooperative engine cancellation. The
host checks consent, cancellation and the exact range again immediately before
publication. The report receives the engine HTML once, including its own section
headings, SVG charts and normal run record for Continue in R. Identical calls in
the same question share a cached task/result and append once. Native menu
analyses cannot overlap that calculation. Engine methods without frequent
progress callbacks can finish computing after cancellation, but cannot publish.

See [acceptance.md](acceptance.md) and [test instructions](../tests/Learning/README.md).
