# Windows Learning integration checks

12 October 2026 catch-up: **131 native checks pass**, including Help after Window,
Example workbook as the final Help item, no duplicated update command, and clearing
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
