# Dockable calculator tests

Build the Windows application first, then run on Windows with WebView2 installed:

    dotnet run --project tests/Calculator -c Release -- C:/Temp/StatsDirectCalculatorTests

For an isolated application build, set `-p:StatsDirectBin=<application-output-directory>`.
The STA WinForms harness uses the production calculator, calculation engine and
shared tool-window/help classes. It checks lazy creation, legacy command routing,
worksheet selection, wrapping without text changes, multiline evaluation,
Enter handling, saved expressions, invalid input, clipboard content (through an
injected callback), docking, splitter limits at high DPI, state retention,
Help coexistence in either opening order and temporary floating during modal
dialogs without stealing focus. The harness does not modify the system clipboard.
A narrow-layout screenshot is written to the supplied output directory.

Also check Tools > Calculator and Enter/Shift+Enter in the full application;
native keyboard routing and the installed Tools menu are outside this harness.
