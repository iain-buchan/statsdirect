# StatsDirect recipe: Tukey hinges with R's ordinary boxplot().
# data: mets... = data
# settings: box_settings = boxWhisker

setting <- function(name, default = NULL) {
  if (!is.null(box_settings[[name]])) box_settings[[name]] else default
}
groups <- lapply(mets, function(x) x[!is.na(x)])
if (!length(groups) || any(!vapply(groups, function(x)
    is.numeric(x) && length(x) > 0 && all(is.finite(x)), logical(1))))
  stop("Select numeric columns containing finite observations.")
labels <- as.character(unlist(setting("seriesTitles", names(mets))))
if (!length(labels)) labels <- paste("Series", seq_along(groups))
if (length(labels) != length(groups)) stop("Supply one label per series.")
horizontal <- setting("horizontal", TRUE)
inner <- setting("innerFence", TRUE)
outer <- setting("outerFence", TRUE)
positions <- if (horizontal) rev(seq_along(groups)) else seq_along(groups)
wrap <- function(text, width) vapply(text, function(x)
  paste(strwrap(x, width = width), collapse = "\n"), character(1))
displayed_labels <- wrap(labels, if (horizontal) 24 else max(8, floor(60 / length(groups))))
main <- wrap(setting("title", "Tukey hinges box plot"), 60)
value_title <- wrap(setting("axisTitle", "Value"), if (horizontal) 55 else 40)
text_lines <- function(text) max(1L, lengths(strsplit(text, "\n", fixed = TRUE)))
limits <- c(setting("valueMinimum"), setting("valueMaximum"))
explicit <- length(limits) == 2 && all(is.finite(limits)) && limits[1] < limits[2]
if (!explicit) limits <- range(unlist(groups))
scale <- setting("valueScale", 0L)
logarithmic <- scale %in% c(1L, 2L)
if (logarithmic && any(c(limits, unlist(groups)) <= 0))
  stop("A logarithmic axis requires positive observations and axis limits.")

# R supplies the hinges, median and nearest-observation whiskers. The two
# optional fence classes below retain StatsDirect's hollow/filled outlier marks.
box_plot <- local({
  old <- par(c("mar", "family", "cex.main", "cex.lab", "cex.axis"))
  on.exit(par(old))
  par(mar = c(if (horizontal) 4 + text_lines(value_title) else 3 + text_lines(displayed_labels),
              if (horizontal) 8 else 4 + text_lines(value_title), 2 + text_lines(main), 1),
      family = if (names(dev.cur()) == "svg") "Arial" else "sans",
      cex.main = 12 / par("ps"), cex.lab = 10 / par("ps"), cex.axis = 9 / par("ps"))
  box_plot <- boxplot(groups, names = labels, at = positions,
    range = if (inner) 1.5 else if (outer) 3 else 0,
    horizontal = horizontal, ylim = limits, outline = FALSE, axes = FALSE,
    main = main, xlab = if (horizontal) value_title else "",
    ylab = if (horizontal) "" else value_title,
    log = if (logarithmic) { if (horizontal) "x" else "y" } else "",
    yaxs = if (explicit) "i" else "r", boxfill = NA, boxwex = 0.6,
    medlwd = 1, whisklty = 1, frame.plot = FALSE)
  side <- if (horizontal) 1L else 2L
  ticks <- axTicks(side)
  interval <- setting("valueInterval"); first <- setting("valueFirstTick")
  if (length(interval) == 1 && is.finite(interval) && interval > 0 &&
      length(first) == 1 && is.finite(first)) {
    ends <- c(ceiling((limits[1] - first) / interval - 1e-10),
              floor((limits[2] - first) / interval + 1e-10))
    if (diff(ends) >= 0 && diff(ends) <= 10000) ticks <- first + seq(ends[1], ends[2]) * interval
  } else if (scale == 1L) {
    powers <- c(ceiling(log2(limits[1])), floor(log2(limits[2])))
    if (powers[2] >= powers[1]) ticks <- 2^seq(powers[1], powers[2])
  }
  if (setting("valueGrid", FALSE)) {
    style <- setting("valueGridStyle", 0L)
    if (horizontal) abline(v = ticks, col = "grey80", lty = if (style == 4L) "311111" else style + 1L) else
      abline(h = ticks, col = "grey80", lty = if (style == 4L) "311111" else style + 1L)
  }
  axis(side, at = ticks, labels = format(ticks, trim = TRUE, drop0trailing = TRUE), las = 1)
  axis(if (horizontal) 2L else 1L, at = positions, labels = displayed_labels, las = 1, tick = FALSE)
  marker <- setting("valueMarker")
  if (length(marker) == 1 && is.finite(marker) && (!logarithmic || marker > 0)) {
    if (horizontal) abline(v = marker) else abline(h = marker)
  }
  if (setting("boxAxes", FALSE)) box()
  point <- function(x, at, pch) {
    if (horizontal) points(x, rep(at, length(x)), pch = pch) else points(rep(at, length(x)), x, pch = pch)
  }
  for (i in seq_along(groups)) {
    x <- groups[[i]]; hinges <- box_plot$stats[c(2, 4), i]; spread <- diff(hinges)
    far <- outer & (x < hinges[1] - 3 * spread | x > hinges[2] + 3 * spread)
    near <- inner & !far & (x < hinges[1] - 1.5 * spread | x > hinges[2] + 1.5 * spread)
    point(x[near], positions[i], 1); point(x[far], positions[i], 16)
    if (setting("markMeanAndMedian", FALSE)) point(mean(x), positions[i], 4)
  }
  box_plot
})
