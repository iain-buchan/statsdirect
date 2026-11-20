using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace StatsDirect.UI
{
    /// <summary>
    /// What a downloaded installer must be before StatsDirect starts it: a file with a valid Authenticode signature made by
    /// StatsDirect Limited, whose version resource says it is StatsDirect and newer than the version running.  Windows makes its
    /// own checks when the file is started; this makes sure first that the file is ours and is an update.
    /// </summary>
    internal static class UpdateInstallerCheck
    {
        /// <summary>
        /// The name on the certificate that signs StatsDirect releases.  A release signed under another name would be refused by
        /// the updater of every version that has this check, so a version that accepts both names has to be released first.
        /// </summary>
        internal const string Publisher = "StatsDirect Limited";
        internal const string Product = "StatsDirect";

        private const int RevocationFailure = unchecked((int)0x800B010E), RevocationOffline = unchecked((int)0x80092013), NoRevocationCheck = unchecked((int)0x80092012);

        /// <summary>The check the updater makes: against the version of the program that is running.</summary>
        internal static void VerifyForRunningProgram(string path) =>
            Verify(path, Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

        /// <summary>Throws unless the file is a StatsDirect installer signed by StatsDirect Limited and newer than <paramref name="running"/>.</summary>
        internal static void Verify(string path, Version running)
        {
            if (Signer(path) != Publisher)
                throw new IOException("The downloaded installer is not signed by StatsDirect Limited, so it has not been opened.");
            // The version resource is inside what the signature covers, so once the signer is ours it can be believed.
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            var offered = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
            var current = new Version(Math.Max(running.Major, 0), Math.Max(running.Minor, 0), Math.Max(running.Build, 0));
            if (info.ProductName != Product || offered <= current)
                throw new IOException("The downloaded installer is not a newer version of StatsDirect, so it has not been opened.");
        }

        /// <summary>The name on the certificate that signed the file, or null when the file has no valid signature.</summary>
        internal static string Signer(string path)
        {
            // Revocation is checked for the whole chain but the root.  Where the revocation servers cannot be reached, as on
            // networks that block them, the signature is checked again without them, so that such a network can still update.
            int result = Trust(path, revocation: 1, flags: 0x80);
            if (result is RevocationFailure or RevocationOffline or NoRevocationCheck)
                result = Trust(path, revocation: 0, flags: 0x10 | 0x1000);
            if (result != 0) return null;
            try
            {
                // CertificateLoader does not expose Authenticode extraction; trust itself is checked by WinVerifyTrust above.
#pragma warning disable SYSLIB0057
                using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                return certificate.GetNameInfo(X509NameType.SimpleName, false);
            }
            catch (CryptographicException) { return null; }
        }

        // WinVerifyTrust on the file's own signature, with no window.  revocation: 1 the whole chain, 0 none.  flags: 0x80 leave
        // the root out of the revocation check; 0x10 no revocation check and 0x1000 nothing fetched from the network.
        private static int Trust(string path, uint revocation, uint flags)
        {
            var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
            IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
            try
            {
                Marshal.StructureToPtr(file, memory, false);
                var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, UnionChoice = 1, File = memory, Revocation = revocation, Flags = flags };
                Guid action = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
                return WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            }
            finally { Marshal.DestroyStructure<TrustFile>(memory); Marshal.FreeHGlobal(memory); }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TrustFile { internal uint Size; [MarshalAs(UnmanagedType.LPWStr)] internal string Path; internal IntPtr Handle, Subject; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TrustData { internal uint Size; internal IntPtr Policy, Client; internal uint Ui, Revocation, UnionChoice; internal IntPtr File; internal uint State; internal IntPtr StateData, Url; internal uint Flags, Context; internal IntPtr SignatureSettings; }
        [DllImport("wintrust.dll", ExactSpelling = true)]
        private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
    }
}
