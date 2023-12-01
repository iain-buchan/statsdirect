using StatsDirect.Configuration;
using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    static class LastChanceCatcher
    {
        public static void CatchMostErrors()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.ThreadException += Application_ThreadException;
        }

        private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = (Exception)e.ExceptionObject;

                CopeWithUnhandledException(ex, false);
            }
            finally
            {
                // Politely ask everything to quit
                Application.Exit();

                // If that returns, force the issue!
                Environment.Exit(-1);
            }
        }

        private static void Application_ThreadException(object? sender, System.Threading.ThreadExceptionEventArgs e)
        {
            try
            {
                CopeWithUnhandledException(e.Exception, false);
            }
            finally
            {
                // Politely ask everything to quit
                Application.Exit();

                // If that returns, force the issue!
                Environment.Exit(-1);
            }
        }

        /// <summary>
        /// Last-ditch attempt to cope with an otherwise-unhandled exception and gain as much information as we can.
        /// </summary>
        /// <param name="ex">The unhandled exception</param>
        /// <param name="canTryToContinue">true if the application might be able to continue (typically because the exception was on a background thread). false if the application cannot continue.</param>
        /// <returns>DialogResult.Abort if the application should close, otherwise something else.</returns>
        private static DialogResult CopeWithUnhandledException(Exception ex, bool canTryToContinue)
        {
            WriteToBlackbox("Uncaught exception", ex);
            string message = ex.Message + Environment.NewLine + ex.StackTrace;
            using frmErrorMessage e = new(message);
            e.ShowDialog();
            return DialogResult.Abort;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        /// 
        public static void WriteToBlackbox(string message, Exception ex)
        {
            try
            {
                using Stream boxStream = File.OpenWrite(Path.Combine(SDConfiguration.MyStatsDirectFolder, "Blackbox.txt"));
                using TextWriter boxWriter = new StreamWriter(boxStream, Encoding.UTF8);
                boxWriter.WriteLine("StatsDirect exception log generated at {0} local time ({1} UTC)", DateTime.Now, DateTime.UtcNow);
                boxWriter.WriteLine();
                boxWriter.WriteLine("This file contains a trace of what StatsDirect was doing when your error occurred. If we've asked you for it, please attach the file or, if you prefer, paste the contents into an email to us.");
                boxWriter.WriteLine();
                if (null != message)
                {
                    boxWriter.WriteLine("Message generated from StatsDirect: {0}", message);
                    boxWriter.WriteLine();
                }
                WriteExceptionToBlackbox(boxWriter, ex, false);
            }
            catch (Exception)
            {
                // If our black box can't operate, we're hosed.  Ignore this error!
            }
        }

        private static void WriteExceptionToBlackbox(TextWriter boxWriter, Exception ex, bool isInnerException)
        {
            if (null == ex)
                return;

            if (isInnerException)
                boxWriter.WriteLine("Inner exception:");
            boxWriter.WriteLine(ex.GetType().FullName);
            boxWriter.WriteLine(ex.Message);
            boxWriter.WriteLine(ex.Source);
            boxWriter.WriteLine(ex.StackTrace);
            if (null != ex.Data)
                foreach (DictionaryEntry de in ex.Data)
                    boxWriter.WriteLine("{0} = {1}", de.Key, de.Value);

            if (null != ex.InnerException)
                WriteExceptionToBlackbox(boxWriter, ex.InnerException, true);
            boxWriter.WriteLine();
        }
    }
}