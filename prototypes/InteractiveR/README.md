# Interactive R console prototype

This is a standalone Windows prototype on `prototype/interactive-r-console`, based on Windows source `a7836b49095e74031da3476d720cd2360ca11c0b`. It does not change the main application, its version, installed files, release tags or installers. It loads the installed R only into a disposable native child process. The Windows host does not load R.dll.

The Console and Script views share one persistent R workspace. Console output is read-only; commands can be recalled, edited and rerun. Real R prompts such as `readline()` and `menu(graphics=FALSE)` receive answers through a separate input-request channel. Incomplete commands use continuation input. Stop interrupts the current command and preserves existing variables; assignments and file writes that already happened are not rolled back. Restart R explicitly starts a new workspace.

The interface is shared with the Mac prototype (`Content/RConsole` in `statsdirect-mac`). Windows uses WebView2 and SVG plot previews; the Mac host uses WKWebView and PDF masters with previews. Existing report-chart SVG policy is unchanged. The prototype has its own WebView2 profile and session working directories under LocalAppData/StatsDirect/InteractiveRPrototype.

## Build and run

Use .NET 10, the WebView2 Runtime, installed 64-bit R with its development headers, and either the Visual Studio C++ tools or the portable compiler supported by `Native/build-native.ps1`.

```powershell
./Build-Prototype.ps1 -RHome 'C:\Program Files\R\R-4.6.1'
./bin/StatsDirect.InteractiveR.Prototype.exe
```

The native helper is built against the selected R headers and checks that the runtime version matches. Compiler downloads and build products stay ignored in this prototype directory. No installer is produced. `--r-home`, `--native-helper` and `--working-directory` can override the defaults. `--script path.R` explicitly executes that initial script automatically, providing a prototype of Continue in R. The ordinary blank console starts without executing a user script.

## Checks

The real-R checks exercise genuine interactive mode, prompt answers, Stop during prompts/calculation, native cleanup, variable preservation, multiline input, recovery after an error, output isolation, stale replies, shared Script state, plots and Restart. Every wait is bounded and observes a structured event.

```powershell
dotnet run --project ./Tests -c Release -- --native-helper ./bin/StatsDirect.RConsole.Native.exe --r-home 'C:\Program Files\R\R-4.6.1'
./bin/StatsDirect.InteractiveR.Prototype.exe --ui-smoke ./ui-results.json
```

The test programs write their actual results and event logs; a successful build alone does not establish that the checks passed.

## Deliberate prototype limits

- This is a separate test app, not a replacement for the released R script window. Its Help explains the prototype; AI integration is not connected.
- Console input comes through R's native console callbacks, including `readLines(stdin())`. Raw native/child process stdin sees NUL/EOF rather than protocol frames. Arbitrary terminal/TUI packages, native graphics interaction (`locator()`), and debugger integration need separate work.
- Ordinary SVG previews snapshot the graphics display list without closing the active device. Follow-on commands such as `abline()` can therefore update that plot. These temporary snapshots are presentation assets, not a new statistical calculation or a permanent R serialization format.
- Stop is cooperative. A native package which does not check for R interrupts may need Restart R. Closing the prototype or Restart terminates its helper. R code still has the normal permissions of the Windows user.
- Script saving saves source, not an automatic `.RData` workspace. R startup profiles and workspace restoration are disabled for this isolated prototype.

The native transport and its separation from user input/output are documented in `Native/README.md`.
