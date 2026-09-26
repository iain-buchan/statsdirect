# Legacy Excel add-in retirement

Run on Windows:

```powershell
dotnet run --project tests/ExcelAddInRetirement -c Release
```

Uses the production cleanup source against unique temporary directories and a disposable registry subtree. Checks preservation of other add-ins, workbooks and settings; consecutive Excel OPEN registrations and value types; StatsDirect's own settings key for the add-in; dry-run and Excel-running deferral, and silence when there is nothing to retire; locked-file retry; legacy names and Office versions; unloaded and redirected profiles; and refusal to traverse junctions or network paths. It does not open Excel or alter real Office settings.

For a read-only inventory of actual installations, the installer helper accepts:

```powershell
dotnet StatsDirectRetire/bin/Release/net10.0-windows/StatsDirectRetire.dll -excel-addin-only -whatif
```

The installer runs the helper on installation, upgrade and repair. Inaccessible or unloaded per-user settings are handled by StatsDirect when that user next starts it with Excel closed. Cleanup never loads another user's registry hive, changes Excel security settings, or closes Excel. Failures are logged to `StatsDirect-retire-excel-addin.log` in the executing account's temporary folder and retried on later application launches.
