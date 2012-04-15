#if USE_R
//
// PlotDemo demo - REngine and graphics
//
// $Id: PlotDemo.java 2767 2007-05-24 16:49:25Z urbanek $
//
using System;
using org.rosuda.REngine;
using org.rosuda.REngine.Rserve;
#if DEAD
/// <summary>A demonstration of the use of Rserver and graphics devices to create graphics in R, pull them into Java and display them. It is a really simple demo. </summary>
[Serializable]
public class PlotDemo:System.Windows.Forms.Control
{
	private class AnonymousClassWindowAdapter
	{
		// just so we can close the window
		public void  windowClosing(System.Object event_sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = true;
			System.Environment.Exit(0);
		}
	}
	[STAThread]
	public static void  Main(System.String[] args)
	{
		try
		{
			System.String device = "jpeg"; // device we'll call (this would work with pretty much any bitmap device)
			
			// connect to Rserve (if the user specified a server at the command line, use it, otherwise connect locally)
			RConnection c = new RConnection((args.Length > 0)?args[0]:"127.0.0.1");
			
			// if Cairo is installed, we can get much nicer graphics, so try to load it
			if (c.parseAndEval("suppressWarnings(require('Cairo',quietly=TRUE))").asInteger() > 0)
				device = "CairoJPEG";
			// great, we can use Cairo device
			else
				System.Console.Out.WriteLine("(consider installing Cairo package for better bitmap output)");
			
			// we are careful here - not all R binaries support jpeg
			// so we rather capture any failures
			REXP xp = c.parseAndEval("try(" + device + "('test.jpg',quality=90))");
			
			if (xp.inherits("try-error"))
			{
				// if the result is of the class try-error then there was a problem
				System.Console.Error.WriteLine("Can't open " + device + " graphics device:\n" + xp.asString());
				// this is analogous to 'warnings', but for us it's sufficient to get just the 1st warning
				REXP w = c.eval("if (exists('last.warning') && length(last.warning)>0) names(last.warning)[1] else 0");
				if (w.String)
					System.Console.Error.WriteLine(w.asString());
				return ;
			}
			
			// ok, so the device should be fine - let's plot - replace this by any plotting code you desire ...
			c.parseAndEval("data(iris); attach(iris); plot(Sepal.Length, Petal.Length, col=unclass(Species)); dev.off()");
			
			// There is no I/O API in REngine because it's actually more efficient to use R for this
			// we limit the file size to 1MB which should be sufficient and we delete the file as well
			xp = c.parseAndEval("r=readBin('test.jpg','raw',1024*1024); unlink('test.jpg'); r");
			
			// now this is pretty boring AWT stuff - create an image from the data and display it ...
			//UPGRADE_ISSUE: Method 'java.awt.Toolkit.createImage' was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1000_javaawtToolkit'"
			//UPGRADE_ISSUE: Method 'java.awt.Toolkit.getDefaultToolkit' was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1000_javaawtToolkit'"
			System.Drawing.Image img = Toolkit.getDefaultToolkit().createImage(xp.asBytes());
			
			//UPGRADE_TODO: Class 'java.awt.Frame' was converted to 'System.Windows.Forms.Form' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaawtFrame'"
			System.Windows.Forms.Form temp_frame;
			temp_frame = new System.Windows.Forms.Form();
			temp_frame.Text = "Test image";
			System.Windows.Forms.Form f = temp_frame;
			//UPGRADE_TODO: Method 'java.awt.Container.add' was converted to 'System.Windows.Forms.ContainerControl.Controls.Add' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaawtContaineradd_javaawtComponent'"
			System.Windows.Forms.Control temp_Control;
			temp_Control = new PlotDemo(img);
			f.Controls.Add(temp_Control);
			//UPGRADE_NOTE: Some methods of the 'java.awt.event.WindowListener' class are not used in the .NET Framework. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1308'"
			f.Closing += new System.ComponentModel.CancelEventHandler(new AnonymousClassWindowAdapter().windowClosing);
			//UPGRADE_ISSUE: Method 'java.awt.Window.pack' was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1000_javaawtWindowpack'"
			f.pack();
			//UPGRADE_TODO: Method 'java.awt.Component.setVisible' was converted to 'System.Windows.Forms.Control.Visible' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaawtComponentsetVisible_boolean'"
			//UPGRADE_TODO: 'System.Windows.Forms.Application.Run' must be called to start a main form. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1135'"
			f.Visible = true;
			
			// close RConnection, we're done
			c.close();
		}
		catch (RserveException rse)
		{
			// RserveException (transport layer - e.g. Rserve is not running)
			System.Console.Out.WriteLine(rse);
		}
		catch (REXPMismatchException mme)
		{
			// REXP mismatch exception (we got something we didn't think we get)
			System.Console.Out.WriteLine(mme);
			SupportClass.WriteStackTrace(mme, Console.Error);
		}
		catch (System.Exception e)
		{
			// something else
			System.Console.Out.WriteLine("Something went wrong, but it's not the Rserve: " + e.Message);
			SupportClass.WriteStackTrace(e, Console.Error);
		}
	}
	
	internal System.Drawing.Image img;
#if DEAD
	public PlotDemo(System.Drawing.Image img)
	{
		this.img = img;
		//UPGRADE_ISSUE: Class 'java.awt.MediaTracker' was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1000_javaawtMediaTracker'"
		//UPGRADE_ISSUE: Constructor 'java.awt.MediaTracker.MediaTracker' was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1000_javaawtMediaTracker'"
		MediaTracker mediaTracker = new MediaTracker(this);
		//UPGRADE_ISSUE: Method 'java.awt.MediaTracker.addImage' was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1000_javaawtMediaTracker'"
		mediaTracker.addImage(img, 0);
		try
		{
			//UPGRADE_ISSUE: Method 'java.awt.MediaTracker.waitForID' was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1000_javaawtMediaTracker'"
			mediaTracker.waitForID(0);
		}
		catch (System.Threading.ThreadInterruptedException ie)
		{
			System.Console.Error.WriteLine(ie);
			System.Environment.Exit(1);
		}
		//UPGRADE_TODO: Method 'java.awt.Component.setSize' was converted to 'System.Windows.Forms.Control.Size' which has a different behavior. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1073_javaawtComponentsetSize_int_int'"
		Size = new System.Drawing.Size(img.Width, img.Height);
	}
#endif
	protected override void  OnPaint(System.Windows.Forms.PaintEventArgs g_EventArg)
	{
		System.Drawing.Graphics g = null;
		if (g_EventArg != null)
			g = g_EventArg.Graphics;
		//UPGRADE_WARNING: Method 'java.awt.Graphics.drawImage' was converted to 'System.Drawing.Graphics.drawImage' which may throw an exception. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1101'"
		g.DrawImage(img, 0, 0);
	}
}
#endif
#endif