# Independent conditioning on Z instead of the chi-square denominator. Integrate positive
# terms, with relative accuracy and normal tails through 40 standard deviations.
NF <- function(t,df,delta) {
 if(t==0) return(pnorm(-delta))
 # At |t|<=1e-9 the gamma transition is too narrow in this coordinate. Taylor-expand
 # Phi(t*S-delta); E[S^2]=1 and the omitted cubic term is negligible at these inputs.
 if(abs(t)<=1e-9) {
   meanS<-if(df>=1e6) 1-1/(4*df)+1/(32*df^2) else exp(lgamma((df+1)/2)-lgamma(df/2)+.5*log(2/df))
   return(pnorm(-delta)+dnorm(delta)*(t*meanS+delta*t^2/2))
 }
 if(t>0) {lo<-max(-40,-delta); hi<-40; base<-pnorm(-delta)}
 else {lo<- -40;hi<-min(40,-delta);base<-0}
 if(lo>=hi) return(base)
 f <- function(z) {
  v <- df*((z+delta)/t)^2
  dnorm(z)*pgamma(v/2,df/2,lower.tail=t<0)
 }
 cuts <- c(lo,hi,seq(lo,hi,length.out=81))
 # Gamma transition: include points corresponding to its central and tail quantiles.
 cuts <- c(cuts,t*sqrt(qchisq(c(1e-12,.001,.1,.5,.9,.999,1-1e-12),df)/df)-delta)
 cuts <- sort(unique(cuts[cuts>=lo & cuts<=hi]))
 area <- function(tolerance) base+sum(vapply(seq_len(length(cuts)-1),function(i)
    integrate(f,cuts[i],cuts[i+1],abs.tol=tolerance/(length(cuts)-1),rel.tol=2e-10,subdivisions=1000)$value,0.0))
 estimate <- area(1e-14)
 if(estimate < 1e-8) area(max(1e-300,estimate*1e-12)) else estimate
}

# Run from this folder. Existing root values are starting guesses only: uniroot
# independently solves the probability equation again, using NF rather than StatsDirect or pt.
options(digits=17)
r <- read.delim('references.tsv')
root <- function(f,guess) {
 width <- 1e-5*max(1,abs(guess))
 uniroot(f,c(guess-width,guess+width),tol=2e-12*max(1,abs(guess)),extendInt='yes',maxiter=200)$root
}
for(i in seq_len(nrow(r))) {
 x<-r$x[i];df<-r$df[i];delta<-r$delta[i]
 if(r$kind[i]=='cdf') {
   r$expected[i]<-NF(x,df,delta);r$other[i]<-NF(-x,df,-delta)
 } else if(r$kind[i]=='quantile') {
   f<-if(x<=.5) function(t) NF(t,df,delta)-x else function(t) NF(-t,df,-delta)-(1-x)
   r$expected[i]<-root(f,r$expected[i]);r$other[i]<-0
 } else if(r$kind[i]=='power') {
   crit<-qt(x/2,df,lower.tail=FALSE)
   r$expected[i]<-NF(-crit,df,-delta)+NF(-crit,df,delta);r$other[i]<-0
 } else {
   r$expected[i]<-root(function(ncp) NF(-x,df,-ncp)-delta,r$expected[i])
   r$other[i]<-root(function(ncp) NF(x,df,ncp)-delta,r$other[i])
 }
}
write.table(r,'references.tsv',sep='\t',row.names=FALSE,quote=FALSE)
cat(nrow(r),'independent reference cases regenerated.\n')
