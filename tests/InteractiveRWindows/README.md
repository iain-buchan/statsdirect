# Integrated Windows interactive R checks

31 October 2026: the actual-application checks pass (the suite prints its count),
with no unhandled UI exceptions and all owned native R workers terminated. The
application selected R 4.6.2; complete SVG pages also matched independent ordinary
R renderings from the same installation. The thirteen shared/replay-model tests
pass under Node. This is an isolated application build, not a released installer
or an Office clipboard acceptance result.

This suite opens the actual StatsDirect MDI R script window and its WebView2
console, using an installed R through the native worker. It does not use the
standalone prototype host. Startup/settings/IPC handlers are detached from the
fixture's main window, and each R window writes into its own test directory.
No installer is built. The chart-viewer checks back up, overwrite (through the
viewer's Copy Chart) and restore the system clipboard; set `STATSDIRECT_SKIP_CLIPBOARD=1`
to skip them and leave the clipboard untouched.

An optional native Office probe is in `OfficeInterop.ps1`, invoked through the
WindowsShell `STATSDIRECT_PASTE_PROBE` hook while its fully materialised clipboard
backup is active. A saved PowerPoint package retained SVG, all nine point markers
and positioned label geometry within Office rounding; a separate Paste Special
PNG package retained the supplied raster fallback. Word automation did not
complete. It is opt-in with `STATSDIRECT_OFFICE_INCLUDE_WORD=1`; no Word acceptance
result is claimed. Never run Office acceptance against a user's open documents.

Build the application into an isolated folder first, then run:

```powershell
$env:STATSDIRECT_INTERACTIVE_R_ARTIFACTS = 'C:\path\to\owned-test-artifacts'
dotnet run --project tests/InteractiveRWindows -c Release -p:StatsDirectBin=C:\path\to\isolated-app
node --test tests/InteractiveRWindows/shared-console-ui.test.mjs tests/InteractiveRWindows/r-console-model.test.mjs
```

For physical keyboard/mouse acceptance, append `-- --manual` to the `dotnet run`
command. It opens a visible **StatsDirect — R console acceptance** fixture and an
empty R script through File > New R session, without running the automated suite.
R starts on first use and writes into the fixture's owned artifact directory.
Close the acceptance window when finished; startup/IPC/preferences handlers and
the fixture document's unsaved-source prompt are excluded.

The independent SVG comparison runs the newest R installed under Program Files;
set `STATSDIRECT_RHOME` to another installed R home. The application uses its
ordinary installed-R discovery.

The checks cover genuine prompt answers, Unicode, continuation, rejected input,
Stop during calculation/sleep/questions/continuation, R stack cleanup, retained
variables, Script/Console continuity, selection-only execution, File New/Open/Save,
immutable SVG revisions, complete paired-test output, multiple complete pages,
base/grid multipanels, device dimensions, explicit file outputs, large SVG Open
cards without previews, global-workspace clearing, Restart, stale input tokens,
independent windows, owned-worker cleanup, and the Rscript fallback through the raised-minimum hook
(STATSDIRECT_R_CONSOLE_MINIMUM: a notice naming Rscript, fresh R per command, stderr output, a chart card, an error). Actual unsaved-source close banners
exercise Cancel, Yes, an owned read-only-file save failure, and unused blank
sessions; file bytes, browser source, worker lifetime and dirty state are checked.
Ordinary R draws the
reference SVGs independently, with canonical comparisons that normalize SVG IDs.

The browser checks retain original chart-button DOM nodes across selection and
pointer/state updates, inspect exact plot/revision dispatch, verify prompt-wheel
routing, and exercise editing/undo/redo/find after native-menu focus. Synthetic
pointer/wheel events test event routing and DOM stability; they do not certify a
physical mouse action or Office clipboard interoperability. Those need separate
native acceptance checks.

The artifact folder retains event logs, SVG reference drawings, test-created
scripts/files and browser screenshots. All waits have deadlines and observe R or
browser state rather than relying on a fixed sleep to imply completion.
