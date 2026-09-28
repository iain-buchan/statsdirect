# The cases of the benchmarks of the Exact Tests on Counts menu: a line has the kind of the analysis, a name, and what the analysis
# is given (sign: n, r, confidence level; fisher: a, b, c, d; mcnemar and orci: a, b, c, d, confidence level; prate: events, time
# at risk, confidence level).
# usage: Rscript --vanilla cases-exact.R cases-exact.txt
args <- commandArgs(trailingOnly = TRUE)
set.seed(20260928)
out <- character(0)
count <- c(sign = 0, fisher = 0, mcnemar = 0, orci = 0, prate = 0)
add <- function(kind, ...) {
  count[kind] <<- count[kind] + 1
  out <<- c(out, paste(c(kind, sprintf("%s%03d", kind, count[kind]), format(c(...), scientific = FALSE, trim = TRUE, digits = 15)), collapse = "\t"))
}
levels <- c(0.95, 0.99, 0.9)

# sign test: from 1 observation to a million, with none, all and half of them on one side
for (n in c(1, 2, 3, 5, 10, 17, 30, 100, 101, 1000, 1074, 1075, 5000, 100000, 1000000)) {
  for (r in unique(c(0, 1, floor(n / 2), ceiling(n / 2), floor(n / 2) - 1, floor(0.4 * n), floor(0.45 * n), floor(0.49 * n), n - 1, n))) {
    if (r >= 0 && r <= n) add("sign", n, r, sample(levels, 1))
  }
}
for (i in 1:60) { n <- sample(1:400, 1); add("sign", n, sample(0:n, 1), sample(levels, 1)) }

# 2 by 2 tables: small, of tens, of hundreds, of thousands; with empty cells; with the same totals both ways (the distribution is
# then the same from both ends); far from and near to independence
table22 <- function(most, association) {
  n <- sample(2:most, 1)
  p1 <- runif(1, 0.05, 0.95); p2 <- runif(1, 0.05, 0.95)
  if (association == 0) p2 <- p1
  n1 <- max(1, round(n * runif(1, 0.2, 0.8))); n2 <- max(1, n - n1)
  a <- rbinom(1, n1, p1); c <- rbinom(1, n2, p2)
  c(a, n1 - a, c, n2 - c)
}
tables <- list()
for (i in 1:60) tables[[length(tables) + 1]] <- table22(30, i %% 2)
for (i in 1:40) tables[[length(tables) + 1]] <- table22(300, i %% 2)
for (i in 1:30) tables[[length(tables) + 1]] <- table22(3000, i %% 2)
for (i in 1:14) tables[[length(tables) + 1]] <- table22(40000, i %% 2)
# the same totals both ways
for (k in c(3, 10, 50, 170, 171, 200, 400, 1000, 3000)) for (a in unique(pmax(0, round(k * c(0.5, 0.45, 0.4, 0.3))))) tables[[length(tables) + 1]] <- c(a, k - a, k - a, a)
for (k in c(7, 60, 500)) for (a in unique(round(k * c(0.5, 0.4)))) tables[[length(tables) + 1]] <- c(a, k - a, k + 1 - a, a)
# empty cells, and all in one cell
for (t in list(c(0, 5, 5, 0), c(5, 0, 0, 5), c(0, 0, 3, 4), c(3, 4, 0, 0), c(0, 3, 0, 4), c(3, 0, 4, 0), c(0, 10, 3, 20), c(10, 0, 3, 20), c(10, 3, 0, 20), c(10, 3, 20, 0),
               c(1, 0, 0, 1), c(1, 1, 1, 1), c(0, 1, 1, 0), c(1, 0, 0, 0), c(0, 0, 0, 0), c(100, 0, 0, 100), c(0, 300, 300, 0), c(2000, 1, 1, 2000), c(1, 5000, 5000, 1)))
  tables[[length(tables) + 1]] <- t
for (t in tables) { add("fisher", t); add("orci", t, sample(levels, 1)); }
# counts that are not whole numbers
for (t in list(c(2.5, 3.5, 4.5, 5.5), c(0.5, 1.5, 2.5, 3.5), c(10.4, 3.6, 7.2, 12.49), c(0.4, 5, 6, 0.3))) { add("fisher", t); add("orci", t, sample(levels, 1)) }

# matched pairs: the pairs that differ are b and c
for (i in 1:60) { m <- sample(c(20, 200, 2000), 1); add("mcnemar", sample(0:m, 1), sample(0:m, 1), sample(0:m, 1), sample(0:m, 1), sample(levels, 1)) }
for (t in list(c(5, 0, 7, 3), c(5, 7, 0, 3), c(5, 1, 0, 3), c(5, 0, 1, 3), c(5, 10, 10, 3), c(0, 1, 1, 0), c(5, 9, 10, 3), c(50, 100000, 99000, 3), c(1, 3, 12, 1), c(1, 12, 3, 1), c(9, 0, 0, 4), c(9, 40, 0, 4), c(9, 0, 40, 4)))
  add("mcnemar", t, sample(levels, 1))

# Poisson rate: no events, few, many; events that are not whole numbers
for (x in c(0, 1, 2, 3, 5, 10, 17, 50, 100, 1000, 10000, 1000000, 100000000, 0.5, 2.5, 10.25)) for (time in c(1, 0.5, 12345.678, 1000000)) add("prate", x, time, sample(levels, 1))
writeLines(out, args[1])
