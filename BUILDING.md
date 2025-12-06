# How to build StatsDirect

## Prerequisites

These prerequisites are for .Net 8.

You will need:
* A Windows 10 or 11 system on which to build - the StatsDirect UI is WinForms-only, and does not (yet!) run on Linux or Mac.
* A recent release of the [.Net 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
* A trial or purchased license for [SpreadsheetGear for Windows](https://www.spreadsheetgear.com/).
* A trial or purchased license for [DevExpress Components for WinForms](https://www.devexpress.com/products/net/controls/winforms/).
* [A current installer executable for the .Net 8 Windows Desktop runtime](DownloadedBinaries/README.md), to be included in the bundled executable installer.

You may want:
* An IDE capable of working with C# and MSBuild, such as Visual Studio 2026 or Visual Studio Code.
* If using Visual Studio Code, the 'C# Dev Kit' extension, which allows working with solution files.
* A Visual Studio extension capable of working with WiX files, such as [HeatWave](https://www.firegiant.com/heatwave/).
* If generating signed binaries and builds, a code signing certificate. Ultimately, you'll need this in .pfx format as it's what `SignTool` understands.

## Setting up your build environment

* Install the .Net SDK.
* If you wish, install your IDE.
* If you wish, install HeatWave.
* Download and install the DevExpress Components for Windows, or follow DevExpress instructions to set up your NuGet package sources to include your subscription feed.
* Follow the instructions on the SpreadsheetGear web site to obtain a license **string** from your license **key**.  Record that string.
* Clone this repository. In these instructions, we use `<repository-root>` to refer to the top of the cloned repository.
* If you have a signing key:
  * Convert it to a password-secured .pfx file.  Record that password.
  * Copy the .pfx to `<repository-root>/Key/sdsign.pfx` or, if you prefer, change `<repository-root>/Key/sign.cmd` so that it reads the key from a different path.
* [Set environment variables](https://www.youtube.com/watch?v=5BTnfpIq5mI) for your SpreadsheetGear license string and, if required, your signing key's password.
  * Set the user variable `SPREADSHEETGEAR_LICENSE_STRING` to the SpreadsheetGear license string you recorded earlier.
  * If you have a signing key, set the user variable `STATSDIRECT_SIGNING_PASSWORD` to the password to your .pfx file.
  * Note that these environment variables are set at process start, so you'll need to restart any IDE or terminal you had open at this point if you want to use them.

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
: Builds the StatsDirect executable, installer, and bundler, signing all artifacts via `<repository-root>/Key/sign.cmd`.

### Output locations

The build process copies generated installers to `<repository-root>/ReleaseBuild`.

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
