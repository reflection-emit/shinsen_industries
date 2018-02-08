using Cauldron;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Principal;

namespace HostsBlockUpdater
{
    internal static class Utils
    {
        /// <summary>
        /// Tests whether the current user is an elevated administrator.
        /// </summary>
        public static bool IsCurrentUserAnAdministrator
        {
            get
            {
                try
                {
                    using (var user = WindowsIdentity.GetCurrent())
                        return new WindowsPrincipal(user).IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Starts the EntryAssembly elevated.
        /// </summary>
        /// <param name="args">The arguments to be passed to the application</param>
        /// <returns>Returns true if successful; otherwise false</returns>
        public static bool StartElevated(params string[] args)
        {
            if (!IsCurrentUserAnAdministrator)
            {
                var startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = Assembly.GetEntryAssembly().Location,
                    Arguments = args != null && args.Length > 0 ? args.Join(" ") : string.Empty,
                    Verb = "runas"
                };

                Process.Start(startInfo);
                return true;
            }
            else
                return false;
        }
    }
}