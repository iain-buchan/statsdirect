# Shared chart alignment — 12 October 2026

Baseline: `statsdirect-mac` commit `4e9f9d10a7dbf795c28fc3dc3e5924476a3bc1d6`.
The Mac chat supplied its then-uncommitted changes to `chart-size.mjs`,
`editor.mjs`, `export.mjs`, `fragment.mjs` and `import.mjs`; these are now in
`upstream/Report`. The final versions, including the SVG correction below, are
now committed upstream as `71fd32193a00e88d96616a0ea538c0fd042c038e` on
`feature/mac-unified-toolbars`. All five files were compared with that fetched
commit and match exactly after normalising CRLF to LF. Other vendored report
files remain at the baseline above.

The shared changes store responsive left/centre/right margins on the picture,
use exact single-picture selection for the alignment buttons, update button
states, disable Justify for pictures, resize from the free corner, and preserve
alignment in editable HTML, clipboard fragments, Word and PDF exports.

Windows testing also found that `exportableSvg()` retained the new report margins
in standalone chart files. It now removes only root margin properties, retaining
chart paint/font declarations. This correction was shared with the Mac chat.

Windows-only differences stay in the host stylesheet and build adapters:

- `windows.css` defaults report SVGs and raster pictures to the left margin.
- The import bundle tags its temporary container `report-import` so those same
  defaults apply before wrappers exist. The host's two workspace chart-default
  selectors exclude that container; its own rule has low specificity, allowing
  incoming styles to override it. Mac uses
  a separate importer WebView without the shared workspace stylesheet.
- `export-style.mjs` retains natural table widths and 96-dpi Office image
  attributes, while preserving the shared exporter's selected chart alignment.
- Ctrl-drag copies on Windows; Option-drag copies on Mac.

All three browser bundles are regenerated using `node ReportEditor/build.mjs`.
The dependency lock and licences are unchanged. The Windows workspace stylesheet
has only the two import-container exclusions described above. Windows also omits
the focused report outline from printing; previously a bottom border could leak
into PDF export despite the ordinary editing outline being hidden.
`tests/WebReports/Alignment.cs` exercises the real WebView2 editor, exports and
reopening; `Charts.cs` checks standalone SVG markup and raster equivalence.
