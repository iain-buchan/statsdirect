# Windows interactive R worker

`StatsDirect.RConsole.Native.exe` is a small native child process. It loads the
selected installed 64-bit R; StatsDirect and WebView2 never load R.dll. Each R
script window owns one persistent R workspace. Stop requests an R interrupt on
the worker's R thread; Restart or closing the window ends the worker process.

The evaluator uses R's parser and evaluator within a balanced `R_ToplevelExec`
error boundary. It does not call `R_ReplDLLdo1`: that DLL REPL resets R's
protection stack and cannot safely be nested within `R_ToplevelExec`.
A submission is parsed whole, as `source()` parses a file: the work is in
proportion to the text, and its expressions share one source reference. Only
text that does not parse whole is parsed one expression at a time, so a
complete prefix runs before an incomplete or invalid suffix. R's `wholeSrcref`
supplies the consumed physical line and Unicode column, including delimiters;
the worker maps that position back to the original UTF-8 input rather than
interpreting R syntax. The byte counter is unsuitable for this because it can
overcount multibyte input.
Errors and Stop discard the remaining submitted expressions while keeping
completed assignments. Normal printing, warnings, task callbacks and the base
`.Last.value` binding retain ordinary Console behaviour; its original binding
lock is restored through `R_ExecWithCleanup`.

The correction is verified against the installed R 4.6.2, and the worker against R 4.2.3 as well, by the checks below:
`test-native.py` (95 native checks, 96 in the checked build), `Tests/Program.cs`
(the production-session checks) and `tests/InteractiveRWindows`. A soak of fresh
start-ups and shutdowns, `test-native.py --startups N` against a checked build,
writes its result to `test-output/soak.json` beside the counts it reports.
R 4.6.1 was removed from this machine and has not been retested with the
correction. The source fix does not update existing installers.

The protocol, interrupt handling and display-list plot capture were first tested
in `prototypes/InteractiveR`. The production session adapter is now
`StatsDirectUI/R/Interactive/RConsoleSession.cs`. The shared web console is based on Mac commit
`cf008df1788fc1ff811988df3392bca57cb31a7b`, with the tested authoritative
source replay fix, display-only clearing and optional output-file links from
Windows commit `83a39468`. The Windows host adds native editing/clipboard/menu
adapters; the shared toolbar scrolls horizontally in a narrow MDI window.

Build the application project normally. Its build invokes `build-native.ps1` and
copies the helper beside StatsDirect.exe, including for `dotnet publish`.
Building requires R development headers plus the pinned portable Zig compiler
(`fetch-native-tools.ps1`), which the build prefers; Visual Studio's C++ compiler builds
the worker too when Zig is absent, with two compatibility flags the script passes for
R 4.6's headers (`msvc-rconfig.h`). R itself is not redistributed. The default
headers are taken from the latest installed R under Program Files; set
`RConsoleRHome` or `R_HOME` to choose another installation, of R 4.2 or later. The
worker takes any 64-bit R from 4.2.0, the minimum `console.c` states: every R entry
point is resolved by name when the worker starts, and R's start-up structure has been
versioned since 4.2.0 with its 64-bit layout unchanged through the headers the worker
is built with (the field R 4.4 added fills what was padding). R confirms the
structure version at start-up; an R below the minimum, a 32-bit R, an R that lacks an
entry point or one that refuses the structure version is turned away with a message
before any of it is used. The build stamp records the headers' version and the
minimum. Verified on R 4.6.2 and R 4.2.3 by the checks below. An installed R the worker
does not take runs each command through its Rscript instead (`RScriptSession.cs`, the
same page events, a notice saying so); the environment variable
`STATSDIRECT_R_CONSOLE_MINIMUM`, set to a series above any installed R, makes the window
take that path, which `tests/InteractiveRWindows` uses to check it.

```powershell
# Optional; downloads and checks the compiler, without installing it globally.
& .\RConsoleWorker\fetch-native-tools.ps1
dotnet build StatsDirectUI\StatsDirectUI.csproj -c Release
# Or use an existing compiler and explicit headers:
dotnet build StatsDirectUI\StatsDirectUI.csproj -c Release -p:RConsoleRHome='C:\Program Files\R\R-4.6.2' -p:RConsoleZig='C:\tools\zig.exe'
```

The build fingerprint includes the C source, manifest, build script and R headers.
Unchanged verified output is reused. Tools, object files, the helper executable,
build stamp and test output are ignored by Git. Application-only builds neither
sign the helper nor build installers. The release signing workflow must sign it
with the other application binaries when installers are eventually built.

Run the real-R native tests after building:

```powershell
python RConsoleWorker\test-native.py                    # the newest installed R; or name an R home first
dotnet run --project RConsoleWorker\Tests -c Release -- RConsoleWorker\bin\StatsDirect.RConsole.Native.exe 'C:\Program Files\R\R-4.6.2'
```

For the protection-stack regression, build a separate checked worker and require
its diagnostic markers. This test-only build verifies a nonnegative, unchanged
stack across every evaluation, including errors and interrupts. Normal builds
do not access the diagnostic stack export or emit these markers.

```powershell
& .\RConsoleWorker\build-native.ps1 -RHome 'C:\Program Files\R\R-4.6.2' -OutputDirectory C:\temp\RConsoleChecks -ProtectionChecks
python RConsoleWorker\test-native.py 'C:\Program Files\R\R-4.6.2' C:\temp\RConsoleChecks\StatsDirect.RConsole.Native.exe --require-protection-checks
```

The tests exercise real prompt answers, loops, sleep, incomplete input, errors,
UTF-8, persisted assignments, invalid/stale replies, input framing and shutdown.
The worker accepts only the selected R installation and a session working folder.
It does not install packages or execute source supplied by a notebook merely
because that notebook was opened.

Original integration baseline, before the protection-stack correction:
31 October 2026 verification: 43 native checks pass on each of R 4.6.1 and
4.6.2. The production session suite passes 92 checks on each version, covering
explicit device dimensions, SVG/PNG/PDF outputs, immutable older assets, variables
and interrupted prompts. Device-close regressions cover plain/namespaced dev.off,
graphics.off, an older noncurrent device, following errors, multipanels, real base
and grid pages, and explicit named devices without duplicate or empty previews.
The original 30 broader adapter tests also pass against
the promoted production session on R 4.6.2, including the complete paired-t
agreement script, real pages/multipanels, stale input and independent sessions.

The pre-close capture follows the actual installed R dev.off/graphics.off
implementations and [R's device.R source](https://svn.r-project.org/R/trunk/src/library/grDevices/R/device.R).
R has no pre-close display-list hook: its public dev.off immediately calls the
native close operation, and graphics.off calls the namespace function in a loop.
A private standard trace on both public dev.off bindings captures the owned
page before close, guards internal snapshot devices and keeps current-page
metadata separate for each device. Named file devices retain their original
file-writing behavior and are displayed after their files are flushed.

Frontend graphics state is stored in a private environment held by the
`.statsdirect.console` session option. It is absent from `.GlobalEnv`, so the
normal `rm(list = ls(all.names = TRUE))` workspace-clearing idiom does not remove
chart capture or close callbacks. The bootstrap environment inherits from base
rather than the user workspace, and host lookups use `base::getOption`. Regression
checks clear all global objects and then draw, close devices and create new user
variables successfully.

Named JPEG, GIF, BMP and TIFF outputs keep an immutable original file for Open.
The same System.Drawing decoder used by the previous output pane writes a
separate immutable PNG preview at the original pixel dimensions, without
resampling; its path is included as optional `previewPath` metadata so later
Copy actions can reload the exact preview without retaining base64 data.
Ordinary PNG files remain byte-for-byte copies. Real R jpeg/bmp/tiff output tests
compare dimensions, decoded pixels and older assets after filename reuse; GIF
uses a fixture copied by R because base R has no GIF device. Invalid raster data
produce an output-file link and leave R execution and variables available.

The private bootstrap retains the earlier RController package path behavior:
`Lib` below the StatsDirect R working folder is created if needed and prepended
to `.libPaths()`, while the standard R library stays available. This preserves
packages already installed through StatsDirect without installing anything or
adding setup variables to the user workspace. The suite verifies this locally.
