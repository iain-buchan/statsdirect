# Windows interactive R worker

`StatsDirect.RConsole.Native.exe` is a small native child process. It loads the
selected installed 64-bit R; StatsDirect and WebView2 never load R.dll. Each R
script window owns one persistent R workspace. Stop requests an R interrupt on
the worker's R thread; Restart or closing the window ends the worker process.

The protocol, interrupt handling and display-list plot capture were first tested
in `prototypes/InteractiveR`. The production session adapter is now
`StatsDirectUI/R/Interactive/RConsoleSession.cs`. The shared web console is based on Mac commit
`cf008df1788fc1ff811988df3392bca57cb31a7b`, with the tested authoritative
source replay fix, display-only clearing and optional output-file links from
Windows commit `83a39468`. The Windows host adds native editing/clipboard/menu
adapters; the shared toolbar scrolls horizontally in a narrow MDI window.

Build the application project normally. Its build invokes `build-native.ps1` and
copies the helper beside StatsDirect.exe, including for `dotnet publish`.
Building requires R development headers plus either Visual Studio's C++ workload
or the pinned portable Zig compiler. R itself is not redistributed. The default
headers are taken from the latest installed R under Program Files; set
`RConsoleRHome` or `R_HOME` to choose another installation. Runtime patch versions
in that same major/minor series are supported; another minor version is refused
before its startup structure is used. Current verification covers R 4.6.1 and
4.6.2, whose public startup headers are byte-identical.

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
python RConsoleWorker\test-native.py 'C:\Program Files\R\R-4.6.2'
dotnet run --project RConsoleWorker\Tests -c Release -- RConsoleWorker\bin\StatsDirect.RConsole.Native.exe 'C:\Program Files\R\R-4.6.2'
```

The tests exercise real prompt answers, loops, sleep, incomplete input, errors,
UTF-8, persisted assignments, invalid/stale replies, input framing and shutdown.
The worker accepts only the selected R installation and a session working folder.
It does not install packages or execute source supplied by a notebook merely
because that notebook was opened.

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
