# Put the dotnet Desktop runtime you need here

In order to build SDBootstrapper, you will need to download the relevant Windows Desktop runtime.
See `DotNetDesktopRuntimeVersion` at the top of SDBootstrapper/Bundle.wxs for the version, then obtain the x64 Windows Desktop Runtime installer from https://dotnet.microsoft.com/en-us/download/dotnet.
The file must keep Microsoft's name for it, `windowsdesktop-runtime-<version>-win-x64.exe`.

To bundle a newer runtime, change `DotNetDesktopRuntimeVersion` and download the matching installer here. Microsoft releases patches most months, and they usually contain security fixes, so it is worth checking before each StatsDirect release.
