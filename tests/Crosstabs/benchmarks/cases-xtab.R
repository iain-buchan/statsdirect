# Tables for the Crosstabs report, written as a file of cases:
#   case <name> <kind> <rows> <columns> <strata> <level> <row scores or -> <column scores or ->      kind: rc or rck
#   a row of counts for each row of the table, stratum after stratum
# usage: Rscript cases-xtab.R <output>
args <- commandArgs(trailingOnly = TRUE)
out <- file(args[1], "w")
put <- function(name, kind, t, level = 0.95, rs = NULL, cs = NULL) {
  if (length(dim(t)) == 2) dim(t) <- c(dim(t), 1)
  sc <- function(s) if (is.null(s)) "-" else paste(format(s, digits = 15, trim = TRUE), collapse = ",")
  cat(sprintf("case\t%s\t%s\t%d\t%d\t%d\t%s\t%s\t%s\n", name, kind, dim(t)[1], dim(t)[2], dim(t)[3], format(level), sc(rs), sc(cs)), file = out)
  for (k in seq_len(dim(t)[3])) write.table(format(matrix(t[, , k], dim(t)[1], dim(t)[2]), digits = 15, trim = TRUE), out, sep = "\t", row.names = FALSE, col.names = FALSE, quote = FALSE)
}
set.seed(27182)
levels <- c(0.95, 0.9, 0.99)
n <- 0
# drawn tables: from independence and from association along the diagonal, thin and well filled
for (size in list(c(2, 2), c(2, 3), c(3, 2), c(3, 3), c(2, 5), c(4, 3), c(3, 5), c(5, 5), c(6, 4))) for (total in c(12, 40, 150, 900)) for (assoc in c(0, 0.8)) {
  n <- n + 1
  r <- size[1]; cc <- size[2]
  p <- outer(runif(r, 0.5, 1.5), runif(cc, 0.5, 1.5)) * exp(assoc * outer(seq_len(r) / r, seq_len(cc) / cc) * 3)
  t <- matrix(rmultinom(1, total, p), r, cc)
  scores <- n %% 3 == 0
  put(sprintf("drawn%03d", n), "rc", t, levels[1 + n %% 3], if (scores) round(cumsum(runif(r, 0.5, 3)), 1), if (scores) round(cumsum(runif(cc, 0.5, 3)), 1))
}
# at the limits
put("two.identity", "rc", matrix(c(1, 0, 0, 1), 2))
put("two.perfect", "rc", matrix(c(12, 0, 0, 9), 2))
put("two.opposite", "rc", matrix(c(0, 7, 11, 0), 2))
put("two.one", "rc", matrix(c(1, 1, 1, 1), 2))
put("two.zero", "rc", matrix(c(8, 0, 5, 3), 2))
put("same.rows", "rc", matrix(c(4, 4, 4, 6, 6, 6, 10, 10, 10), 3))
put("empty.column", "rc", matrix(c(5, 2, 4, 0, 0, 0, 3, 6, 4), 3))
put("empty.row", "rc", matrix(c(5, 0, 4, 3, 0, 6, 7, 0, 2, 1, 0, 8), 3))
put("empty.both", "rc", matrix(c(5, 0, 4, 0, 0, 0, 7, 0, 2, 1, 0, 8), 3))
put("one.row", "rc", matrix(c(4, 6, 10), 1))
put("one.column", "rc", matrix(c(4, 6, 10), 3))
put("diagonal", "rc", diag(c(5, 7, 3, 6)))
put("antidiagonal", "rc", diag(c(5, 7, 3, 6))[, 4:1])
put("fractions", "rc", matrix(c(1.5, 3.25, 2, 4, 0.75, 6.5), 2))
put("large", "rc", matrix(c(1200, 800, 450, 300, 950, 700, 150, 400, 1350), 3))
put("sparse.wide", "rc", matrix(c(1, 0, 0, 2, 0, 1, 1, 0, 0, 0, 3, 0, 1, 1, 0, 0, 0, 2), 3))
put("scores.negative", "rc", matrix(c(9, 4, 2, 5, 6, 5, 2, 4, 9), 3), 0.95, c(-2, 0.5, 7), c(3, 1, 10))
put("scores.reversed", "rc", matrix(c(9, 4, 2, 5, 6, 5, 2, 4, 9), 3), 0.95, c(3, 2, 1), c(1, 2, 3))
put("labels.unequal", "rc", matrix(c(5, 2, 4, 3, 6, 4), 3))
put("ten.by.two", "rc", matrix(rpois(20, 6), 10), 0.9)
# tables in strata
for (k in c(2, 3, 5)) for (total in c(30, 200)) for (rep in 1:3) {
  n <- n + 1
  base <- rbeta(k, 3, 6)
  t <- array(0, c(2, 2, k))
  for (s in 1:k) { m <- round(total * runif(2, 0.4, 1.6) / k) + 1; e <- rbinom(2, m, plogis(qlogis(base[s]) + c(0, 0.7))); t[, , s] <- rbind(c(m[1] - e[1], e[1]), c(m[2] - e[2], e[2])) }
  put(sprintf("strata%03d", n), "rck", t, levels[1 + n %% 3])
}
for (size in list(c(2, 3), c(3, 3), c(4, 3), c(3, 5))) for (k in c(2, 4)) for (total in c(40, 300)) {
  n <- n + 1
  r <- size[1]; cc <- size[2]
  t <- array(0, c(r, cc, k))
  for (s in 1:k) { p <- outer(runif(r, 0.5, 1.5), runif(cc, 0.5, 1.5)) * exp(outer(seq_len(r) / r, seq_len(cc) / cc) * 2); t[, , s] <- matrix(rmultinom(1, round(total / k), p), r, cc) }
  scores <- n %% 2 == 0
  put(sprintf("strata%03d", n), "rck", t, 0.95, if (scores) round(cumsum(runif(r, 0.5, 3)), 1), if (scores) round(cumsum(runif(cc, 0.5, 3)), 1))
}
put("strata.zero", "rck", array(c(5, 0, 3, 4, 0, 6, 2, 5, 7, 1, 0, 9), c(2, 2, 3)))
put("strata.small", "rck", array(c(1, 0, 0, 0, 1, 1, 0, 0, 3, 2, 4, 5, 6, 1, 2, 8), c(2, 2, 4)))
put("strata.noevents", "rck", array(c(6, 9, 0, 0, 5, 3, 2, 4, 7, 8, 1, 3), c(2, 2, 3)))
put("strata.onerow", "rck", array(c(4, 0, 6, 0, 5, 0, 3, 2, 4, 5, 1, 6, 2, 3, 4, 1, 2, 5), c(2, 3, 3)))
put("strata.one", "rck", array(c(1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 2, 4, 1, 5, 2, 6, 3, 1, 2, 4, 1, 3, 5, 2, 2, 1, 4), c(3, 3, 3)))
close(out)
cat("cases written to", args[1], "\n")
