# Checks of the rendering of reports as HTML

A console project that renders small templates with the program's HTML report renderer (`CreoleHtmlReportRenderer` in `StatsDirectUI/TemplateProcessing`), with a stand-in for the program as the template host. The Windows report window renders reports as rich text, so Windows never shows this HTML; the Mac version does, and anything that saves or serves a report as HTML would.

The checks are made on a build of the program, as those of `tests/DataSelection` are: `StatsDirect.dll` of the Debug configuration, or of the folder given with `-p:StatsDirectBin=...`.

```
dotnet run -c Release
```

**What is checked.** The text of a substituted value is encoded as the text of the template is: a P value below the display threshold, which the program writes as "P < 0.0001", comes out as `P &lt; 0.0001`; a title with an ampersand and angle brackets is encoded; a P value above the threshold, a rounded number and the plain text of the template render as they did.

**What these checks showed when they were written (9 October 2026, from the Mac port's replay of the help examples).** The text of a value went into the HTML as it was, so that "P < 0.0001" left a bare "<" inside its span and the HTML was not well formed. Browsers recover, because a space follows the "<", but a strict parser does not.
