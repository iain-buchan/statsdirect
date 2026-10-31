# Production Windows shell checks

31 October 2026 menu simplification: **211 shell checks pass** with `STATSDIRECT_SKIP_CLIPBOARD=1`. The actual template menus retain their order without Tools; Calculator has a separate Analysis section before Options and reopens the same pane with no document, a worksheet, a report or an R session. Data, range selection, report content, calculator state and unexecuted R code are preserved. File > New R session and Help > Check for Updates remain available. The isolated application build succeeds with the existing NU1701 warning. The released checkout, version and installers are unchanged.

`NotebookOpening.cs` exercises the actual File > Open `.ipynb` dispatcher. It checks current report/code restoration, typed data in new worksheets, no automatic R execution, new unsaved documents without notebook save paths, the unchanged original file, and rollback of a failed import while existing work stays open. The full shell suite passes **183 checks** with STATSDIRECT_SKIP_CLIPBOARD=1, including tab names and strict UTF-8 validation; the export totals below describe the earlier run.

24 October 2026 notebook export: **170 shell checks pass** with `STATSDIRECT_SKIP_CLIPBOARD=1`, including the menu, file-state and atomic-export checks below. No installer was rebuilt.

`NotebookExport.cs` covers the merged report File menu's single **Export Jupyter notebook...** command, the `.ipynb` dialog, and retained Word/PDF Save As choices. It checks export of current unsaved narrative, preservation of the HTML source, title, save path and dirty state, refusal to overwrite the source or replace an existing notebook when required data are missing, and temporary-file cleanup. These checks belong to the shell run below; no new full-suite total is claimed here.

17 October 2026 decoded chart images: **285 checks pass**. Native Report
Copy/Cut supplies SVG and standard image representations, and SVG/PNG/bitmap
paste retains one-step undo/redo. The fixture verifies that the actual
Ctrl+Z/Ctrl+Y events reach the report and are handled; failures retain keyboard,
focus and document diagnostics. The report JavaScript bundles await image
decoding before preparing PNGs.

16 October 2026 R SVG chart isolation: **80 checks pass**. Native browser captures
verify that charts with deliberately conflicting glyph, clipping, gradient and
CSS definitions retain their own shapes and colours, including a repeated chart.
The original inline implementation fails this regression. SVGs now display as
separate vector image documents and retain their size, aspect ratio and original
file links. The real R chart tests cover multiple SVG pages, PNG and PDF output;
any pre-existing output files are restored byte-for-byte with their timestamps.
Set `STATSDIRECT_SVG_ARTIFACTS` to keep the browser captures in a chosen folder.
The Release build succeeds with the existing NU1701 and CS0252 warnings.

16 October 2026 startup logo and report closing: **73 checks pass**, with UI-thread
exceptions captured and failing the run. The real loading window stays visible
during main-window construction, Load and earlier Shown handlers, then closes
after the main window is visible. The splash remains alive through launch-file
loading in production. The broader checks exposed a pre-existing disposed-icon
error when the last report closed, reproduced without the splash change; assigning
the application icon after creating the report's native MDI window fixes it.
Report fixtures now await the normal asynchronous close instead of disposing a
window while its browser snapshot is pending. The Release build succeeds with
the existing NU1701 and CS0252 warnings; no installer was rebuilt.

16 October 2026 report Save As formats: **70 checks pass**. The Save As dialog
offers HTML, Word and PDF with HTML selected by default. Tests save genuine DOCX
and PDF files from both new and previously saved reports, inspect Word's editable
table and values, and verify that exports retain the report title, HTML save path
and unsaved edits. A subsequent ordinary Save updates the HTML without touching
the exported Word file. Ordinary first Save and save-on-close keep the editable
HTML format. The Release application builds with the existing NU1701 and CS0252
warnings; no installer was rebuilt for this change.

16 October 2026 RTF startup regression: **53 checks pass**. The compiled
application's RTF importer is called before any spreadsheet import in a fresh
process, then the actual File > Open path displays accented and Unicode report
text, retains the imported file's name and Save As semantics, and saves/reopens
the converted HTML without changing the original RTF. The same new test against
the installed 5.2.0 application reproduced the reported `RtfPipe.TextEncoding`
failure, with Windows-1252 unavailable. The Release application builds with the
existing NU1701 and CS0252 warnings; no installer was rebuilt.

16 October 2026, later: **48 checks pass**. The script window's output pane is a WebView2 page, so the new
chart checks run a real R script that writes an SVG and a PNG, then read the page: both charts drawn (the SVG
as it is, the PNG as an image) after their names and before the list of files written, the SVG at its svg
element's size or the pane's width keeping its shape, and R's prompts in their own colour in a monospace face.
The Copy-from-output check selects on the page and reads the page's selection. Each needs R installed.

16 October 2026: **43 checks pass**, including the example-workbook tab regression.
After entering data in a blank workbook, the harness opens Help > Example workbook,
runs a real paired analysis from its Agreement sheet into the HTML report and returns
to the workbook. The example and original tab names, original data and installed
example file are preserved. Additional checks cover report-title changes, CSV import,
unnamed temporary workbooks and saved-file path tooltips. These use the actual MDI
tabs, SpreadsheetGear and WebView2; the test runner now includes their import/browser
runtime dependencies. The Release application builds; no installer was rebuilt.

12 October 2026 catch-up: all **28 shell checks** pass with an R script active for toolbar rendering. The RScript suite passes **163 checks**, including AI then R+, all eleven vector symbols and the single accessible Open in R control. Native Learning separately verifies the final Help ordering: Help after Window, updates in Help, Example workbook last below a separator.

12 October 2026: **28 checks pass** with the common document toolbar. They cover worksheet and script undo/redo, one-click script Save, Find, native More overflow, the language selector, output selection retained through More, switching document toolbars, and calculator focus/undo routing. `STATSDIRECT_TOOLBAR_ARTIFACTS` optionally selects a directory for rendered grid/script toolbar images. The separate R script-window checks pass 22 assertions, including the labelled Run button starting the existing R workflow and print/Find/Help behaviour. No installer or release was created.

Build the Release application, then run on Windows:

```powershell
dotnet run --project tests/WindowsShell -c Release -p:StatsDirectBin=C:\path\to\application
```

The harness loads the compiled application and its full payload. Supply the
application folder extracted from an MSI to check its resources and assemblies.
It uses the actual main MDI form, SpreadsheetGear worksheet, calculator and
merged Edit menu, including the menu activation boundary that temporarily
takes input focus. It checks the legacy `-calculator` startup command, undo/redo,
worksheet selection, floating/redocking and editing after the last worksheet
closes. It never changes the system clipboard.

On 10 October 2026, all ten checks passed against the final Release MSI's
extracted application. Hash comparisons verified all 11 report-editor assets,
979 help files, 278 operation files and 151 templates against the tested source,
and the application/WebView2 binaries against the Release build. The offline
bundle contained that MSI and the verified, Microsoft-signed .NET and WebView2
runtime installers. Its .NET arguments were corrected to `/install /quiet
/norestart` (see [Microsoft's installer options](https://learn.microsoft.com/en-us/dotnet/core/install/windows#command-line-options)); the executable name belongs to the payload, not its arguments.

The calculator suite passed 72 checks at both 96 and 240 DPI; HTML5 Help passed
41, WebReports 110, installed Word/Excel 18, update download/dialog checks 20,
and upstream report JavaScript checks six. Release application/MSI/EXE builds
passed with existing NU1701 and same-version upgrade ICE61 warnings. No clean
installation/upgrade, Windows 10 VM, PowerPoint or physical-printer acceptance
was performed on this development PC.

The fixture excludes normal startup/update dialogs, IPC and user settings
writes. It creates and disposes only its own off-screen forms and synthetic
worksheet. It is not an installer upgrade or physical keyboard acceptance test.
