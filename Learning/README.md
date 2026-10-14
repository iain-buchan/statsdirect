# Windows Learning and AI host

This unreleased Windows host reuses the Mac Learning interface and content. It
adds Help > Learning / Learning Options, provider courses, local learning records,
assessment exports, a dockable R-script assistant, and guided R installation.
The existing single document toolbar is retained. Ordinary startup opens a blank
worksheet; a supplied file remains the startup document. No calculation algorithm
or released version number changes here. The later chart-alignment catch-up is
documented separately in `ReportEditor/chart-alignment-port.md`.

## Source and redistribution

The source is `iain-buchan/statsdirect-mac`, commit
`1f4d920be36081a6e9121d83933b6356ed6ece2d` (MIT).
`upstream/Learn` and `upstream/Docs/Learn/bank.mjs` are copied unchanged. The original
`Content/Learn`, `Content/Tutor` and `Content/Brand` assets are under
`StatsDirectUI/Assets`. The Windows host supplies the bridge and adjusts platform
wording. `upstream.lock.json` records SHA-256 hashes of the original copied files;
its `windowsAdaptations` entries separately record successive Windows ports.
`.gitattributes` prevents Git's line-ending
conversion from changing those vendored bytes. Preserve the MIT notice and the bundled
`THIRD-PARTY-LICENSES.txt` for DOMPurify, KaTeX and highlight.js.

For a content update, build and review the Mac source at a recorded commit, copy
these same directories, update the lock, and run the Windows checks. The upstream
build uses its pinned Learn dependencies and the Grid esbuild dependency; it does
not need to run when compiling the Windows application from checked-in assets.

The Windows Learning bundle now includes the selection/reading-position module
from Mac commit `0a112baeae9f799b10685ada30f6dd442aec68c5`, copied unchanged as
`conversation-view.mjs`. `selection-port.mjs` applies a narrow, guarded adaptation
to the recorded app source at build time: stable message IDs and selection
capture/restoration around streamed and completed replies. `recovery-port.mjs`
adds the recovery and R-context bridge behaviour from Mac `cbccc10b591c85e34c6cc9faee9397e7aeb2e017`.
Lesson content and dependency versions match the original
bundle. To rebuild it, run `pnpm --dir Learning install --frozen-lockfile` and
`node Learning/build.mjs` from the repository root. Commit the generated
`StatsDirectUI/Assets/Learn/app.js` and refresh the adaptation hashes when changing
the port. The build fails if its expected source fragments no longer match;
review the adaptation explicitly when updating the upstream baseline.

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

Searchable PDF course import uses unmodified PdfPig 0.1.16, Apache-2.0, source
commit `a7bb35662bbbf405efddad50aedc9bcdcf515afc`. Its full licence and included
font notices ship in `Assets/Licenses/PdfPig`. OCR is not included.

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

R setup pins CRAN's R 4.6.1 Windows installer and SHA-256, verifies its Windows
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
Setting DWORD `DisableOnlineTutor=1` in HKLM or HKCU
`Software\Policies\StatsDirect` disables the online tutor; local learning remains.

Every Send includes the question and bounded conversation history. Learning also
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
