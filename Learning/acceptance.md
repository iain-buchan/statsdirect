# Windows / Mac workflow acceptance — 13 October 2026

## AI-to-R context follow-up

The updated 5.2.0 candidate fixes Script AI's bottom Open R code and per-block Open
in R: each export contains the shared source snapshots, literal worksheet values
actually read and earlier R blocks through the selected reply/block. It does not
read current edits, execute code or broaden AI sharing. Local export metadata is
not included in the conversation text sent to ChatGPT. New conversations start
without earlier data; exported scripts label the replay sections for review.

**436 checks pass** against the application extracted from the signed MSI:
Learning 152, WebReports 252, WindowsShell 32. The Learning
run omits optional official-runtime and installer-file probes. New cases execute
the supplied PEFR example and follow-ups using real R 4.6.1, checking the original
nine pairs and t statistic, plots, signed-rank, sign and log-ratio code, edited
source isolation, per-block dependencies, old replies, new conversations, literal
worksheet snapshots, missing rows and escaped strings. ChatGPT is mocked, and
opening code is verified not to execute it. Evidence is in `outputs/ai-r-context-*`
and `work/ai-r-context` under the workspace used below. The earlier numerical,
runtime and updater results remain the baseline for those unchanged components.

The fresh signed EXE (298,673,648 bytes) and MSI (24,207,360 bytes) supersede the
earlier packages described below. All 1,703 application files match the signed
build, all five expected signatures validate and all three bundle payload hashes
match. The EXE is copied to `C:\temp\StatsDirectSetup.exe` after verification.
No installer was published by these checks. An actual
installation/upgrade and a live ChatGPT account request were not performed.

## Windows 5.2.0 release corrections

The local `main` checkout at `e344aa76`, with the corrections in this review,
passes **478 checks**: Learning/AI/R 137, WebReports 250, WindowsShell 32,
TutorRuntime 39 and UpdateDownloadRegression 20. Regression coverage includes
metadata-only disconnection diagnostics and removal of legacy raw diagnostic
files; preserved forward/backward selections in growing Markdown paragraphs;
pausing bottom-following while text is selected; and recovery after a real
isolated WebView renderer crash, with conversation replay and security settings
reapplied. The calculation engine and shared Mac editor were not changed.

Fresh `ReleaseWithSigning` EXE and MSI packages were built on 13 October 2026.
All **1,703 application files** extracted from the MSI match the signed build;
the EXE's embedded MSI, WebView2 installer and .NET installer match their source
payloads. Authenticode validation passes for both installers and all three signed
application binaries. The installed version is 5.2.0.0 and no TutorRuntime is
bundled. A further **169 checks** pass against the extracted signed MSI payload
(Learning 137 and WindowsShell 32).

Release artifacts are `ReleaseBuild/StatsDirectSetup.exe` (298,678,600 bytes)
and `ReleaseBuild/StatsDirect-x64.msi` (24,215,552 bytes). Build, suite and package
verification evidence is under
`C:\Users\ieb21\Documents\Codex\2026-09-19\po\outputs\release-5.2-fixed-*`.
The build retains the existing NU1701 and WiX packaging warnings, with no errors.
These packages were local release candidates during verification.
A clean-machine installation/upgrade and a live ChatGPT
account request were not performed. Runtime checks used the previously verified
cached official archive; no new live runtime download was needed.

## 12 October baseline and earlier follow-ups

Windows review branch: `feature/windows-unified-toolbars`, based on `575d7e58`.
Version remains 5.1.1. This development branch contains unreleased changes; the
review executable and validation below were prepared before committing them.
The earlier single-toolbar changes remain on this branch. No numerical engine,
operation definition or R recipe changed. The latest catch-up also ports five
shared report-editor modules and rebuilds all three bundles; provenance and
Windows adaptations are recorded in `ReportEditor/chart-alignment-port.md`.

Windows review executable:
`C:\Users\ieb21\Documents\Codex\2026-09-19\po\work\windows-parity\app-mac-catchup\StatsDirect.exe`

## First-use AI installation follow-up

The next Windows review build is
`C:\Users\ieb21\Documents\Codex\2026-09-19\po\work\windows-parity\app-ai-ondemand\StatsDirect.exe`.
The Release build passes with the existing NU1701 warning. The output contains
OpenAI licence/notice assets and no TutorRuntime directory. The project no longer
requires or copies a runtime; the MSI explicitly excludes old TutorRuntime trees
left in incremental output. No installer or release has been built for this follow-up.

**40 first-use runtime checks pass**, including an actual 123 MB GitHub/CDN download,
archive/file SHA-256 checks, OpenAI publisher verification, installation, offline
reuse, corrupt-install repair, cancellation/cleanup, malformed archive rejection,
cross-process lock cancellation and native modal progress/error/retry/close.
The progress dialog was rendered and inspected at 240 DPI; Cancel remains visible.
**131 native Learning/AI/R checks pass** on the changed application, including the
new missing-runtime check proving account refresh performs no download/startup.
The official runtime also starts with isolated credentials and no inherited login.
No live account sign-in or model request was made. Clean-machine installer/upgrade
acceptance remains a release check; the tests use isolated temporary runtime roots.

Evidence: `outputs/ai-ondemand-build.txt`, `outputs/ai-ondemand-runtime-tests.txt`,
`outputs/ai-ondemand-learning-tests.txt`, and
`work/windows-parity/ai-runtime-checks/ai-setup-progress.png` under the same workspace.
The earlier results below remain the baseline for unchanged report/engine workflows.

## Automated results

| Area | Windows evidence | Result |
|---|---|---|
| Release build | .NET 10 / WinForms / WebView2 build; runtime and third-party notices present | Pass; existing NU1701 PresentationFramework compatibility warning |
| Main workspace and common toolbar | WindowsShell, actual MDI/grid/calculator/focus/undo/overflow | 28 checks pass |
| Startup | New launch creates one blank sheet; existing document preserved; no compulsory chooser | Pass in native Learning suite |
| Learning and provider courses | Help menu, 12 built-in lessons, provider pack, options/persistence, imports, assessment record exports | Included in 131 native checks, all pass |
| Example workbook | Help menu opens an editable unsaved copy; all supplied worksheets retained; installed workbook unchanged | Pass |
| Shared lesson content | Exact vendored lesson/question/provider tests with Windows help/definitions and real R | 27 tests pass, including all lesson R scripts and numerical assertions |
| Per-question document sharing | Off by default; acknowledgment/consent record before access; bounded literal worksheet reads; stale/unapproved rejection | Pass |
| Native Script AI | Sharing, question and Send/Stop above conversation; dock/popout, collapse/focus, wrapping/scrolling at high DPI, draft preservation, reset for next question, read-only tools | Pass; no live account question |
| ChatGPT transport | Mocked account/sign-in, text streaming, errors, cancellation/retry; rejects wrong thread/namespace/tool/duplicate call ID | Pass |
| Streamed conversation display | Incremental text, selected text/reading position preserved, bottom following, unchanged pane/focus, long history, final corrections and clearing | 11 regression checks pass |
| Official runtime | Pinned archive hash + OpenAI Authenticode signature; isolated strict-config startup and account/read | Pass, no inherited account or model request |
| Direct analysis | All ten supported operations through native TemplateProcessor; real report output and run records | Pass |
| Paired t and agreement | Nine-pair PEFR: t = 4.9257744860354, n = 9; SVG agreement charts; real Continue in R replay | Pass; engine section headings appear once |
| Data integrity | Missing rows remain paired; exact 2 × 2 orientation and explicit design; no headings silently removed | Pass |
| Publication boundary | Cached duplicates append once; cancelling or editing during computation prevents publication; failed queued append does not block later report edits | Pass |
| Report editing/copy/export | WebReports with toolbar, styling, undo/deletion, SVG/raster alignment, clipboard payloads, HTML/PDF/DOCX and reopen | 217 checks pass; eight shared JavaScript tests also pass |
| Continue in R and follow-ons | Existing complete RScript suite, real engine examples, options/confidence, regression follow-ons, nested/grouped frames, moved-column provenance and script window | 163 checks pass; all 227 manifest recipes agree with help examples |
| R setup | Official R installer hash/publisher verified; actual installed R readiness; injected modal installer lifecycle/cancel/retry/resume | Pass; no actual installation performed |
| Record recovery | Shared validator rejects corrupted nested portfolio; native host stops opening Options and preserves original bytes | Pass |

Windows native Learning suite: **131 checks pass**. Tests use fictional data and
temporary learning storage. RScript checks preserved pre-existing user script
files. The current Learning assets carry the exact copied source lock and licences.

Logs are under
`C:\Users\ieb21\Documents\Codex\2026-09-19\po\outputs\`:
`windows-catchup-build.txt`, `windows-catchup-learning.txt`,
`windows-catchup-shell.txt`, `windows-catchup-reports.txt`,
`windows-catchup-rscript.txt`, `windows-catchup-office.txt`,
`windows-catchup-editor-js.txt`, `windows-catchup-pdf.txt`.
The unchanged shared lesson content's earlier 27-test run is recorded in
`windows-parity-shared-lessons.txt`.
Learning screenshots, exports and the PEFR R script/output are in
`work\windows-parity\catchup-learning-checks`; report visual/export evidence is in
`work\windows-parity\catchup-report-checks` beneath the same workspace.

The 12 October layout follow-up adds 14 native checks for Help > Example workbook
and the Script AI question flow. Rendered expanded, confirmed/reply, floating and
small-window states were inspected at 240 DPI. Long confirmation labels wrap;
short windows scroll to keep both entry controls and replies reachable. Shared
lesson assets and calculation code were not changed by this follow-up.

The streaming follow-up reproduces the reported jump: replacing all transcript
text and forcing ScrollToCaret on every chunk discarded the selection and moved
an earlier reading position to the bottom. ConversationTextBox now changes the
new tail with redraw paused, preserves selection/first visible line, and follows
the bottom only when the reader was already there without selected text.
Repeated identical completion text does nothing. Eleven new checks cover these
cases, 4,000-line replies, trimming/correction and starting a new conversation.
That fix first went into `app-stable-chat`; it is also in the current
`app-mac-catchup` build. Save/close any previous review app before opening this one.

## Latest Windows catch-up

Help remains after Window. Its final order is Learning, Learning Options,
separator, Contents and Index, Method Selection, separator, Check for Updates,
separator, **Example workbook**, separator, About StatsDirect. The instruction was
Example workbook before About, with About last as on the Mac; an earlier reading
that put Example workbook last was corrected on 13 October 2026. Updates
uses the existing Windows implementation, including R checking; no Mac Sparkle
option was invented for Windows. Custom Tools and Calculator retain their order.

The Script toolbar ends with Help, AI and R+. AI/R+ use the Mac vector paths and
Windows system foreground/disabled colours. Open in R remains the accessible,
tooltip and overflow label and occurs once. Native rendered icons were reviewed.
The existing sharing/question/history flow and streaming stability remain;
acknowledgment now clears a stale confirmation reminder before focusing entry.

New SVG and raster charts start left aligned. A single selected picture accepts
Left/Centre/Right, reports its alignment on the toolbar and disables Justify.
Text, mixed selections and multiple-picture selections retain their text editing
behaviour. Tests cover undo/redo, no-op commands, keyboard/toolbar focus, resizing
from the free corner, Escape cancellation, Fit, clipboard and HTML reopening.

The report suite now passes **217 checks**. Word XML and actual installed Word
retain all four fixture-picture alignments in both original and reopened exports.
The Office suite passes **23 checks**, including physical Word/Excel paste and
the right-aligned SVG-to-PNG clipboard fallback at its correct size. The rendered
PDF positions and dimensions pass a pixel check and were visually reviewed.

Testing corrected root margins leaking into standalone SVG, Windows import
defaults overriding incoming alignment (including imported report wrappers), and
a focused report outline appearing in PDF. All five shared modules have identical
SHA-256 hashes to the final Mac source after normalising line endings. Windows
import/default/print changes remain in the host adapter/styles. Source provenance
is in `ReportEditor/chart-alignment-port.md`.

These checks preceded the user's request to commit and push the development
branch in Iain Buchan's name. No installer or public release was made. The
running application and released Windows build were left untouched.

## Mac coordination

The “StatsDirect for MacOS” chat reported its refreshed review build at
`.build/cross-platform-final/StatsDirect.app`: strict signature/debug-symbol
checks and 1,552 distribution checks pass, with its previously reported native
learning/provider/consent, 37 script AI groups, paired/agreement + real R,
101 parent/follow-on paths (67 operations), 49 report groups, 20 toolbar groups,
startup/recent/Finder and missing-R checks green. That chat also confirmed the
learning report's paired/agreement headings appear once with Creole colours.
These Mac results were reported by that session, not rerun on Windows.

The implementations retain their native window controls but share the lesson
content, portfolio/course schemas, tool contract/whitelist, default consent
behaviour, report records and R recipes. Windows' modal setup minimisation fix
is specific to WinForms and does not require a Mac change.

## Before a public release

- Exercise real personal ChatGPT sign-in, one learning question and one script
  question on both platforms. Automated tests used the real runtime only for
  unauthenticated startup/account status, and a mocked provider for questions.
- Install on clean supported Windows 10/11 machines; test missing R, elevation,
  installer cancellation, detection and resumption of both Run and Open in R.
  The native OS installer itself was not run on this development PC.
- Build/inspect the MSI and offline setup after the additional learning/runtime
  payload; test upgrade and uninstall. No installer or release was produced here.
- Test PowerPoint and additional Office versions with representative user files.
  Installed Word/Excel paste and Word opening of exported files pass on this PC;
  complete file-association/upgrade and physical-printer tests remain outstanding.
- Windows 10 compatibility is unchanged at the source/framework level; a clean
  Windows 10 runtime check remains outstanding. No Windows 10 VM was used here.

Known scope limits: AI worksheet reads are capped at 4,000 cells / 32 columns;
this does not limit the data grid. Non-supported analyses open the normal form.
Course PDF import extracts searchable text, not OCR. Mail drafts depend on the
user's `.eml` handler; the same TXT/JSON files remain available for attachment.
