# How to build StatsDirect

## Prerequisites

These prerequisites are for .Net 10.

You will need:
* A Windows 10 or 11 system on which to build - the StatsDirect UI is WinForms-only, and does not (yet!) run on Linux or Mac.
* A recent release of the [.Net 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
* A trial or purchased license for [SpreadsheetGear for Windows](https://www.spreadsheetgear.com/) that allows you to use Hotfix 9.3.84, the version this project references, or later. Versions before 9.3.56 are not compatible with .Net 10.
* The [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) for reports, and its x64 standalone installer in `DownloadedBinaries` when building setup.
* [A current installer executable for the .Net 10 Windows Desktop runtime](DownloadedBinaries/README.md), to be included in the bundled executable installer.
* 64-bit R development headers and a C compiler for the interactive R worker. Run `RConsoleWorker/fetch-native-tools.ps1` for the verified portable Zig compiler, which the build prefers; Visual Studio's C++ compiler builds the worker too when Zig is absent, with two compatibility flags the script passes for R 4.6's headers. Nothing is installed globally by that script. See [RConsoleWorker/README.md](RConsoleWorker/README.md) for explicit header/compiler selection and runtime compatibility.

You may want:
* An IDE capable of working with C# and MSBuild, such as Visual Studio 2026 or Visual Studio Code.
* If using Visual Studio Code, the 'C# Dev Kit' extension, which allows working with solution files.
* A Visual Studio extension capable of working with WiX files, such as [HeatWave](https://www.firegiant.com/heatwave/).
* If generating signed binaries and builds, the StatsDirect Ltd code signing certificate. It is a GlobalSign certificate on a USB token; the token's SafeNet client must be installed on the build machine so that the certificate appears in the Windows certificate store, where `SignTool` finds it by name.

## Setting up your build environment

* Install the .Net SDK.
* If you wish, install your IDE.
* If you wish, install HeatWave.
* Install the WebView2 Evergreen Runtime if it is not already present. No DevExpress subscription or package feed is needed.
* No ChatGPT runtime or account is needed to build. StatsDirect downloads and verifies the pinned connection component on the user's first **Use my ChatGPT** action; it is excluded from the installer. `Learning/prepare-runtime.ps1` is an optional developer fixture for runtime tests. See [Learning/README.md](Learning/README.md).
* Follow the instructions on the SpreadsheetGear web site to obtain a license **string** from your license **key**.  Record that string.
* Clone this repository. In these instructions, we use `<repository-root>` to refer to the top of the cloned repository.
  * Keep `<repository-root>` short, such as `C:\src\statsdirect`. Unless Windows long paths are enabled, the installer build fails with `WIX0103: Cannot find the file ...` once the path to a file in the build output passes 260 characters, which happens when the repository root is longer than about 125 characters (a clone inside a deep OneDrive or Temp folder, for example).
* If you will sign builds:
  * Install the token's SafeNet Authentication Client and plug the token in. `certmgr.msc` (Personal > Certificates) should then list a certificate issued to `StatsDirect Ltd`.
  * `<repository-root>/Key/sign.cmd` selects that certificate by its name. If more than one certificate with that name is present, set the user environment variable `STATSDIRECT_SIGNING_THUMBPRINT` to the SHA-1 thumbprint of the right one.
  * The token client asks for the token password when the first file is signed. A signing build signs eight files (the program, its main library, the installer's helper `StatsDirectRetire.dll`, the native R worker, the .msi, the foreground extension, the setup bundle's engine and the bundle), so consider enabling the client's single log-on option or you will be asked eight times.
  * Signatures are timestamped at GlobalSign's RFC 3161 server; set `STATSDIRECT_SIGNING_TIMESTAMP_URL` to use a different one.
  * The older way of signing with a `.pfx` file still works: put it at `<repository-root>/Key/sdsign.pfx` and set `STATSDIRECT_SIGNING_PASSWORD` to its password. Certificate authorities no longer issue keys that way.
* [Set environment variables](https://www.youtube.com/watch?v=5BTnfpIq5mI) for your SpreadsheetGear license string.
  * Set the user variable `SPREADSHEETGEAR_LICENSE_STRING` to the SpreadsheetGear license string you recorded earlier.
  * Note that these environment variables are set at process start, so you'll need to restart any IDE or terminal you had open at this point if you want to use them.
  * The build stops with an error if `SPREADSHEETGEAR_LICENSE_STRING` is not set, because the resulting StatsDirect would not start. To build without a license anyway (for example, to run `-sanity-check` or `-test-operations`), add `-p:AllowMissingSpreadsheetGearLicense=true` to the `dotnet build` command.

## Fork pull requests and CI coverage

The required **Build and deterministic checks** job also runs on fork and
Dependabot pull requests without access to the SpreadsheetGear licence secret.
These runs explicitly build with `AllowMissingSpreadsheetGearLicense=true` and
an empty licence, then verify the compiled assembly contains no licence string.
They run the full deterministic and R replay suites, the headless sanity and
operation diagnostics, and the Learning JavaScript tests. The sanity checker
reports its existing skip of the sample-workbook opening when unlicensed.

The native Learning filtered-grid check and the advisory window-driven job are
deferred. Several window suites instantiate the licensed worksheet control;
other advisory UI coverage is deferred with that job. The run's summary states
this reduced native/UI coverage. A green fork result is not a substitute for
those checks on a reviewed, trusted branch.

Trusted repository runs keep the licence secret scoped to their build steps
and retain the full native/window checks. Missing trusted credentials still
fail the normal build guard; CI does not silently downgrade those runs. These
workflows use ordinary `pull_request` with read-only repository permissions,
not `pull_request_target` or a privileged follow-up workflow. Compiled binaries
are never shared with fork runs or uploaded as CI artifacts.

The repository owner can select **unlicensed** in **Run workflow** to validate
the same headless path on a trusted branch without supplying or using the
licence. Other contributors cannot force this override on a trusted run. The
option only removes access/coverage; it cannot enable secrets for a fork.
It does not build installers or change the normal application's licence guard.

## Building the StatsDirect program, installer, and bundled executable installer

### Solution configurations

There are four configurations for the StatsDirect solution.  By default, the solution builds Debug.

Debug
: Builds the StatsDirect executable only, with no setups and no signing. The resulting executable has debugging symbols, and catches exceptions in the same way as a released StatsDirect.

DebugWatchExceptions
: Builds the StatsDirect executable only, with no setups and no signing. The resulting executable has debugging symbols, but **does not** catch exceptions in key places. This is generally the build to use when debugging something that raises an exception, as the debugger will pause at the error if running under one.

Release
: Builds the StatsDirect executable, installer, and bundler; but does not sign them. Useful when checking build processes or when wishing to release an unsigned version.

ReleaseWithSigning
: Builds the StatsDirect executable, installer, and bundler, and signs `StatsDirect.exe` and `StatsDirect.dll` before they are packaged, then the installer (.msi), the setup bundle's engine and the bundled setup executable, all via `<repository-root>/Key/sign.cmd`. Each signature is verified after signing, so the build fails rather than producing an unsigned or untrusted file.

### Output locations

The build process copies generated installers to `<repository-root>/ReleaseBuild`.

Both installer projects pin WiX and their extensions to 7.0.1. WiX 7 requires
explicit acceptance of its [OSMF EULA](https://docs.firegiant.com/wix/osmf/).
For a command-line installer build, pass `-p:AcceptEula=wix7`, for example:

```powershell
dotnet build StatsDirectUI.slnx -c Release -p:AcceptEula=wix7
```

This accepts the EULA for that invocation without changing the machine's saved
acceptance settings. For IDE builds, follow WiX's documented acceptance process.
The application-only Debug configurations do not build WiX installers.

### Installer foreground handover

The bundle includes a small native `SDBootstrapper/Focus` BAFunctions extension.
After successful UAC elevation it posts one handover to the installer's own
window thread. Hidden, disabled and minimised windows are left alone. Windows
can deny foreground activation even on that thread and with the upstream
engine-side request from WiX issue 9317 / PR728.

If activation is denied, the helper raises its own progress window once without
changing keyboard focus, then immediately returns it to the normal window
group. It does not leave the installer always on top or repeat activation during
progress. Existing topmost windows are left alone. The log distinguishes actual
foreground ownership from this visibility fallback and records whether normal
window mode was restored; a failed cleanup is logged as `normal=0`.

The bundle build restores the pinned WiX 7.0.1 native API headers and builds the
extension with the same portable Zig compiler used by the R worker, or Visual
Studio C++ when Zig is absent. `ReleaseWithSigning` signs the extension before
embedding it in setup. Run `tests/InstallerFocus/run.ps1` after changes, then
verify the actual post-UAC handover on Windows. The automated callback checks
do not install StatsDirect or certify Windows foreground activation. Launch from
Explorer and click Install/approve UAC manually in repeated tests; invoking a
button through automation is not an equivalent test of the last-input rules.

The BAFunctions interface is independently versioned from the Burn engine.
WiX 7.0.1 still supplies BAFunctions version `2024.1.1.0`, and its
`OnElevateComplete` event still uses the version-5 callback contract. Recheck
both against the pinned WiX source when upgrading; changing either constant
to the engine's major version would disable the foreground handover.
The published BAFunctions window-procedure hook is message 1025; its x64
argument/result structures are 40/16 bytes.

### Updates: the manifest and the installer's address

From 5.3.17 the check for a new version reads a small file, the update manifest,
at `https://www.statsdirect.com/download/windows-latest.json`
(`StatsDirectUI/UI/UpdateOffer.cs`):

```json
{
  "schema": 1,
  "version": "5.3.17",
  "url": "https://github.com/iain-buchan/statsdirect/releases/download/v5.3.17/StatsDirectSetup.exe",
  "mirror": "https://www.statsdirect.com/download/v5.3.17/StatsDirectSetup.exe",
  "size": 304535768,
  "sha256": "79dbc07d18397b55f6c9e7957c30e5242370610f25a106ef0eb648450c394359"
}
```

- `url` must be, character for character, the address of `StatsDirectSetup.exe`
  in this repository's GitHub release tagged `v<version>`. The updater accepts
  no other first address.
- `mirror` need not be given. If it is, it must be `StatsDirectSetup.exe` in
  `https://www.statsdirect.com/download/` or up to four folders below it. A
  release publishes the versioned one, in the folder `v<version>`: the file
  there never changes, so an offer goes on finding its own installer after the
  website has moved on to a later release. The manifest cannot send a download
  anywhere but these two places.
- The updater turns to the mirror only when the file cannot be fetched from
  `url`, as on a network that blocks GitHub: no connection, a refusal, a file
  that is not the one published, or a timeout. With a mirror available, the
  first address has a 30-second idle timeout for each response or part of
  the file, and a 10-minute limit for the whole attempt including redirects.
  The mirror then has 30 minutes of its own, as does an address with no mirror
  (`UpdateDownloadLimits`).
- The updater offers the version only to an older StatsDirect. It downloads the
  file, following redirects only to https addresses, and starts it only if it
  has that size and SHA-256, is StatsDirect's setup program signed by
  StatsDirect Limited, is newer than the version running and is the version
  offered. That holds whichever address sent the file. Otherwise the download
  is removed, StatsDirect stays open and the user is given the download page.
- The setup program is told from the program's own signed files by the name in
  its version resource, `StatsDirectSetup.exe`, which the build takes from
  `OutputName` in `SDBootstrapper/SDBootstrapper.wixproj`. Like the name on the
  signing certificate, it has to stay as it is: installed copies refuse a setup
  program under another name.
- A reader passes over names it does not know, so fields can be added. A change
  that an older reader would misread needs a new `schema` number and a new
  address for the file: installed versions go on reading `windows-latest.json`
  in schema 1.
- The address names this repository. If the repository is ever moved or
  renamed, the manifest goes on giving this address, which GitHub redirects
  and the updater follows; a version that accepts the new address has to be
  released before anything else takes the old name.

At each release, with the signed installer. Each command is
`dotnet run --project tests/UpdateDownloadRegression -c Release --` followed by
the options shown, and none of them starts an installer.

1. `--verify <StatsDirectSetup.exe> --running <previous version>` must say PASS,
   as before: installed copies will start this installer.
2. `--manifest <StatsDirectSetup.exe> --out windows-latest.json --versioned-mirror`
   writes the manifest from the signed installer with the updater's own code,
   and reads it back. It refuses a file that is not the setup program.
3. Attach `windows-latest.json` to the GitHub release with the installers
   before the release is published. With release immutability switched on,
   nothing can be added to a release afterwards.
4. After publishing, `--check-manifest windows-latest.json --running <previous version> --download --require-github`
   must say PASS. It takes the file from the release's address and no other,
   so a mirror in good order cannot hide a file that is missing or wrong at
   GitHub.
5. After the GitHub check passes, deploy the website in one operation. Place the
   signed setup at the manifest's versioned mirror and compare it byte for byte
   with the GitHub release's setup. Keep earlier version folders so updates
   already offered can still find their installers. In the same deployment,
   publish the manifest byte for byte from the release, update the download page,
   and publish the website's unversioned setup and MSI. The manifest names stable
   releases only, and both installer addresses must serve the same file when it
   goes live.
6. Record the live checks as part of the website deployment. Run
   `--check-manifest https://www.statsdirect.com/download/windows-latest.json --running <previous version> --download --block-github`
   to confirm that the live manifest offers the new version and that the updater
   can verify the installer from the mirror with GitHub blocked. If the check
   fails, tell the website maintainer immediately so they can correct the mirror
   or withdraw the new manifest. Use the website team's results when they cover
   the same live manifest and installers.

Installed versions before 5.3.17 do not read the manifest, and the website keeps
their addresses working as its own files:

- 5.0.8 to 5.3.16 read the version from the text "Current version ..." on
  `www.statsdirect.com/download.aspx` and fetch
  `https://www.statsdirect.com/download/StatsDirectSetup.exe`. 5.3.15 and 5.3.16
  also require the file to be signed by StatsDirect Limited and newer than the
  version running.
- 5.0.7 and earlier open `http://www.statsdirect.com/download/StatsDirectSetup.exe`
  in the default browser.

That address stays a file on the website, not a redirect to GitHub: an installed
copy on a network that blocks GitHub can update only from it. The page must not
announce a version before that address serves it.

### Desktop HTML5 help

The complete offline help bundle is checked in at `StatsDirectUI/Assets/Help`.
An ordinary application or installer build copies it into `Help` beside the
executable; Flare is not required to build StatsDirect. WebView2 displays it in
a resizable pane on the right of the MDI workspace. **Pop out** moves the same
browser into a companion window; **Dock** returns it. The browser is reparented,
not reloaded, so navigation history, expanded R examples and reading position
survive. The divider changes the pane width, and Close restores the workspace.
The user's mode and width are retained for the current application session.
F1, analysis/report help links and message-box Help buttons use the same viewer,
leaving the active worksheet/report and data selection intact. During a modal
dialog, visible help temporarily floats so it stays interactive, then returns
to the user's chosen presentation when the dialog closes.

To update the bundle, publish from the StatsDirect Help Editor (File > Publish
content, or the `publish-content` command of `StatsDirectHelpBuilder.exe`). It
builds the desktop help from the maintained `HelpEditor/HelpSource` in the
`statisticalhelp` repository, delivers it to this checkout's
`StatsDirectUI/Assets/Help` after inspecting the destination's hash, and records
the delivery in `Assets/Help/.statsdirect-desktop-help.json` and
`.statsdirect-desktop-help-adoption.json` at the repository root. While
`.statsdirect-desktop-help.publishing.json` exists at the repository root the
application build stops (`BuildTools/StatsDirectDesktopHelp.targets`). Commit the
entire delivered bundle; `StatsDirectUI.csproj` keeps its control files out of
the application package. MadCap Flare is retired: do not build any help target
with it, and do not replace the bundle with a partial copy of topics or with the
website output.

Numeric context IDs retain their historical `chm-id` XML name for compatibility
with existing operations and the Mac host. They resolve through the generated
`Help/Data/Alias.xml`; no CHM viewer or compiled help is used. The installer
excludes CHM/CHW files and removes the old `StatsDirect.chm`/`StatsDirect.chw`
during upgrades. `tests/HtmlHelp` covers the browser and context map; also test
an upgrade on a clean Windows VM before release.

### Interactive R script window

The R Script window uses the shared offline `Assets/RConsole` page, with Console
and Script views of one persistent R workspace. The native worker is built and
copied by the application project; a separate R installation supplies R.dll. The
Windows application never loads that DLL into its own process. The current
worker takes any 64-bit R from 4.2, the minimum `RConsoleWorker/console.c` states, and is
verified with R 4.6.2 and R 4.2.3; the window uses the newest such R installed. More > Set up R opens the
existing official-R installation flow; R is not bundled into StatsDirect.

Run `tests/InteractiveRWindows`, `tests/WindowsShell` and `tests/Learning` after
changing this integration, as well as the native/session tests documented in
`RConsoleWorker/README.md`. The R window starts its worker on first execution;
opening R files and notebooks never executes their code. File > Save captures
current browser source without serialising or re-running the live workspace.
Stop interrupts the current R calculation or prompt; Restart and closing the
window end that workspace. Closing an R document uses the same asynchronous
parent-close preparation as the web report, allowing WebView2 edits and saves
to complete without blocking its UI thread.

### Calculator

Analysis > Calculator opens a resizable pane below the workspace, created only on
request. Close hides it without leaving a tab. Pop out and Dock move the same
calculator view; its expression, result, saved calculations, size and presentation
are retained for the current session. Help can remain open on the right.
The input shows about three lines by default in both docked and floating modes;
enlarging the pane or window gives it more space. It wraps and scrolls vertically;
Enter evaluates and Shift+Enter
inserts a line break. Recall restores a saved calculation; Insert puts a saved
expression at the caret. During modal dialogs, a visible calculator floats and
then returns, preserving focus in the parameter dialog.
The calculator's Edit menu and Ctrl+Z/Ctrl+Y provide multilevel undo/redo,
including Recall and Insert, even without an open worksheet. Docking preserves
the input control and its editing history. Input is plain text; the native
Windows text control does not restore the retired DevExpress report editor.

The shared tool-window infrastructure is used by Calculator and Help. Run
`tests/Calculator` and `tests/HtmlHelp` after changing it. Calculator is in its own
Analysis menu section immediately before Options. External-program shortcuts and
Setup Tools have been retired. Old `-calculator` shortcuts open the main application
with this same pane; the separate calculator application has been removed.
`tests/WindowsShell` checks the actual main form and merged worksheet commands.
`tests/WorksheetOutput` checks the writing of an operation's output frames to a picked
sheet by the real document host, with the main window opened off screen.

### Visual Studio 2026

* Open `<repository-root>/StatsDirectUI.slnx`.
* Select your preferred solution configuration (we suggest `Debug` to start).
* Build -> Rebuild solution.

### Visual Studio Code with C# Dev Kit

* Open `<repository-root>/StatsDirectUI.slnx`.
* Right-click StatsDirectUI.slnx and Open Solution.
* In the status bar at the bottom of the window, set the configuration appropriately (it will start as 'Debug Any CPU')
* In the Solution Explorer accordion at the bottom of the Visual Studio Code Explorer, right-click on the preferred build target and choose Build.

### Command line

```
cd <repository-root>
dotnet clean
dotnet build -c <solution-configuration> -p:AcceptEula=wix7
```
