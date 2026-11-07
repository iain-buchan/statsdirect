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

The wide-reader checks use a real offscreen WebView at the requested width,
including on small CI desktops. The harness temporarily sets the floating
frame's maximum tracking size and sizes its native window, then restores the
original limit; it does not change browser zoom or emulate a viewport. A setup
failure reports the requested and actual native/browser sizes, DPI, zoom and
screen limits, while retaining the `innerWidth >= 1280` requirement.

Also check F1, the draggable divider, Pop out, Dock and Help > Contents in the
application, and use a clean VM to
verify an MSI upgrade removes the old CHM and installs the entire Help folder.
