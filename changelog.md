# Changelog

Changelog best practices reference: https://keepachangelog.com/en/1.0.0/

##[Unreleased]

### Changed
- DevExpress.Win.RichEdit updated to 26.1.4
- SpreadsheetGear updated to 9.3.84
- Dependencies updated

### Fixed
- Data > Cleaning and Encoding > Search and Replace (Basic), and the Count action of Search and Replace (Advanced), failed with a script compilation error
- The Release build configuration tried to sign the installers, so failed without a signing key; only ReleaseWithSigning signs now

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
