# Changelog

Changelog best practices reference: https://keepachangelog.com/en/1.0.0/

##[v5.0.0] 2026-09-18

Version 5 follows an independent audit of the calculation layer against R (and more accurate references where R has none), which found 24 defects. All are corrected here, and each correction was confirmed on the Windows build. Results from the procedures listed below can differ from version 4.

### Changed
- Intraclass correlation (Agreement > Continuous) is the one-way ANOVA estimator ICC(1) with an exact F-based confidence interval, as R and Shrout and Fleiss, not Fisher's symmetrical-table form
- Power for the t tests uses the exact non-central t distribution; sample sizes for the paired t test, independent proportions and correlation are one smaller in some cases
- Two-sample Smirnov P values are exact and conditional on ties, as R's ks.test, at any sample size
- Harbord's test uses the hypergeometric variance of the score for odds-ratio analyses; the exact confidence interval for Hedges g converges to 1e-9
- Meta-analysis bias indicators: the confidence intervals for the Egger and Harbord-Egger bias estimates are given at twice the alpha of the analysis, so 90% by default, matching the P < 0.1 convention for these low-powered tests. Egger's interval was previously at the level of the analysis (95%); Harbord's was at 92.5% by mistake, where earlier versions and the help gave 90%. The bias estimates and P values are unchanged
- A Bonferroni-adjusted (simultaneous) confidence interval is added to the Bonferroni multiple comparison report
- Time series summary: the precision of the mean AUC comes from the subjects' own AUCs, the group comparison is a Welch t test, and the bootstrap resamples whole subjects
- The multi-rater kappa report no longer prints the line labelled weighted kappa, which was a Berry-Mielke coefficient on per-column codes
- Bootstrap and random allocation draws are uniform, so bootstrap and randomisation results differ from version 4 even with the same seed
- DevExpress.Win.RichEdit updated to 26.1.4
- SpreadsheetGear updated to 9.3.84
- Dependencies updated
- The setup executable bundles the .Net 10.0.12 Windows Desktop runtime, up from 10.0.0
- LOESS: the default polynomial degree is 2, as the help says and as in R itself, not 1
- The installer puts 487 files on the machine instead of 1,503 and is 6 MB smaller: it no longer carries a superseded copy of the web help, the compiler's messages in twelve other languages, the test framework, a separate debug symbols file or the help maintainer's mapping workbook. Error messages still give source line numbers, because the symbols are now inside StatsDirect.dll
- Signed builds use the StatsDirect Ltd certificate on its USB token, timestamp at GlobalSign, verify each signature, and sign StatsDirect.exe and StatsDirect.dll as well as the installer and setup executable

### Fixed
- Conditional maximum likelihood estimates printed as 0 in the diagnostic test, exact odds ratio interval, retrospective risk, comparison of two rates, log-rank hazard ratio and Mantel-Haenszel (exact option) reports; the intervals and P values were right. Introduced in April 2024, so versions 4.0.0 to 4.0.5 are affected
- Two-rater kappa from worksheet columns always stopped with "Invalid data"; multi-rater kappa stopped with an error for three or more categories with unequal numbers of ratings, and its standard errors used the number of columns rather than the number of ratings
- Spearman's rank correlation with ties reported a one sided P as two sided; the exact P for 10 or fewer pairs excluded the observed statistic on the lower side
- Kendall's rank correlation: the exact lower side P excluded the observed statistic, so it, and the two sided P whenever tau was negative, were too small (for tau = -0.733333 with 10 pairs, two sided P = 0.0009 where 0.0022 is right). The same error affected the Begg-Mazumdar bias indicator in meta-analysis and Kendall's tau in the agreement report when there are no ties
- Correlation meta-analysis: the Begg-Mazumdar and Egger bias indicators left out any study whose correlation or lower confidence limit was zero or negative; the Egger line was printed unrounded
- Incidence rate difference meta-analysis printed the lower limit of the Egger interval unformatted
- Unpaired t test: the unequal-variance (Welch) confidence interval used the pooled degrees of freedom, and power was calculated with the second sample size in place of the ratio of sample sizes
- Woolf's pooled odds ratio without Haldane correction, its interval and chi-square statistics were contaminated by the corrected analysis
- Two-sample Smirnov one sided P values were half the two sided P; P values ignored ties
- Time series summary understated the standard error of the mean AUC by treating time points as independent
- Time series summary printed the correlation coefficient r under the label R-square for AUC and log(AUC) against normal scores; it now prints r squared
- Durbin-Watson statistic omitted the first squared residual difference in multiple linear and polynomial regression
- Compare two standardized rates: the "All" (crude) row was not printed
- Agreement report: Kendall's tau b of within-subject standard deviation against mean overcounted tie groups of three or more, and could exceed 1
- Conditional logistic regression: cases and controls were swapped in the optional counts table with more than one control per case; coefficients and odds ratios were unaffected
- Two sided binomial P could exceed 1
- Hedges g confidence limit could be lost for odd degrees of freedom (non-central t returned 0 in three special cases)
- Chi-square quantiles failed outside 0.000002 < p < 0.999998; chi-square P values and quantiles were inaccurate in the tails above about 2000 degrees of freedom
- Smaller numerical corrections: t quantile for extremely small P with 60 to 65 degrees of freedom, error function for tiny arguments, singular value decomposition with exactly collinear variables
- Charts: every line meant to be green was drawn in red, including the 1 SD lines of the control chart and the Lorenz curve of the Gini chart
- Histogram: the overlaid normal curve was shifted and too wide (by 11% to 18% in the help's examples), and stopped at the first and last bin mid-points
- Charts: a fitted line or band with an end outside the plot area lost the whole of that segment, so polynomial regression curves stopped short of the axes; it is now clipped at the edge. Polynomial regression curves and bands were drawn as about 16 straight pieces
- Charts: labels on a horizontal log axis overlapped when it spanned four or five decades (meta-analysis plots with very wide intervals); such axes now label the decades only
- Charts in HTML reports: line thickness and dash patterns were ignored, filled squares and diamonds were black whatever their colour (hiding the confidence interval of the pooled estimate in meta-analysis plots), and bold or italic text was drawn plain
- Histogram: a value equal to a bin's upper limit was counted in the bin above, and inconsistently so because of floating-point error in the bin edges (the help's IgM example had 3 and 120 in its first two bins where the documented rule gives 10 and 113)
- Histogram: the x axis is again labelled at the bin mid-points, as its title says and as earlier versions did, rather than at round values that fell between the bars
- Box and whisker plot: the quartiles were the (np + 0.5)th ordered values, which matched neither the descriptive statistics nor the help nor earlier versions; they are now the conventional p(n + 1)th values (centile type 2)
- Stacked bar charts: the legend listed the value columns as well as the bar labels (seven entries for four segments in the help's example) and the fourth segment was filled with the first colour
- Scatter chart with more than one series and a legend failed with "Don't know how to draw style's shape" the first time it was drawn
- Kaplan-Meier: "Save estimates and CIs to worksheet" wrote a spurious extra first row for each group (the first time repeated, marked as a non-event)
- Charts: a linear axis scale built directly rather than by the automatic scaler recursed until the stack overflowed; no menu route that reaches it was found, so this is a latent crash
- Error bar plot: with two or more series, the default option that moves overlapping bars apart moved the later series' bars, markers and lines dozens of units along the axis, usually off the chart
- LOESS: with missing values the curve was drawn against the wrong x values, and the saved fits, standard errors and residuals were shorter than the data and out of step with it from the first missing row; the "Plot fits" options and the confidence level were ignored (the fit and a band of 2 standard errors were always drawn)
- Analyses run through R: accented and other non-ASCII characters in column titles and labels were turned into "?", a Windows user name containing such a character stopped R from running at all, a double quote or backslash in a column title stopped the analysis, and results containing missing values could not be read back
- Method comparison regression looked only in its own R library, so it ignored an installed mcr package and downloaded another copy over an unencrypted connection; it now uses installed packages and downloads over https
- The Help buttons of the Export Graphic dialog and of the regression predictor prompt, and two unlisted functions, asked for topics that do not exist in the help file
- The tabs for open workbooks and reports kept a fixed height of 25 pixels whatever the display scaling, so at 150% their captions lost everything below the baseline (underscores vanished) and at 200% or more most of each caption was cut off; the tabs, and the panel they share with the toolbar, now take their height from the text
- Charts drawn by R (LOESS, method comparison regression) filled only the top left of their picture on a display with scaling above 100%, about two fifths of it at 225%, because R was started as a program that is not DPI-aware
- The logo shown while StatsDirect starts was stretched sideways on a display with scaling above 100% (to nearly twice its width at 250%) and was not enlarged with the display
- Agreement > Continuous never printed the limits of agreement for two columns
- The distribution calculator's critical values of Spearman's rho were wrong for 71 or more pairs (for 100 pairs, 0.699946 at every P), and an error was shown where the critical value is rho = 1; its critical values and P now include the observed statistic for more than 10 pairs too, as they already did for 10 or fewer
- Help > Check for Updates never finished, in version 4.0.5 as well: the downloaded page was written to the console instead of being examined, so the dialog stayed at "Contacting www.statsdirect.com..." and the check at start-up could not announce a new version. The same fault stopped the check for a newer R. Once examined, the page could still never yield a newer version: the running version was read as invalid because of the source revision that current .Net SDKs append to it, and only versions numbered 4 were looked for
- Data > Cleaning and Encoding > Search and Replace (Basic), and the Count action of Search and Replace (Advanced), failed with a script compilation error
- The Release build configuration tried to sign the installers, so failed without a signing key; only ReleaseWithSigning signs now
- Setup removed the .Net runtime it depends on, in versions 4.0.5 and earlier test builds of 5.0.0: uninstalling StatsDirect uninstalled the bundled .Net Windows Desktop runtime even when other programs used it, and installing a newer setup over an older one that bundles the same runtime left StatsDirect unable to start ("You must install or update .NET to run this application"), because the old setup removed the runtime after the new one had found it present. Setup now installs the runtime when it is missing and otherwise leaves it alone, and no longer keeps a 60 MB copy of the runtime installer in the package cache
- The installer's own runtime check still asked for .NET 6, so a .msi run on its own would have installed on a machine whose runtime is too old to start the program; it now requires the .NET 10 Desktop Runtime and says so

##[v4.0.5] 2025-12-05

### Changed
- DevExpress.Win.RichEdit updated to 25.1.7
- Runs on .Net 8, rather than .Net 6 (which is no longer supported)
- Dependencies updated
- WiX v6 used for Windows Installer preparation, up from v5
- Repository prepared for open source release

##[v4.0.4] 2024-06-24

### Changed
- DevExpress.Win.RichEdit updated to 24.1.3

##[v4.0.3] 2024-06-10
### Changed
- Removed check of Windows version from installer.

### Fixed
- Update to check update.aspx for a new update if available, fixes [#23](https://github.com/statsdirect/statsdirect4/issues/23)
- Added more information in message about missing .Net 6 in Installer [#24](https://github.com/statsdirect/statsdirect4/issues/24)

##[v4.0.2] 2024-06-07
### Changed
- Updated website check for newer version, changed major version check from 3 to 4

##[v4.0.1] 2024-06-06
### Changed
- Updated where the MSI and installer are compiled to. Now compiled to \ReleaseBuilds folder.
- Added sided option to Kruskal-Wallis analysis [#19](https://github.com/statsdirect/statsdirect4/issues/19)

##[v4.0.0] 2024-05-23
### Changed

### Fixed
- Dialog artefact in Kruskal-Wallis follow-on function transitions [#21](https://github.com/statsdirect/statsdirect4/issues/21)
- Dialog dropdowns for follow-on functions need making longer to accommodate function title [#20](https://github.com/statsdirect/statsdirect4/issues/20)
- Context sensitive help no longer works in the dialog boxes for follow-on functions [#18](https://github.com/statsdirect/statsdirect4/issues/18)
- SD v4 - Excel integration not working on clean systems [#17](https://github.com/statsdirect/statsdirect4/issues/17)
- SD - 4 Testing - Analysis - Non-Parametric - LOESS - reporting error [#15](https://github.com/statsdirect/statsdirect4/issues/15)
- Scaling issue in dialogue area [#12](https://github.com/statsdirect/statsdirect4/issues/12)
- SD4 - tab issue in report generation [#7](https://github.com/statsdirect/statsdirect4/issues/7)
- SD4 - Update CHM [#6](https://github.com/statsdirect/statsdirect4/issues/6)
- SD4 - Wordpad is not invokable - issue with path [#4](https://github.com/statsdirect/statsdirect4/issues/4)

### Deprecated

### Removed

### Added

### Security
