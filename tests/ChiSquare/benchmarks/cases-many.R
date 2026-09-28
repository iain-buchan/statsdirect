# 2 by k tables of very many subjects whose chi-square is small, and their figures from sums of squares.
# usage: Rscript --vanilla cases-many.R cases-many.txt r-many.txt
args <- commandArgs(trailingOnly = TRUE)
set.seed(11)
cases <- character(0); out <- character(0)
put <- function(key, value) out <<- c(out, paste0(key, "\t", format(value, digits = 15, scientific = TRUE)))
i <- 0
for (scale in c(1e3, 1e5, 1e6, 1e7, 1e8, 3e8)) for (j in 1:4) {
  i <- i + 1
  k <- sample(3:6, 1)
  n <- round(scale * runif(k, 1, 5))
  p <- runif(1, 0.2, 0.8)
  s <- round(n * p + rnorm(k) * sqrt(n * p * (1 - p)))
  f <- n - s
  v <- 1:k
  name <- sprintf("p%02d", i)
  cases <- c(cases, paste(c("chi2k", name, 1, k, format(as.vector(rbind(s, f, v)), scientific = FALSE, trim = TRUE)), collapse = "\t"))
  o <- cbind(s, f); e <- outer(rowSums(o), colSums(o)) / sum(o)
  x2 <- sum((o - e)^2 / e)
  pp <- sum(s) / sum(n)
  t <- sum(v * (s - n * pp))
  vb <- sum(n * v) / sum(n)
  t2 <- t^2 / (pp * (1 - pp) * sum(n * (v - vb)^2))
  key <- paste0("chi2k|", name)
  put(paste0(key, "|chi"), x2); put(paste0(key, "|z.1|chi_lin"), t2); put(paste0(key, "|z.1|non.1|chi_non"), x2 - t2)
  put(paste0(key, "|chi_p"), pchisq(x2, k - 1, lower.tail = FALSE))
}
writeLines(cases, args[1]); writeLines(out, args[2])
