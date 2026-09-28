# Tables for Fisher's exact test that are too large to be tabulated from 0, with the first count from far below what is expected to
# far above it, and tables with the same totals both ways; and the P values from the hypergeometric distribution.
# usage: Rscript --vanilla cases-fisher-large.R cases-fisher-large.txt r-fisher-large.txt
args <- commandArgs(trailingOnly = TRUE)
set.seed(20260930)
cases <- character(0); out <- character(0)
put <- function(key, value) out <<- c(out, paste0(key, "\t", if (is.character(value)) paste0("\"", value, "\"") else format(value, digits = 15, scientific = TRUE)))
i <- 0
give <- function(t) {
  i <<- i + 1
  name <- sprintf("large%03d", i)
  cases <<- c(cases, paste(c("fisher", name, format(t, scientific = FALSE, trim = TRUE)), collapse = "\t"))
  u <- t; if (u[1] > u[4]) u[c(1, 4)] <- u[c(4, 1)]; if (u[2] > u[3]) u[c(2, 3)] <- u[c(3, 2)]
  a <- u[1]; p <- u[1] + u[2]; q <- u[3] + u[4]; r <- u[1] + u[3]; n <- p + q; e <- p * r / n
  s <- sqrt(p * q / n * r / n * (n - r) / (n - 1))
  support <- max(0, r - q, floor(min(a, e - 60 * s) - 60 * s - 2)):min(p, r, ceiling(max(a, e + 60 * s) + 60 * s + 2))
  prob <- dhyper(support, p, q, r); observed <- dhyper(a, p, q, r)
  one <- if (a > e) phyper(a - 1, p, q, r, lower.tail = FALSE) else phyper(a, p, q, r)
  key <- paste0("fisher|", name)
  put(paste0(key, "|tail_1"), if (a > e) "(upper tail)" else "(lower tail)")
  put(paste0(key, "|p_1"), one); put(paste0(key, "|p_1d"), min(1, 2 * one))
  put(paste0(key, "|p_2"), min(1, sum(prob[prob <= observed * (1 + 1e-7)])))
  put(paste0(key, "|mid_p"), one - observed / 2); put(paste0(key, "|mid_p_2"), min(1, 2 * (one - observed / 2)))
}
for (n in c(3000, 20000, 3e5, 3e6, 2e7, 2e8, 1e9, 2e9)) for (j in 1:14) {
  n1 <- round(n * runif(1, 0.1, 0.9)); n2 <- n - n1
  r <- round(n * runif(1, 0.1, 0.9))
  sd <- sqrt(n1 * n2 / n * r / n * (n - r) / (n - 1))
  a <- round(n1 * r / n + c(-37, -30, -20, -8, -3, -1, -0.2, 0, 0.3, 1.5, 4, 9, 25, 36)[j] * sd)
  a <- max(0, r - n2, min(a, n1, r))
  give(c(a, n1 - a, r - a, n2 - (r - a)))
}
# the same totals both ways: the two tails have tables of the same probability
for (k in c(2000, 50000, 1e6, 3e7, 5e8)) for (z in c(0, 0.4, 1, 2, 3.5, 6, 30)) {
  a <- round(k / 2 - z * sqrt(k) / 2 / sqrt(2))
  give(c(a, k - a, k - a, a))
  give(c(k - a, a, a, k - a))
}
# a row or a column of few
for (t in list(c(3, 5000000, 2, 9000000), c(0, 4000, 5, 3000000), c(1, 1, 100000000, 100000000), c(2000, 3, 1500, 1), c(1, 900000000, 0, 900000000))) give(t)
writeLines(cases, args[1]); writeLines(out, args[2])
cat(length(cases), "tables\n")
