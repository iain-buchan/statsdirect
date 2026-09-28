# Benchmarks of the Exact Tests on Counts menu, worked out from the definitions.
# usage: Rscript --vanilla figures-exact.R cases-exact.txt r-exact.txt
args <- commandArgs(trailingOnly = TRUE)
lines <- readLines(args[1])
out <- character(0)
put <- function(key, value) out <<- c(out, paste0(key, "\t", if (is.character(value)) paste0("\"", value, "\"") else if (is.na(value)) "NA" else if (is.infinite(value)) (if (value > 0) "Inf" else "-Inf") else format(value, digits = 15, scientific = TRUE)))

# the one side confidence level that the report names when all or none of the observations are on one side
oneSided <- function(level) paste0(" [", format(round(100 * (level + (1 - level) / 2), 1), nsmall = 0), "% one-sided CI]")

sign <- function(key, n, r, level) {
  if (r > n) { t <- r; r <- n; n <- t }
  less <- min(r, n - r)
  p1 <- pbinom(less, n, 0.5)
  put(paste0(key, "|sample"), n); put(paste0(key, "|sample_1"), r)
  put(paste0(key, "|exact.1|prob_1"), p1)
  put(paste0(key, "|exact.1|prob_2"), min(1, 2 * p1))
  put(paste0(key, "|z"), max(0, abs(n / 2 - r) - 0.5) / sqrt(n / 4))
  # Clopper-Pearson limits from the beta distribution
  put(paste0(key, "|lower"), if (r == 0) 0 else qbeta((1 - level) / 2, r, n - r + 1))
  put(paste0(key, "|upper"), if (r == n) 1 else qbeta(1 - (1 - level) / 2, r + 1, n - r))
  put(paste0(key, "|prop"), r / n)
  put(paste0(key, "|ci"), 100 * level)
}

# the table arranged as the report arranges it: the cell a is no more than d, and b no more than c
arranged <- function(t) { a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]; if (a > d) { x <- a; a <- d; d <- x }; if (b > c) { x <- b; b <- c; c <- x }; c(a, b, c, d) }

fisher <- function(key, t) {
  # counts that are not whole numbers are rounded, a half to the even number
  u <- arranged(round(t)); a <- u[1]; b <- u[2]; c <- u[3]; d <- u[4]
  p <- a + b; q <- c + d; r <- a + c; n <- p + q
  if (p <= 0 || q <= 0 || r <= 0 || b + d <= 0) { put(paste0(key, "|ok"), 0); return(invisible()) }
  put(paste0(key, "|ok"), 1)
  put(paste0(key, "|tab3_a1"), a); put(paste0(key, "|tab3_b1"), b); put(paste0(key, "|tab3_a2"), c); put(paste0(key, "|tab3_b2"), d)
  e <- p * r / n
  put(paste0(key, "|exp_a"), e)
  # a has the hypergeometric distribution: r taken from p and q
  support <- max(0, r - q):min(p, r)
  prob <- dhyper(support, p, q, r)
  observed <- dhyper(a, p, q, r)
  lower <- phyper(a, p, q, r); upper <- phyper(a - 1, p, q, r, lower.tail = FALSE)
  one <- if (a > e) upper else lower
  put(paste0(key, "|tail_1"), if (a > e) "(upper tail)" else "(lower tail)")
  put(paste0(key, "|p_1"), one)
  put(paste0(key, "|p_1d"), min(1, 2 * one))
  put(paste0(key, "|p_2"), min(1, sum(prob[prob <= observed * (1 + 1e-7)])))
  put(paste0(key, "|mid_p"), one - observed / 2)
  put(paste0(key, "|mid_p_2"), min(1, 2 * (one - observed / 2)))
  invisible(list(support = support, prob = prob))
}

mcnemar <- function(key, t, level) {
  b <- t[2]; c <- t[3]
  if (b + c <= 0) { put(paste0(key, "|ok"), 0); return(invisible()) }
  put(paste0(key, "|ok"), 1)
  x2 <- (b - c)^2 / (b + c)
  put(paste0(key, "|chi"), x2); put(paste0(key, "|chi_p"), pchisq(x2, 1, lower.tail = FALSE))
  y2 <- max(abs(b - c) - 1, 0)^2 / (b + c)
  put(paste0(key, "|yates_chi"), y2); put(paste0(key, "|yates_chi_p"), pchisq(y2, 1, lower.tail = FALSE))
  put(paste0(key, "|risk"), if (c > 0) b / c else Inf)
  # the limits of the ratio b / c from the Clopper-Pearson limits of the proportion b / (b + c)
  n <- b + c
  pl <- if (b == 0) 0 else qbeta((1 - level) / 2, b, n - b + 1)
  pu <- if (b == n) 1 else qbeta(1 - (1 - level) / 2, b + 1, n - b)
  put(paste0(key, "|from"), if (b == 0) 0 else pl / (1 - pl))
  put(paste0(key, "|to"), if (b == n) Inf else pu / (1 - pu))
  r <- max(b, c); s <- min(b, c)
  put(paste0(key, "|f"), r / (s + 1))
  # twice the probability of r or more of the pairs that differ being one way
  put(paste0(key, "|tail_2"), min(1, 2 * pbinom(r - 1, n, 0.5, lower.tail = FALSE)))
  put(paste0(key, "|pc"), 100 * level)
}

orci <- function(key, t, level) {
  t <- round(t)   # counts that are not whole numbers are rounded, a half to the even number
  a <- t[1]; b <- t[2]; c <- t[3]; d <- t[4]
  for (i in 1:4) put(paste0(key, "|", c("tab_a1", "tab_b1", "tab_a2", "tab_b2")[i]), t[i])
  put(paste0(key, "|odds"), if (a * d == 0 && b * c == 0) NA else if (a * d == 0) 0 else if (b * c == 0) Inf else a * d / (b * c))
  m1 <- a + b; n1 <- a + c; n0 <- b + d
  lo <- max(0, m1 - n0); hi <- min(m1, n1)
  if (lo == hi) {
    # an empty row or column: the table is the only one that its totals allow (fisher.test gives the same estimate, limits and P)
    for (k in c("eor", "llf", "llm")) put(paste0(key, "|", k), 0)
    for (k in c("ulf", "ulm")) put(paste0(key, "|", k), Inf)
    for (k in c("p1f", "p2f", "p2m")) put(paste0(key, "|", k), 1)
    put(paste0(key, "|p1m"), 0.5)
    put(paste0(key, "|pc"), 100 * level)
    return(invisible())
  }
  support <- lo:hi
  logd <- dhyper(support, n1, n0, m1, log = TRUE)
  dens <- function(logpsi) { v <- logd + logpsi * support; v <- exp(v - max(v)); v / sum(v) }
  mean <- function(logpsi) sum(support * dens(logpsi))
  up <- function(logpsi, half) { v <- dens(logpsi); sum(v[support > a]) + half * v[support == a] }     # a or more, the observed with its weight
  down <- function(logpsi, half) { v <- dens(logpsi); sum(v[support < a]) + half * v[support == a] }
  root <- function(f) uniroot(f, c(-60, 60), tol = 1e-13, extendInt = "yes", maxiter = 10000)$root
  alpha <- 1 - level
  put(paste0(key, "|eor"), if (a == lo) 0 else if (a == hi) Inf else exp(root(function(l) mean(l) - a)))
  put(paste0(key, "|llf"), if (a == lo) 0 else exp(root(function(l) up(l, 1) - alpha / 2)))
  put(paste0(key, "|ulf"), if (a == hi) Inf else exp(root(function(l) down(l, 1) - alpha / 2)))
  put(paste0(key, "|llm"), if (a == lo) 0 else exp(root(function(l) up(l, 0.5) - alpha / 2)))
  put(paste0(key, "|ulm"), if (a == hi) Inf else exp(root(function(l) down(l, 0.5) - alpha / 2)))
  prob <- exp(logd); observed <- prob[support == a]
  upper <- sum(prob[support >= a]); lower <- sum(prob[support <= a])
  put(paste0(key, "|p1f"), min(upper, lower))
  put(paste0(key, "|p2f"), min(1, sum(prob[prob <= observed * (1 + 1e-7)])))
  mid <- min(upper - observed / 2, lower - observed / 2)
  put(paste0(key, "|p1m"), mid)
  put(paste0(key, "|p2m"), min(1, 2 * mid))
  put(paste0(key, "|pc"), 100 * level)
}

prate <- function(key, x, time, level) {
  put(paste0(key, "|rate"), x / time)
  put(paste0(key, "|from"), if (x == 0) 0 else qgamma((1 - level) / 2, x) / time)
  put(paste0(key, "|to"), qgamma(1 - (1 - level) / 2, x + 1) / time)
  put(paste0(key, "|pc"), 100 * level)
}

for (line in lines) {
  f <- strsplit(line, "\t")[[1]]
  v <- as.numeric(f[-(1:2)])
  if (f[1] == "sign") sign(paste0("sign|", f[2]), v[1], v[2], v[3])
  if (f[1] == "fisher") {
    got <- fisher(paste0("fisher|", f[2]), v)
    fisher(paste0("fisherx|", f[2]), v)
    # the rows of the expanded report, for the tables that it lists: the probability of each value of a, and the two tails
    if (!is.null(got) && length(got$support) <= 400) {
      key <- paste0("fisherx|", f[2])
      for (i in seq_along(got$support)) {
        put(paste0(key, "|row.", i, "|a"), got$support[i])
        put(paste0(key, "|row.", i, "|ind_p"), got$prob[i])
        put(paste0(key, "|row.", i, "|lower"), sum(got$prob[1:i]))
        put(paste0(key, "|row.", i, "|upper"), sum(got$prob[i:length(got$prob)]))
      }
    }
  }
  if (f[1] == "mcnemar") mcnemar(paste0("mcnemar|", f[2]), v[1:4], v[5])
  if (f[1] == "orci") orci(paste0("orci|", f[2]), v[1:4], v[5])
  if (f[1] == "prate") prate(paste0("prate|", f[2]), v[1], v[2], v[3])
}
writeLines(out, args[2])
