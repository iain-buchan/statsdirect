# Independent references for large-noncentrality effect-size intervals.
# Run from this folder: Rscript --vanilla noncentral-t-integral.R
# R's pt uses a normal approximation above a noncentrality of about 37.6.
# Instead integrate over the standardized chi-square variable in
# T = (Z + delta) / sqrt(V / df), with independent Z~N(0,1), V~chi-square(df).
# Production uses the log of sqrt(V/df), a different coordinate and density calculation.
options(digits = 17)
F <- function(t, df, delta) {
  # For one or two df, integrate over sqrt(V) to remove the chi-square density's
  # endpoint singularity; its density is half-normal or Rayleigh, respectively.
  if (df <= 2) {
    upper <- sqrt(qchisq(1e-18, df, lower.tail=FALSE))
    f <- function(w) pnorm(t*w/sqrt(df)-delta) *
      if (df == 1) sqrt(2/pi)*exp(-w*w/2) else w*exp(-w*w/2)
    cuts <- c(0, upper, seq(0, upper, length.out=21))
    if (t != 0) cuts <- c(cuts, sqrt(df)*(delta+c(-8,0,8))/t)
    cuts <- sort(unique(cuts[cuts >= 0 & cuts <= upper]))
    return(sum(vapply(seq_len(length(cuts)-1), function(i)
      integrate(f, cuts[i], cuts[i+1], abs.tol=2e-14, rel.tol=2e-12, subdivisions=2000)$value, 0.0)))
  }
  scale <- sqrt(2 * df)
  bounds <- (c(qchisq(1e-18, df), qchisq(1e-18, df, lower.tail=FALSE)) - df) / scale
  f <- function(x) {
    v <- pmax(0, df + scale * x)
    pnorm(t * sqrt(v / df) - delta) * dchisq(v, df) * scale
  }
  cuts <- c(bounds, seq(max(bounds[1], -10), min(bounds[2], 10), length.out=21))
  # Include the transition of pnorm, including for a large t and small df.
  if (t != 0) {
    s <- (delta + c(-8, 0, 8)) / t
    cuts <- c(cuts, (df * s[s > 0]^2 - df) / scale)
  }
  cuts <- sort(unique(cuts[cuts >= bounds[1] & cuts <= bounds[2]]))
  sum(vapply(seq_len(length(cuts)-1), function(i)
    integrate(f, cuts[i], cuts[i+1], abs.tol=2e-14, rel.tol=2e-12, subdivisions=2000)$value, 0.0))
}
cdf <- expand.grid(df=c(1, 2, 10, 31, 32, 100, 1000, 1001, 5000, 10000, 1e6, 1e9), t=c(0, 5, 20, 40, 50, 100))
cdf$delta <- cdf$t + rep(c(-2, 0, 2), length.out=nrow(cdf))
cdf <- rbind(cdf, transform(cdf, t=-t, delta=-delta),
             data.frame(df=rep(c(10, 1000, 1001), each=4), t=20,
                        delta=rep(c(19.999999, 20, 20.000001, -20.000001), 3)))
cdf$p <- mapply(F, cdf$t, cdf$df, cdf$delta)
write.table(cdf, "noncentral-t-cdf.txt", sep="\t", quote=FALSE, row.names=FALSE)

limits <- expand.grid(df=c(1, 2, 10, 31, 32, 100, 1000, 1001, 5000, 10000, 1e6, 1e9), t=c(0, 5, 20, 40, 50, 100))
limits$alpha <- rep(c(.005, .025, .05), length.out=nrow(limits))
root <- function(t, df, p) {
  width <- max(10, abs(t) * 2)
  uniroot(function(delta) F(t, df, delta) - p, c(t-width, t+width),
          tol=1e-11, extendInt="downX")$root
}
limits$lower <- mapply(function(t, df, alpha) root(t, df, 1-alpha), limits$t, limits$df, limits$alpha)
limits$upper <- mapply(root, limits$t, limits$df, limits$alpha)
write.table(limits, "noncentral-t-limits.txt", sep="\t", quote=FALSE, row.names=FALSE)
cat(nrow(cdf), "CDF references and", nrow(limits), "pairs of noncentrality limits generated.\n")
