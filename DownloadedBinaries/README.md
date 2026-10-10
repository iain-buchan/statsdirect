# Installer prerequisites

In order to build SDBootstrapper, you will need to download the relevant Windows Desktop runtime.
See `DotNetDesktopRuntimeVersion` at the top of SDBootstrapper/Bundle.wxs for the version, then obtain the x64 Windows Desktop Runtime installer from https://dotnet.microsoft.com/en-us/download/dotnet.
The file must keep Microsoft's name for it, `windowsdesktop-runtime-<version>-win-x64.exe`.

To bundle a newer runtime, change `DotNetDesktopRuntimeVersion` and download the matching installer here. Microsoft releases patches most months, and they usually contain security fixes, so it is worth checking before each StatsDirect release.

## WebView2 reports

Also download the Evergreen Standalone Installer (x64) from https://developer.microsoft.com/en-us/microsoft-edge/webview2/ as `MicrosoftEdgeWebView2RuntimeInstallerX64.exe` in this folder. Microsoft's current x64 download link is https://go.microsoft.com/fwlink/?linkid=2124701. Verify its Microsoft Authenticode signature before building and refresh it before releases. The file is ignored by Git.

The setup bundle installs this shared runtime per machine, silently, when missing; the payload supports offline installation. It never removes the runtime when StatsDirect is uninstalled. The standalone MSI requires a machine-wide runtime already present and explains how to install it. A runtime installed for just the administrator is insufficient for other users of this per-machine application.
