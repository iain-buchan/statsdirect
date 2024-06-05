# Ready2Run R2R

Solution configuration - Release and ReleaseWithSigning both compile to Release folder

    dotnet publish StatsDirectUI.csproj --configuration Release --output "./bin/OUTPUT" --runtime win-x64 -p:PublishReadyToRun=true

This compiles the normal Release version of StatsDirectUI and a R2R verion in a OUTPUT folder. There is a difference in size of the statsdirect.exe and the statsdirect.dll files.

Produces a self-contained deployment for Windows 64-bit ( --runtime win-x64 ).

At the moment I copy the R2R version from the OUTPUT folder into the Release folder, then compile the SDBootstrapper.
The new installer (StatsDirectSetup.exe) is approx. 274Mb

<https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run>
