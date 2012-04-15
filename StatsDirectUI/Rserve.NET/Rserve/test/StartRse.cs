#if USE_R
using System;
using RConnection = org.rosuda.REngine.Rserve.RConnection;

namespace org.rosuda.REngine
{
    /// <summary>helper class that consumes output of a process. In addition, it filter output of the REG command on Windows to look for InstallPath registry entry which specifies the location of R. </summary>
    class StreamHog : SupportClass.ThreadClass
    {
        virtual public System.String InstallPath
        {
            get
            {
                return installPath;
            }

        }
        internal System.IO.Stream is_Renamed;
        internal bool capture;
        internal System.String installPath;
        internal StreamHog(System.IO.Stream is_Renamed, bool capture)
        {
            this.is_Renamed = is_Renamed;
            this.capture = capture;
            Start();
        }
        override public void Run()
        {
            try
            {
                //UPGRADE_WARNING: At least one expression was used more than once in the target code. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1181'"
                System.IO.StreamReader br = new System.IO.StreamReader(is_Renamed, System.Text.Encoding.Default);
                System.String line = null;
                while ((line = br.ReadLine()) != null)
                {
                    if (capture)
                    {
                        // we are supposed to capture the output from REG command
                        int i = line.IndexOf("InstallPath");
                        if (i >= 0)
                        {
                            System.String s = line.Substring(i + 11).Trim();
                            int j = s.IndexOf("REG_SZ");
                            if (j >= 0)
                                s = s.Substring(j + 6).Trim();
                            installPath = s;
                            System.Console.Out.WriteLine("R InstallPath = " + s);
                        }
                    }
                    else
                        System.Console.Out.WriteLine("Rserve>" + line);
                }
            }
            catch (System.IO.IOException e)
            {
                SupportClass.WriteStackTrace(e, Console.Error);
            }
        }
    }

    /// <summary>simple class that start Rserve locally if it's not running already - see mainly <code>checkLocalRserve</code> method. It spits out quite some debugging outout of the console, so feel free to modify it for your application if desired.<p>
    /// <i>Important:</i> All applications should shutdown every Rserve that they started! Never leave Rserve running if you started it after your application quits since it may pose a security risk. Inform the user if you started an Rserve instance.
    /// </summary>
    public class StartRserve
    {
        /// <summary>check whether Rserve is currently running (on local machine and default port).</summary>
        /// <returns> <code>true</code> if local Rserve instance is running, <code>false</code> otherwise
        /// </returns>
        public static bool RserveRunning
        {
            get
            {
                try
                {
                    RConnection c = new RConnection();
                    System.Console.Out.WriteLine("Rserve is running.");
                    c.close();
                    return true;
                }
                catch (System.Exception e)
                {
                    System.Console.Out.WriteLine("First connect try failed with: " + e.Message);
                }
                return false;
            }

        }
        /// <summary>shortcut to <code>launchRserve(cmd, "--no-save --slave", "--no-save --slave", false)</code> </summary>
        public static bool launchRserve(System.String cmd)
        {
            return launchRserve(cmd, "--no-save --slave", "--no-save --slave", false);
        }

        /// <summary>attempt to start Rserve. Note: parameters are <b>not</b> quoted, so avoid using any quotes in arguments</summary>
        /// <param name="cmd">command necessary to start R
        /// </param>
        /// <param name="rargs">arguments are are to be passed to R
        /// </param>
        /// <param name="rsrvargs">arguments to be passed to Rserve
        /// </param>
        /// <returns> <code>true</code> if Rserve is running or was successfully started, <code>false</code> otherwise.
        /// </returns>
        public static bool launchRserve(System.String cmd, System.String rargs, System.String rsrvargs, bool debug)
        {
            try
            {
                System.Diagnostics.Process p;
                // bool isWindows = false;
                System.String osname = System.Environment.GetEnvironmentVariable("OS");
                if (osname != null && osname.Length >= 7 && osname.Substring(0, (7) - (0)).Equals("Windows"))
                {
                    // isWindows = true; /* Windows startup */
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                    startInfo.FileName = cmd;
                    startInfo.Arguments = "-e \"library(Rserve);Rserve(" + (debug ? "TRUE" : "FALSE") + ",args='" + rsrvargs + "')\" " + rargs;
                    p = System.Diagnostics.Process.Start(startInfo);
                }
                /* unix startup */
                else
                {
                    p = SupportClass.ExecSupport(new System.String[] { "/bin/sh", "-c", "echo 'library(Rserve);Rserve(" + (debug ? "TRUE" : "FALSE") + ",args=\"" + rsrvargs + "\")'|" + cmd + " " + rargs });
                    // we need to fetch the output - some platforms will die if you don't ...
                    StreamHog errorHog = new StreamHog(p.StandardError.BaseStream, false);
                    StreamHog outputHog = new StreamHog(p.StandardInput.BaseStream, false);
                    p.WaitForExit();
                    System.Int32 generatedAux3 = p.ExitCode;
                }
            }
            catch (System.Exception x)
            {
                System.Diagnostics.Debug.WriteLine("failed to start Rserve process with " + x.Message);
                return false;
            }
            int attempts = 5; /* try up to 5 times before giving up. We can be conservative here, because at this point the process execution itself was successful and the start up is usually asynchronous */
            while (attempts > 0)
            {
                try
                {
                    RConnection c = new RConnection();
                    c.close();
                    return true;
                }
                catch (System.Exception e2)
                {
                    System.Console.Out.WriteLine("Try failed with: " + e2.Message);
                }
                /* a safety sleep just in case the start up is delayed or asynchronous */
                try
                {
                    System.Threading.Thread.Sleep(5000);
                }
                catch (System.Threading.ThreadInterruptedException)
                {
                }
                attempts--;
            }
            return false;
        }

        /// <summary>checks whether Rserve is running and if that's not the case it attempts to start it using the defaults for the platform where it is run on. This method is meant to be set-and-forget and cover most default setups. For special setups you may get more control over R with <<code>launchRserve</code> instead. </summary>
        public static bool checkLocalRserve()
        {
            if (RserveRunning)
                return true;
            System.String osname = System.Environment.GetEnvironmentVariable("OS");
            if (osname != null && osname.Length >= 7 && osname.Substring(0, (7) - (0)).Equals("Windows"))
            {
                System.Diagnostics.Debug.WriteLine("Windows: query registry to find where R is installed ...");
                System.String installPath = null;
                try
                {
                    Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                    Microsoft.Win32.RegistryKey rKey = hklm.OpenSubKey(@"Software\R-core\R");
                    object installPathObject = rKey.GetValue("InstallPath");
                    if (null != installPathObject)
                        installPath = (string)installPathObject;
                    rKey.Close();
                    hklm.Close();
                }
                catch (System.Exception rge)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: unable to find the location of R: " + rge);
                    return false;
                }
                if (installPath == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: canot find path to R. Make sure reg is available and R was installed with registry settings.");
                    return false;
                }
                return launchRserve(installPath + "\\bin\\R.exe");
            }
            bool tmpBool;
            if (System.IO.File.Exists((new System.IO.FileInfo("/Library/Frameworks/R.framework/Resources/bin/R")).FullName))
                tmpBool = true;
            else
                tmpBool = System.IO.Directory.Exists((new System.IO.FileInfo("/Library/Frameworks/R.framework/Resources/bin/R")).FullName);
            bool tmpBool2;
            if (System.IO.File.Exists((new System.IO.FileInfo("/usr/local/lib/R/bin/R")).FullName))
                tmpBool2 = true;
            else
                tmpBool2 = System.IO.Directory.Exists((new System.IO.FileInfo("/usr/local/lib/R/bin/R")).FullName);
            bool tmpBool3;
            if (System.IO.File.Exists((new System.IO.FileInfo("/usr/lib/R/bin/R")).FullName))
                tmpBool3 = true;
            else
                tmpBool3 = System.IO.Directory.Exists((new System.IO.FileInfo("/usr/lib/R/bin/R")).FullName);
            bool tmpBool4;
            if (System.IO.File.Exists((new System.IO.FileInfo("/usr/local/bin/R")).FullName))
                tmpBool4 = true;
            else
                tmpBool4 = System.IO.Directory.Exists((new System.IO.FileInfo("/usr/local/bin/R")).FullName);
            bool tmpBool5;
            if (System.IO.File.Exists((new System.IO.FileInfo("/sw/bin/R")).FullName))
                tmpBool5 = true;
            else
                tmpBool5 = System.IO.Directory.Exists((new System.IO.FileInfo("/sw/bin/R")).FullName);
            bool tmpBool6;
            if (System.IO.File.Exists((new System.IO.FileInfo("/usr/common/bin/R")).FullName))
                tmpBool6 = true;
            else
                tmpBool6 = System.IO.Directory.Exists((new System.IO.FileInfo("/usr/common/bin/R")).FullName);
            bool tmpBool7;
            if (System.IO.File.Exists((new System.IO.FileInfo("/opt/bin/R")).FullName))
                tmpBool7 = true;
            else
                tmpBool7 = System.IO.Directory.Exists((new System.IO.FileInfo("/opt/bin/R")).FullName);
            return (launchRserve("R") || (tmpBool && launchRserve("/Library/Frameworks/R.framework/Resources/bin/R")) || (tmpBool2 && launchRserve("/usr/local/lib/R/bin/R")) || (tmpBool3 && launchRserve("/usr/lib/R/bin/R")) || (tmpBool4 && launchRserve("/usr/local/bin/R")) || (tmpBool5 && launchRserve("/sw/bin/R")) || (tmpBool6 && launchRserve("/usr/common/bin/R")) || (tmpBool7 && launchRserve("/opt/bin/R")));
        }
    }
}
#endif