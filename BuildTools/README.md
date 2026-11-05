# Desktop help publication records

The Help Editor verifies the complete help tree in `StatsDirectUI/Assets/Help` when publishing or when explicitly verifying a delivery. A normal application build checks the unfinished-publication marker; it does not rehash the delivery receipt. The source tree retains its receipt and build metadata. The application copies reader files only: publication-control JSON is excluded, while the viewer, styles, offline MathJax and equation artwork remain included.

The repository-root `.statsdirect-desktop-help.publishing.json` is temporary transaction state and is ignored by Git. `StatsDirectDesktopHelp.targets` checks for either a file or directory at that name and stops the application build while publication is incomplete. Finish or recover the publication through the Help Editor; ignoring the marker does not bypass that guard.

After an explicitly reviewed first replacement of Flare output, `.statsdirect-desktop-help-adoption.json` is a persistent provenance record. Review and commit it alongside the delivered help. It is deliberately not ignored or created by application packaging. No adoption is performed by changing these packaging rules.
