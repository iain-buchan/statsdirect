#if USE_R
using System;
using org.rosuda.REngine;
using org.rosuda.REngine.Rserve;

[Serializable]
class TestException:System.Exception
{
	public TestException(System.String msg):base(msg)
	{
	}
}

namespace Rserve.Test
{
    public class test
    {
        [STAThread]
        public static void Main(System.String[] args)
        {
            try
            {
                RConnection c = new RConnection();

                System.Console.Out.WriteLine(">>" + c.eval("R.version$version.string").asString() + "<<");

                {
                    System.Console.Out.WriteLine("* Test string and list retrieval");
                    RList l = c.eval("{d=data.frame(\"huhu\",c(11:20)); lapply(d,as.character)}").asList();
                    int cols = l.Count;
                    int rows = l.at(0).length();
                    System.String[][] s = new System.String[cols][];
                    for (int i = 0; i < cols; i++)
                        s[i] = l.at(i).asStrings();
                    System.Console.Out.WriteLine("PASSED");
                }

                {
                    System.Console.Out.WriteLine("* Test NA/NaN support in double vectors...");
                    double R_NA = BitConverter.ToDouble(BitConverter.GetBytes(0x7ff00000000007a2L), 0);
                    // int R_NA_int = -2147483648; // just for completeness
                    double[] x = new double[] { 1.0, -1.0, 0.5, R_NA, System.Double.NaN, 3.5 };
                    c.assign("x", x);
                    System.String nas = c.eval("paste(capture.output(print(x)),collapse='\\n')").asString();
                    System.Console.Out.WriteLine(nas);
                    if (!nas.Equals("[1]  1.0 -1.0  0.5   NA  NaN  3.5"))
                        throw new TestException("NA/NaN assign+retrieve test failed");
                    System.Console.Out.WriteLine("PASSED");
                }

                {
                    System.Console.Out.WriteLine("* Test assigning of lists and vectors ...");
                    RList l = new RList();
                    l.put("a", new REXPInteger(new int[] { 0, 1, 2, 3 }));
                    l.put("b", new REXPDouble(new double[] { 0.5, 1.2, 2.3, 3.0 }));
                    System.Console.Out.WriteLine("  assign x=pairlist");
                    c.assign("x", new REXPList(l));
                    System.Console.Out.WriteLine("  assign y=vector");
                    c.assign("y", new REXPGenericVector(l));
                    System.Console.Out.WriteLine("  assign z=data.frame");
                    c.assign("z", REXP.createDataFrame(l));
                    System.Console.Out.WriteLine("  pull all three back to Java");
                    REXP x = c.parseAndEval("x");
                    System.Console.Out.WriteLine("  x = " + x);
                    x = c.eval("y");
                    System.Console.Out.WriteLine("  y = " + x);
                    x = c.eval("z");
                    System.Console.Out.WriteLine("  z = " + x);
                    System.Console.Out.WriteLine("PASSED");
                }

                {
                    // factors
                    System.Console.Out.WriteLine("* Test support of factors");
                    REXP f = c.parseAndEval("factor(paste('F',as.integer(runif(20)*5),sep=''))");
                    System.Console.Out.WriteLine("  f=" + f);
                    System.Console.Out.WriteLine("  isFactor: " + f.Factor + ", asFactor: " + f.asFactor());
                    if (!f.Factor || f.asFactor() == null)
                        throw new TestException("factor test failed");
                    System.Console.Out.WriteLine("  singe-level factor used to degenerate:");
                    f = c.parseAndEval("factor('foo')");
                    System.Console.Out.WriteLine("  isFactor: " + f.Factor + ", asFactor: " + f.asFactor());
                    if (!f.Factor || f.asFactor() == null)
                        throw new TestException("single factor test failed (not a factor)");
                    if (!f.asFactor().at(0).Equals("foo"))
                        throw new TestException("single factor test failed (wrong value)");
                    System.Console.Out.WriteLine("  test factors with null elements contents:");
                    c.assign("f", new REXPFactor(new RFactor(new System.String[] { "foo", "bar", "foo", "foo", null, "bar" })));
                    f = c.parseAndEval("f");
                    if (!f.Factor || f.asFactor() == null)
                        throw new TestException("factor assign-eval test failed (not a factor)");
                    System.Console.Out.WriteLine("  f = " + f.asFactor());
                    f = c.parseAndEval("as.factor(c(1,'a','b',1,'b'))");
                    System.Console.Out.WriteLine("  f = " + f);
                    if (!f.Factor || f.asFactor() == null)
                        throw new TestException("factor test failed (not a factor)");
                    System.Console.Out.WriteLine("PASSED");
                }


                {
                    System.Console.Out.WriteLine("* Double round-trip test");
                    double[] x = c.eval("array(-1234.5678)").asDoubles();
                    byte[] returned = BitConverter.GetBytes(x[0]);
                    byte[] actual = BitConverter.GetBytes(-1234.5678);
                    if (x[0] != -1234.5678)
                        throw new TestException("-1234.5678 incorrectly returned - is there a double endianness issue?");
                    System.Console.Out.WriteLine("PASSED");
                }

                {
                    System.Console.Out.WriteLine("* Lowess test");
                    c.eval("set.seed(0)");
                    double[] x = c.eval("rnorm(100)").asDoubles();
                    double[] y = c.eval("rnorm(100)").asDoubles();
                    c.assign("x", x);
                    c.assign("y", y);
                    RList l = c.parseAndEval("lowess(x,y)").asList();
                    System.Console.Out.WriteLine("  " + l);
                    x = l.at("x").asDoubles();
                    y = l.at("y").asDoubles();
                    System.Console.Out.WriteLine("PASSED");
                }

                {
                    // multi-line expressions
                    System.Console.Out.WriteLine("* Test multi-line expressions");
                    if (c.eval("{ a=1:10\nb=11:20\nmean(b-a) }\n").asInteger() != 10)
                        throw new TestException("multi-line test failed.");
                    System.Console.Out.WriteLine("PASSED");
                }
                {
                    System.Console.Out.WriteLine("* Matrix tests\n  matrix: create a matrix");
                    int m = 100, n = 100;
                    double[] mat = new double[m * n];
                    int i = 0;
                    while (i < m * n)
                        mat[i++] = i / 100;
                    System.Console.Out.WriteLine("  matrix: assign a matrix");
                    c.assign("m", mat);
                    c.voidEval("m<-matrix(m," + m + "," + n + ")");
                    System.Console.Out.WriteLine("matrix: cross-product");
                    double[][] mr = c.parseAndEval("crossprod(m,m)").asDoubleMatrix();
                    System.Console.Out.WriteLine("PASSED");
                }

                {
                    System.Console.Out.WriteLine("* Test serialization and raw vectors");
                    byte /* sbyte */[] b = c.eval("serialize(ls, NULL, ascii=FALSE)").asBytes();
                    System.Console.Out.WriteLine("  serialized ls is " + b.Length + " bytes long");
                    c.assign("r", new REXPRaw(b));
                    System.String[] s = c.eval("unserialize(r)()").asStrings();
                    System.Console.Out.WriteLine("  we have " + s.Length + " items in the workspace");
                    System.Console.Out.WriteLine("PASSED");
                }
                c.close();
            }
            catch (RserveException rse)
            {
                System.Console.Out.WriteLine(rse);
            }
            catch (REXPMismatchException mme)
            {
                System.Console.Out.WriteLine(mme);
                SupportClass.WriteStackTrace(mme, Console.Error);
            }
            catch (TestException te)
            {
                System.Console.Error.WriteLine("** Test failed: " + te.Message);
                SupportClass.WriteStackTrace(te, Console.Error);
            }
            catch (System.Exception e)
            {
                SupportClass.WriteStackTrace(e, Console.Error);
            }
        }
    }
}
#endif