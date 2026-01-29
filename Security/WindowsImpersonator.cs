using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace SQLScripter.Security
{
    [SupportedOSPlatform("windows")]
    public class WindowsImpersonator
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword,
            int dwLogonType, int dwLogonProvider, out IntPtr phToken);

        private const int LOGON32_PROVIDER_DEFAULT = 0;
        private const int LOGON32_LOGON_INTERACTIVE = 2;

        public static SafeAccessTokenHandle? Logon(string username, string password)
        {
            try
            {
                string domain = ".";
                string user = username;

                if (username.Contains("\\"))
                {
                    string[] parts = username.Split('\\');
                    domain = parts[0];
                    user = parts[1];
                }

                bool loggedOn = LogonUser(user, domain, password, LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out IntPtr tokenHandle);

                if (loggedOn)
                {
                    return new SafeAccessTokenHandle(tokenHandle);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
