using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace HostsBlockUpdater
{
    /// <summary>
    /// Implements the <see cref="HostsUpdater" /> class
    /// </summary>
    public sealed class HostsUpdater
    {
        /// <summary>
        /// Default uri for of the hosts file used for the update
        /// </summary>
        private const string DefaultHostsFileUri = "http://winhelp2002.mvps.org/hosts.txt";

        private string hostFileName;

        private HostsUpdater(string[] args)
        {
            this.hostFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Drivers", "etc", "hosts");

            if (args == null || args.Length == 0)
                this.Merge(HostsUpdater.DefaultHostsFileUri);
            else
                this.Merge(args[0]);
        }

        /// <summary>
        /// The Main method is the entry point
        /// </summary>
        /// <param name="args">Command parameters</param>
        public static void Main(string[] args)
        {
            new HostsUpdater(args);
        }

        private string[] DownloadHostsFile(string path)
        {
            var isValidPath = path.IsLocalPath();
            string[] body;

            if (!isValidPath.HasValue)
            {
                return null;
            }

            if (isValidPath.Value)
            {
                body = File.ReadAllLines(path);
            }
            else
            {
                body = (new WebClient()).DownloadString(path).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            }

            // clean it
            var result = new List<string>();

            foreach (var line in body)
            {
                if (line.TrimStart().StartsWith("#") || string.IsNullOrEmpty(line.Trim()))
                    continue;

                var redirect = line.Split(' ');

                if (redirect.Length == 0)
                    continue;

                result.Add(redirect[1].Trim());
            }

            return result.ToArray();
        }

        private string[] GetHostsBody()
        {
            try
            {
                var fileBody = File.ReadAllLines(this.hostFileName);
                var outputFile = new List<string>();

                foreach (var line in fileBody)
                {
                    if (line.TrimStart().StartsWith("#") || string.IsNullOrEmpty(line.Trim()))
                    {
                        continue;
                    }

                    var redirect = line.Split(' ');

                    if (redirect.Length == 0)
                    {
                        continue;
                    }

                    outputFile.Add(redirect[1].Trim());
                }

                return outputFile.ToArray();
            }
            catch (Exception e)
            {
                this.Log(e);
                return null;
            }
        }

        private void Log(Exception e)
        {
            File.WriteAllText(Path.GetRandomFileName() + ".txt", e.Message + "/n/n" + e.StackTrace);
            Environment.Exit(1);
        }

        private void Merge(string args)
        {
            var source = this.DownloadHostsFile(args);
            var target = this.GetHostsBody();

            var newLines = new List<string>();

            foreach (var line in source)
            {
                var entry = line.ToLower();

                if (!target.Contains(entry) && entry != "localhost")
                {
                    newLines.Add("0.0.0.0 " + entry);
                }
            }

            try
            {
                File.AppendAllLines(this.hostFileName, newLines);
            }
            catch (Exception e)
            {
                this.Log(e);
            }
        }
    }
}
