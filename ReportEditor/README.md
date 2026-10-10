# Windows report editor

**File > New Report**, automatically created reports and opened HTML reports all use WebView2. The DevExpress report forms, RTF/EMF report generators and startup preloader have been retired. Chart-options previews use the same HTML/SVG renderer in a read-only window.

The editor supports formatted text, tables, SVG/raster sizing, multi-section undo/redo, rich cut/copy/paste, Windows Ctrl-drag copying and ordinary drag moves. New and reopened reports are immediately editable, with their formatting ribbon visible and no Edit report/Done toggle. Save, Print and clipboard commands use the main toolbar. The **File** menu provides **Save**, **Save As**, **Open HTML** and **Export report > Word document / PDF** for the active web report. There is no Report dropdown on the toolbar. Menu commands merge automatically with the main window, as they do for data workbooks. The recent-operations selector has been removed. Ctrl+S, Ctrl+Shift+S, Ctrl+O, Ctrl+P and F1 go through the report host. Browser reload is blocked to avoid losing edits.

Select text or click a chart and press Delete or Backspace. Click a result heading to select the whole result; Shift-click another heading to select a group. Ctrl+A selects all results. Text edits, chart deletion and result deletion share the same Ctrl+Z/Ctrl+Y undo/redo history, including restoration of result order and operation/help metadata. There are no separate result/chart removal buttons or removal-only undo list.

Windows report presentation retains the established template styles: a left margin, Arial 10pt, compact underlined headings and the standard Creole result colours (blue confidence intervals, green P values, teal scores, red warnings, dark red subtotals and navy model/grand totals). Charts retain their physical report width instead of stretching across a wide screen; the user can still resize them. Explicit imported and user-applied formatting takes precedence. `windows.css` supplies these defaults without changing the shared Mac stylesheet.

Generated content comes from the existing operation definitions and Creole templates through `HtmlRenderer`/`CreoleHtmlReportRenderer`; it is not recreated from a demonstration report. The template roles retain their established meaning: titles are bold and underlined, subtitles and table headers are underlined at normal weight, tables have plain left-aligned cells without shading or borders, and preformatted text uses Courier New. All these text roles use 10pt. Correct shared styles or the template renderer when generated output differs; do not repair individual generated labels as the implementation.

The formatting toolbar uses 20px SVG symbols inside 32px controls, with explicit sizing and zero margins so chart styles cannot stretch its icons. The native main toolbar uses `SharpToolbarRenderer` to draw its eight common commands as vectors at the current display scale. Its image layout uses 20 logical pixels, converted to device pixels and refreshed when the monitor DPI changes; merged data toolbar items keep their existing renderer fallback.

Use **File > Save** or **File > Save As** for an editable HTML document. Save/reopen preserves separate results, operation names and numeric help contexts in inert JSON metadata. File > Open opens HTML reports in the new editor, including ordinary HTML without that marker. Plain text is imported as encoded preformatted text and saved to a new HTML file. Imported markup is sanitized before it reaches the editor. RTF, MHTML and DOCX import are not implemented: open these in Word or another compatible editor, save as HTML and then open that file. StatsDirect displays this guidance when asked to open a legacy report; it does not modify the original.

PDF and DOCX are exports; they do not change the editable document's path or mark its latest edits as saved. Saving writes a temporary file in the destination directory and only replaces the target after the complete file is ready. Closing awaits pending editor messages and saves without blocking the WebView2 UI thread. A later edit during an export remains dirty.

## Shared Mac component

Pinned source: **iain-buchan/statsdirect-mac**, commit **1ec343445a2b9fae25e2a1875c83f2a3e4da178e**, “Add editable reports with rich transfers and legacy RTF import”.

- `upstream/Report` contains the original source, locked dependency manifest and JavaScript tests; `upstream/Tests/Fixtures/Reports` contains their fixtures.
- `upstream/LICENSE` retains the Mac repository licence.
- `StatsDirectUI/Assets/ReportEditor/vendor` contains offline browser bundles and their third-party notices. `workspace.css` is copied from the same Mac commit.
- `build.mjs` adapts the original editor's message boundary to `statsDirectReportHost.post` and uses Ctrl-copy drag semantics/shortcut labels. Its export adapter uses the shared `inlinePresentation` helper on the detached HTML copy so stylesheet-based result colours and fonts reach Word runs. Exporting does not modify the live report or its undo history. Upstream source files stay unchanged.
- `export-style.mjs` prevents the shared exporter from replacing Windows template presentation with independent document, heading, table and monospace sizes. Word table cells use their resolved run/paragraph styles; Office HTML retains selected table styles and Excel number/text hints. DOCX table borders default to none and explicit cell styling is retained. These guarded build-time substitutions require review when the upstream source changes.
- `host.js` implements the report-entry model, bounded transaction history and typed browser commands. `ReportView.cs` owns the native WebView2 boundary, files, clipboard and printing. `frmReportWebView.cs` integrates that control with the existing `IReport`/MDI window contract.
- `deletion.mjs` adds selection-aware Delete/Backspace and result-heading selection. The build adapter exposes the shared transfer helpers to preserve partial table structure and handle charts atomically; whole-result deletions are recorded alongside HTML changes by the host. Original upstream files remain unchanged.

Rebuild the Windows editor bundle after changing its pinned source or adapter:

```powershell
pnpm --dir ReportEditor/upstream/Report install --frozen-lockfile
node ReportEditor/build.mjs
```

The editor, export and import bundles are rebuilt from the pinned source by the adapters above. `safe-border-style.mjs` permits the twelve border longhands that Chromium produces from CSS shorthand, so explicit borders survive copying and reopening. The Windows clipboard adapter uses point units for Office styles and 96-dpi image attributes: this preserves text and chart sizes at high display scaling. Do not transfer the Windows image-attribute adjustment to Mac without testing its clipboard path. Editable HTML metadata is taken from the styled export snapshot so styles also survive reopening. Normal .NET builds require no JavaScript package installation. Update sources, bundles, styles, fixtures and notices together when changing the upstream pin; retain its export and sanitization tests.

## Hosting and deployment

The control uses a local virtual HTTPS origin mapped only to the bundled editor directory. It exposes no native objects, blocks permissions/downloads/external resource requests, restricts navigation and checks the source of native messages. Imports use the Mac DOMPurify/conversion pipeline. Public clipboard HTML uses Office-compatible pictures; the private clipboard representation retains SVG. CF_HTML offsets count UTF-8 bytes.

WebView2 Evergreen Runtime is required for reports, chart previews and offline HTML5 help. The setup bundle installs the x64 standalone runtime per machine when it is missing, including offline, and never removes the shared runtime. The standalone MSI checks this prerequisite. The installer excludes stale DevExpress assemblies and the old blank RTF template from incremental build output; upgrading removes the old MSI components, leftover root-level DevExpress DLLs and retired CHM files. SpreadsheetGear remains the worksheet control.

## Validation and remaining acceptance work

See `tests/WebReports/README.md`. The native harness uses the installed Chromium WebView2 Runtime, including the production paired-t/agreement engine and renderer, and creates HTML, DOCX, PDF and screenshots. It does not write the system clipboard. The separate Office integration script pastes synthetic HTML into new Word and Excel documents, then restores the previous clipboard. The report-rendering and parametric/agreement regression suites remain applicable.

Before release, validate interactive save/cancel/application-exit flows, IME/accessibility, large reports, printing on physical printers and external image insertion/export. Installed Word and Excel transfers are covered by the integration checks; PowerPoint and other Office versions still need acceptance testing. The editor does not offer a full Word-style page-layout/ruler or Find/Replace feature set. Legacy report import remains future work.
