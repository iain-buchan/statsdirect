# Dockable calculator tests

Build the Windows application first, then run on Windows with WebView2 installed:

    dotnet run --project tests/Calculator -c Release -- C:/Temp/StatsDirectCalculatorTests

For an isolated application build, set `-p:StatsDirectBin=<application-output-directory>`.
Add `--96dpi` after the output directory to check standard 100% layout as well
as the normal run at the monitor's actual scaling.
The STA WinForms harness uses the production calculator, calculation engine and
shared tool-window/help classes. It checks lazy creation, legacy command routing,
worksheet selection, compact default input height and resizing, wrapping without text changes, multiline evaluation,
Enter handling, saved expressions, invalid input, clipboard content (through an
injected callback), docking, splitter limits at high DPI, state retention,
Help coexistence in either opening order and temporary floating during modal
dialogs without stealing focus. The harness does not modify the system clipboard.
Docked, floating and narrow-layout screenshots are written to the supplied output directory.

The expanded suite passes 72 checks at both 96 and 240 DPI. It exercises the
pane Edit menu, Ctrl+Z/Ctrl+Y, Recall/Insert undo steps, chronological history,
read-only result protection, and selection/history across hiding and docking.
`tests/WindowsShell` additionally exercises the actual compiled main form,
legacy startup command and merged worksheet Edit menu. Physical keyboard and
assistive-technology acceptance remains useful before release.
