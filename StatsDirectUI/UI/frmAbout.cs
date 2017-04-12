using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Globalization;

namespace StatsDirect.UI
{
    public partial class frmAbout : Form
    {
        private OperatingSystem osInfo;
        private bool m95;
        private bool m98;
        private bool mMe;
        private bool mNt;
        private bool m_2K;
        private bool mXP;
        private bool mVista;
        private bool m7;
        private bool m8;
        private bool m81;
        private bool m10;

        public frmAbout()
        {
            InitializeComponent();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void lblVisit_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start("http://www.statsdirect.com/update.aspx");
            }
            catch (Exception ex)
            {
                // Almost-silent fail
                Debug.Print("Couldn't launch process to navigate to web site: " + ex.Message);
            }
        }

        private void frmAbout_Shown(object sender, EventArgs e)
        {
            RefreshVersion();
            RefreshSysInfo();
            RefreshUserInfo();
        }

        private void RefreshSysInfo()
        {
            osInfo = Environment.OSVersion;
            switch (osInfo.Platform)
            {
                case PlatformID.Win32Windows:
                    // No check on major version!
                    // Flags are additive.  Test for highest need, or lowest acceptable.
                    m95 = true;
                    m98 = osInfo.Version.Minor >= 10;
                    mMe = (osInfo.Version.Build & 0xffff) >= 3000;
                    break;
                case PlatformID.Win32NT:
                    mNt = true;
                    m_2K = osInfo.Version.Major >= 5;
                    mXP = osInfo.Version.Major >= 5 && osInfo.Version.Minor >= 1;
                    mVista = osInfo.Version.Major >= 6;
                    m7 = osInfo.Version.Major >= 6 && osInfo.Version.Minor >= 1;
                    m8 = osInfo.Version.Major >= 6 && osInfo.Version.Minor >= 2;
                    m81 = osInfo.Version.Major >= 6 && osInfo.Version.Minor >= 3;
                    m10 = osInfo.Version.Major >= 10;
                    break;
            }

            string clrVersion = RuntimeEnvironment.GetSystemVersion();
            string bitness = ", unknown bitness";
            switch (IntPtr.Size)
            {
                case 4:
                    bitness = "32-bit";
                    break;
                case 8:
                    bitness = "64-bit";
                    break;
                // Default: Do nothing
            }
            string version = Platform + " v" + osInfo.Version.Major + "." + osInfo.Version.Minor + "." + osInfo.Version.Build + " " + osInfo.ServicePack + ", CLR " + clrVersion + " " + bitness + (IsNgen() ? ", native image" : ", JIT-compiled");
            lblSysInfo.Text = version;
        }

        private void RefreshVersion()
        {
            System.Reflection.Assembly mainAssembly = GetType().Assembly;
            string mainFileName = mainAssembly.Location;
            DateTime touchTime = System.IO.File.GetLastWriteTime(mainFileName);
            string version = mainAssembly.FullName;
            if (version.Contains("Version="))
            {
                int versionPos = version.IndexOf("Version=", StringComparison.Ordinal);
                version = version.Substring(versionPos + 8);
                int spacePos = version.IndexOf(',');
                version = version.Substring(0, spacePos);
                // It's a 4-number version. Chop the fourth number off as we don't use it.
                int lastDot = version.LastIndexOf('.');
                if (lastDot >= 0)
                    version = version.Substring(0, lastDot);
            }
            version += " (" + touchTime.ToString("d") + ")";
            lblVersion.Text = "Version: " + version;
        }

        private void RefreshUserInfo()
        {
            UserInfo ui = GetBestUserInfo();

            int d = !DateTime.MinValue.Equals(ui.Expires) ? Math.Abs(DateTime.ParseExact(ui.Expires, "dd/MM/yyyy", CultureInfo.InvariantCulture).Subtract(DateTime.Now).Days) : 0;
            if (ui.Trial || d < 60)
            {
                lblEmail.Text = ui.Name + "\r\n" + "YOUR LICENCE EXPIRES IN " + d + " DAYS.\r\nTO ORDER, CLICK ON THE ABOVE LINK,\r\nTHEN DOUBLE CLICK HERE TO ENTER A NEW LICENCE KEY.";
                lblEmail.Cursor = Cursors.Hand;
                lblEmail.DoubleClick += lblEmail_DoubleClick;
            }
            else
            {
                lblEmail.Text = ui.Name + "\r\n" + ui.Company;
            }
            lblExpiry.Text = "The licence for this installation expires on " + ui.Expires;
        }

        void lblEmail_DoubleClick(object sender, EventArgs e)
        {
            UserInfo ui = GetBestUserInfo();

            using (frmLicense f = new frmLicense(ui, false))
            {
                f.ShowDialog(this);
            }
            RefreshUserInfo();
        }

        private static UserInfo GetBestUserInfo()
        {
            UserInfo ui = License.GetUserInfo(true);
            bool scrap;
            bool machineOk = License.Check(ui, out scrap);
            if (machineOk)
                return ui;
            return License.GetUserInfo(false);
        }

        public string Platform
        {
            get
            {
                if (mMe)
                    return "Windows Me";
                if (m98)
                    return "Windows 98";
                if (m95)
                    return "Windows 95";
                if (m10)
                    return "Windows 10";
                if (m81)
                    return "Windows 8.1";
                if (m8)
                    return "Windows 8";
                if (m7)
                    return "Windows 7";
                if (mVista)
                    return "Windows Vista";
                if (mXP)
                    return "Windows XP";
                if (m_2K)
                    return "Windows 2000";
                if (mNt)
                    return "Windows NT";
                return "Unknown platform";
            }
        }

        private static bool IsNgen()
        {
            Process process = Process.GetCurrentProcess();
            ProcessModuleCollection modules = process.Modules;
            foreach (ProcessModule m in modules)
            {
                if (m.FileName.Contains("\\" + process.ProcessName + ".ni"))
                    return true;
            }
            return false;
        }
    }
}
