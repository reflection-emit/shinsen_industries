using System;

namespace HostsBlockUpdater
{
    internal static class Extensions
    {
        /// <summary>
        /// Returns true if the path is a local path. False if its an url.
        /// Returns null if the path is not valid.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static bool? IsLocalPath(this string target)
        {
            if (target.StartsWith("http:\\"))
            {
                return false;
            }

            try
            {
                return new Uri(target).IsFile;
            }
            catch
            {
                return null;
            }
        }
    }
}
