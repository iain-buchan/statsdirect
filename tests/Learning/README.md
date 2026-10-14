# Windows Learning integration checks

14 October 2026 recovery/R-context parity: **217 checks pass**, plus the shared
runner's **27** and WindowsShell's **32**. `Recovery.cs` terminates four isolated
WebView2 renderers and checks native history/draft recovery, continued partial
replies, exactly-once completion, security settings, one working R bridge,
closed-window partial retention and resumed conversation without resending.
`LearningReplay.cs` executes full PEFR and follow-up scripts in real R, checks
source snapshots beyond the AI excerpt limit, dynamic worksheet reads with blanks
and quoted text, lesson isolation, old blocks, stale actions, persistence and
metadata exclusion from AI requests/record exports. Malformed local R metadata
must leave the saved portfolio untouched.

Set `STATSDIRECT_SKIP_CLIPBOARD=1` to leave the system clipboard untouched when
its contents cannot be safely preserved (for example, enhanced metafiles). This
skips seven physical copy checks explicitly; selection assertions still run.
The 217-check run used this option. Without it there are **224 checks**. Optional
official-runtime and R-installer-file probes are additional; neither ran here.

13 October 2026 selection/copy follow-up: **165 checks pass**. The previous
Learning bundle lost the selection on the next streamed chunk. `Selection.cs`
now checks paragraph-start/end boundaries, backwards and mixed-format selections,
deliberate paragraph breaks, paused following and completion with viewport
clamping. It uses Chromium's actual Copy command and reads Windows clipboard
text, temporarily preserving/restoring the previous clipboard contents.
The shared Learning/R runner also passes all **27 tests**. No live AI request
is made; the optional runtime and installer probes are omitted.

13 October 2026 AI-to-R follow-up: **152 checks pass** with the optional official
runtime and R-installer probes omitted. `ConversationR.cs` tests the actual bottom
and per-block Open in R actions and runs the user's PEFR code in a fresh real R
process, including plots, paired t, signed-rank, sign and log-ratio analyses.
Source snapshots survive later edits; follow-ups retain earlier definitions;
older replies exclude later code; new conversations do not inherit old data.
Opening code never runs it. Worksheet exports preserve the values actually read,
column indices, missing positions and escaped text without rereading edited data.
The AI reply is mocked, while all exported R is executed in isolated test folders.
The final run uses the application extracted from the signed MSI and includes
opening R blocks from the user's own questions as well as assistant replies.

13 October 2026 release fixes: **137 checks pass**, including five new diagnostic
privacy checks. A synthetic malformed protocol response and unknown method name
must never appear in the diagnostic log; useful error type/message length remains.
Legacy raw-response diagnostics are removed while new metadata-only logs survive
restarts. No real account or model request is used. Streaming tests now explicitly
clear the selection before asserting that following resumes at the bottom.

12 October 2026 catch-up: **131 native checks pass**, including Help after Window,
Example workbook in its own section before About StatsDirect, no duplicated update command, and clearing
the obsolete sharing reminder after acknowledgment. The eleven streamed-reply
selection, scroll and focus regression checks remain green.

These checks use real WinForms, SpreadsheetGear and WebView2 controls. ChatGPT
conversation and dynamic-tool messages use `mock-server.mjs`, with no model
requests. Optional runtime checks start the official component under a fresh
temporary CODEX_HOME and read account status only. R analyses use installed R.

First-use setup follow-up: **131 checks pass** on `app-ai-ondemand`, including a
missing-runtime account refresh that performs no download or startup. The optional
R-installer-file check was not supplied in this run. Download/install and setup
dialog checks are in [TutorRuntime](../TutorRuntime/README.md): 40 pass including
the optional real GitHub download. The application no longer bundles its runtime.

Build StatsDirect first, then run from the repository root:

```powershell
$env:STATSDIRECT_TEST_NODE = (Get-Command node).Source
$env:STATSDIRECT_LEARNING_ARTIFACTS = 'C:\path\to\test-artifacts'
$env:STATSDIRECT_TEST_TUTOR_RUNTIME = "$PWD\DownloadedBinaries\TutorRuntime\bin\codex-app-server.exe" # optional prepare-runtime.ps1 fixture
# Optional: verify an already downloaded, pinned official R installer; never install it.
$env:STATSDIRECT_TEST_R_INSTALLER = 'C:\path\to\R-4.6.1-win.exe'
dotnet run --project tests/Learning -c Release -p:StatsDirectBin=C:\path\to\app
$env:STATSDIRECT_RSCRIPT = 'C:\Program Files\R\R-4.6.1\bin\Rscript.exe'
node tests/Learning/run-shared.mjs
```

The shared runner copies the exact vendored modules/tests into a uniquely owned
temporary folder, uses Windows' real help/operation files, and adapts only the
hard-coded R executable and multiline `-e` invocation. It does not alter the
vendored source or lesson content. All twelve lesson R scripts execute, create
plots and pass the upstream numerical assertions; provider/assessment, legacy
record, consent history and course tests are included.

The native suite covers menus, blank startup, document preservation, persistence,
courses, bounded worksheet reads, per-question consent, isolated sign-in protocol,
streaming/error/cancel/retry, tool boundaries, native Script AI controls, five
exports and an unsent email draft. It exercises all ten direct engine operations,
PEFR paired t/agreement and real R replay, duplicate calls, missing-row alignment,
table orientation/design, invalid/stale/unsupported/unapproved/cancelled requests.
R setup uses injected download/launch actions to verify modal lifetime, progress,
cancel/retry, readiness/resume and late-close callbacks without installing R.
Corrupted nested learning records must remain byte-for-byte unchanged.
Help > Example workbook opens an editable unsaved copy and preserves the installed
file. Script AI checks verify that sharing and question entry precede the replies,
including focus/draft preservation across dock/pop-out and wrapped confirmation
text with vertical scrolling in small windows. Rendered pane states are saved
with the other artifacts for visual review.
Streaming checks reproduce successive reply chunks in the actual native pane.
They cover preserved selection/reading position/focus, bottom following,
unchanged layout, identical completions, long replies, corrected/trimmed tails
and clearing for a new conversation.

Tests use temporary learning stores and fictional data. Windows shell/report
checks are separate; run UI suites sequentially because they exercise focus.
See [the acceptance matrix](../../Learning/acceptance.md) for results and limits.
