# Offline HTML5 help tests

Run on Windows with WebView2 installed:

    dotnet run --project tests/HtmlHelp -c Release -- C:/Temp/StatsDirectHelpTests

The native harness uses the production viewer and published help assets. It
checks every named/numeric alias and operation context ID, topic rendering,
images/equations, R dropdowns, reference anchors, full-text search, navigation,
external-link isolation and missing files. Docking checks cover MDI layout,
preserved focus/selection, shared browser state, reading position, close/reopen,
mode/width retention and automatic floating/return during modal dialogs.
Screenshots and a temporary browser profile go to the supplied output folder.

Also check F1, the draggable divider, Pop out, Dock and Help > Contents in the
application, and use a clean VM to
verify an MSI upgrade removes the old CHM and installs the entire Help folder.
