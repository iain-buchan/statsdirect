#if USE_R
using System;
using org.rosuda.REngine;
using org.rosuda.REngine.Rserve;

public class jt
{
	[STAThread]
	public static void  Main(System.String[] args)
	{
		try
		{
			RConnection c = new RConnection((args.Length > 0)?args[0]:"127.0.0.1");
			
			//UPGRADE_WARNING: At least one expression was used more than once in the target code. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1181'"
			System.IO.StreamReader ir = new System.IO.StreamReader(new System.IO.StreamReader(System.Console.OpenStandardInput(), System.Text.Encoding.Default).BaseStream, new System.IO.StreamReader(System.Console.OpenStandardInput(), System.Text.Encoding.Default).CurrentEncoding);
			System.String s = null;
			System.Console.Out.Write("> ");
			while ((s = ir.ReadLine()).Length > 0)
			{
				if (s.Equals("shutdown"))
				{
					System.Console.Out.WriteLine("Sending shutdown request");
					c.shutdown();
					System.Console.Out.WriteLine("Shutdown successful. Quitting console.");
					return ;
				}
				else
				{
					REXP rx = c.parseAndEval(s);
					System.Console.Out.WriteLine("result(debug): " + rx.toDebugString());
				}
				System.Console.Out.Write("> ");
			}
		}
		catch (RserveException rse)
		{
			System.Console.Out.WriteLine(rse);
			/*		} catch (REXPMismatchException mme) {
			System.out.println(mme);
			mme.printStackTrace(); */
		}
		catch (System.Exception e)
		{
			SupportClass.WriteStackTrace(e, Console.Error);
		}
	}
}
#endif