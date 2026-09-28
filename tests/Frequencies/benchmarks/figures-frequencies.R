# Benchmarks of the Frequencies analysis, worked out from the definitions: the frequency of each value, its percentage of the values
# that are not missing, and the cumulative frequency and percentage, in each of the four orders of the analysis.
# The values of a column of numbers are in order of size, and those of a column of text in the order of their characters (capital
# letters before small ones); in the orders of the frequencies, values that have the same frequency are in the order of the values.
# usage: Rscript --vanilla figures-frequencies.R cases-frequencies.txt r-frequencies.txt
args <- commandArgs(trailingOnly = TRUE)
lines <- readLines(args[1])
out <- character(0)
put <- function(key, value) out <<- c(out, paste0(key, "\t", value))
for (at in seq(1, length(lines), by = 2)) {
  head <- strsplit(lines[at], "\t")[[1]]
  name <- head[2]; kind <- head[3]
  cells <- strsplit(lines[at + 1], "\t")[[1]]
  missing <- cells == "NA"
  values <- cells[!missing]
  labels <- unique(values)
  count <- sapply(labels, function(l) sum(values == l))
  # the place of each value in the order of the values
  place <- if (kind == "numbers") rank(as.numeric(labels)) else match(labels, sort(labels, method = "radix"))
  orders <- list(value.asc = order(place), value.desc = order(-place), frequency.asc = order(count, place), frequency.desc = order(-count, place))
  for (o in names(orders)) {
    key <- paste0(name, "|", o)
    put(paste0(key, "|total"), length(cells))
    put(paste0(key, "|missing"), sum(missing))
    put(paste0(key, "|rows"), length(labels))
    i <- orders[[o]]
    cumulative <- cumsum(count[i])
    for (r in seq_along(i)) {
      put(paste0(key, "|", r, "|value"), paste0("\"", labels[i][r], "\""))
      put(paste0(key, "|", r, "|frequency"), count[i][r])
      put(paste0(key, "|", r, "|percent"), format(100 * count[i][r] / length(values), digits = 15))
      put(paste0(key, "|", r, "|cumulative"), cumulative[r])
      put(paste0(key, "|", r, "|cumulative.percent"), format(100 * cumulative[r] / length(values), digits = 15))
    }
  }
}
writeLines(out, args[2])
