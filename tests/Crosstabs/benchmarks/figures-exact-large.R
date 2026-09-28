# Benchmarks of the Fisher-Freeman-Halton exact test for tables that the program used to refuse, and for tables at the limit of the
# keys of the method: the P value of fisher.test, or "refused" where it gives an error.
# usage: Rscript --vanilla figures-exact-large.R cases-exact-large.txt r-exact-large.txt
args <- commandArgs(trailingOnly = TRUE)
lines <- readLines(args[1])
out <- character(0)
at <- 1
while (at <= length(lines)) {
  head <- strsplit(lines[at], "\t")[[1]]
  if (head[1] == "case") {
    rows <- as.integer(head[4]); cols <- as.integer(head[5])
    t <- matrix(0, rows, cols)
    for (i in seq_len(rows)) t[i, ] <- as.numeric(strsplit(lines[at + i], "\t")[[1]])
    p <- tryCatch(format(fisher.test(t, workspace = 2e8)$p.value, digits = 15), error = function(e) "\"refused\"")
    out <- c(out, paste0(head[2], "|p\t", p))
    at <- at + rows
  }
  at <- at + 1
}
writeLines(out, args[2])
