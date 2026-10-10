# How to build StatsDirect

## Prerequisites

These prerequisites are for .Net 10.

You will need:
* A Windows 10 or 11 system on which to build - the StatsDirect UI is WinForms-only, and does not (yet!) run on Linux or Mac.
* A recent release of the [.Net 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
* A trial or purchased license for [SpreadsheetGear for Windows](https://www.spreadsheetgear.com/) that allows you to use Hotfix 9.3.84, the version this project references, or later. Versions before 9.3.56 are not compatible with .Net 10.
* The [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) for reports, and its x64 standalone installer in `DownloadedBinaries` when building setup.
* [A current installer executable for the .Net 10 Windows Desktop runtime](DownloadedBinaries/README.md), to be included in the bundled executable installer.

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
* Follow the instructions on the SpreadsheetGear web site to obtain a license **string** from your license **key**.  Record that string.
* Clone this repository. In these instructions, we use `<repository-root>` to refer to the top of the cloned repository.
  * Keep `<repository-root>` short, such as `C:\src\statsdirect`. Unless Windows long paths are enabled, the installer build fails with `WIX0103: Cannot find the file ...` once the path to a file in the build output passes 260 characters, which happens when the repository root is longer than about 125 characters (a clone inside a deep OneDrive or Temp folder, for example).
* If you will sign builds:
  * Install the token's SafeNet Authentication Client and plug the token in. `certmgr.msc` (Personal > Certificates) should then list a certificate issued to `StatsDirect Ltd`.
  * `<repository-root>/Key/sign.cmd` selects that certificate by its name. If more than one certificate with that name is present, set the user environment variable `STATSDIRECT_SIGNING_THUMBPRINT` to the SHA-1 thumbprint of the right one.
  * The token client asks for the token password when the first file is signed. A signing build signs six files (the program, its main library, the installer's helper `StatsDirectRetire.dll`, the .msi, the setup bundle's engine and the bundle), so consider enabling the client's single log-on option or you will be asked six times.
  * Signatures are timestamped at GlobalSign's RFC 3161 server; set `STATSDIRECT_SIGNING_TIMESTAMP_URL` to use a different one.
  * The older way of signing with a `.pfx` file still works: put it at `<repository-root>/Key/sdsign.pfx` and set `STATSDIRECT_SIGNING_PASSWORD` to its password. Certificate authorities no longer issue keys that way.
* [Set environment variables](https://www.youtube.com/watch?v=5BTnfpIq5mI) for your SpreadsheetGear license string.
  * Set the user variable `SPREADSHEETGEAR_LICENSE_STRING` to the SpreadsheetGear license string you recorded earlier.
  * Note that these environment variables are set at process start, so you'll need to restart any IDE or terminal you had open at this point if you want to use them.
  * The build stops with an error if `SPREADSHEETGEAR_LICENSE_STRING` is not set, because the resulting StatsDirect would not start. To build without a license anyway (for example, to run `-sanity-check` or `-test-operations`), add `-p:AllowMissingSpreadsheetGearLicense=true` to the `dotnet build` command.

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

To update the bundle, build the `DesktopHelp` target in the `statisticalhelp` Flare
project (`C:/Develop/SD3Help/StatsDirect/StatsDirect.flprj` on this PC). Its local
destination publishes directly to this checkout's `StatsDirectUI/Assets/Help`;
adjust that destination when using another checkout. The target includes
software-help content, uses a compact desktop stylesheet/master without web
analytics, and generates all navigation, search, images and R dropdowns. Commit
the entire published bundle, excluding Flare build logs/metadata (gitignored).
Do not replace it with a partial copy of topics or either public web target
(`GitHub` for GitHub Pages and `Website` for statsdirect.com/help).

Numeric context IDs retain their historical `chm-id` XML name for compatibility
with existing operations and the Mac host. They resolve through the generated
`Help/Data/Alias.xml`; no CHM viewer or compiled help is used. The installer
excludes CHM/CHW files and removes the old `StatsDirect.chm`/`StatsDirect.chw`
during upgrades. `tests/HtmlHelp` covers the browser and context map; also test
an upgrade on a clean Windows VM before release.

### Calculator

Tools > Calculator opens a resizable pane below the workspace, created only on
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
`tests/Calculator` and `tests/HtmlHelp` after changing it. Existing saved Tools
entries for this installation's `StatsDirect.exe -calculator` are deduplicated
and routed to the pane. Old `-calculator` shortcuts now open the main application
with this same pane; the separate calculator application has been removed.
`tests/WindowsShell` checks the actual main form and merged worksheet commands.

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
dotnet build -c <solution-configuration>
```
