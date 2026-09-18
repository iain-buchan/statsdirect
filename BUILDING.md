# How to build StatsDirect

## Prerequisites

These prerequisites are for .Net 10.

You will need:
* A Windows 10 or 11 system on which to build - the StatsDirect UI is WinForms-only, and does not (yet!) run on Linux or Mac.
* A recent release of the [.Net 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
* A trial or purchased license for [SpreadsheetGear for Windows](https://www.spreadsheetgear.com/) that allows you to use Hotfix 9.3.84, the version this project references, or later. Versions before 9.3.56 are not compatible with .Net 10.
* A trial or purchased license for [DevExpress Components for WinForms](https://www.devexpress.com/products/net/controls/winforms/).
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
* Download and install the DevExpress Components for Windows, or follow DevExpress instructions to set up your NuGet package sources to include your subscription feed.
* Follow the instructions on the SpreadsheetGear web site to obtain a license **string** from your license **key**.  Record that string.
* Clone this repository. In these instructions, we use `<repository-root>` to refer to the top of the cloned repository.
* If you will sign builds:
  * Install the token's SafeNet Authentication Client and plug the token in. `certmgr.msc` (Personal > Certificates) should then list a certificate issued to `StatsDirect Ltd`.
  * `<repository-root>/Key/sign.cmd` selects that certificate by its name. If more than one certificate with that name is present, set the user environment variable `STATSDIRECT_SIGNING_THUMBPRINT` to the SHA-1 thumbprint of the right one.
  * The token client asks for the token password when the first file is signed. A signing build signs five files (the program, its main library, the .msi, the setup bundle's engine and the bundle), so consider enabling the client's single log-on option or you will be asked five times.
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
