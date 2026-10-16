# Production Windows shell checks

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
