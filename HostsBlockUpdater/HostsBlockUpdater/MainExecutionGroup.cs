using Cauldron;
using Cauldron.Activator;
using Cauldron.Consoles;
using Cauldron.Core;
using Cauldron.Core.Collections;
using Cauldron.Core.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HostsBlockUpdater
{
    [ExecutionGroup("General")]
    public sealed class MainExecutionGroup : IExecutionGroup
    {
        private DynamicEqualityComparer<HostFileLine> equalityComparer;
        private string hostFileName;
        private IEnumerable<IFileImporter> importers;

        public MainExecutionGroup()
        {
            var importersPath = Path.Combine(ApplicationInfo.ApplicationPath.FullName, "Importers");
            if (!Directory.Exists(importersPath))
                Directory.CreateDirectory(importersPath);

            Assemblies.LoadAssembly(new DirectoryInfo(importersPath));

            this.importers = Factory.CreateMany<IFileImporter>();
            this.hostFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Drivers", "etc", "hosts");
            this.equalityComparer = new DynamicEqualityComparer<HostFileLine>(
                (a, b) => string.Equals(a.HostName, b.HostName, StringComparison.InvariantCultureIgnoreCase),
                x => x.HostName.GetHashCode());
        }

        [Parameter("Analyses the hosts file", "analyse", "a")]
        public bool Analyse { get; set; }

        [Parameter("Creates a backup of the hosts file. Requires the full path of the back-up\ntarget directory.$mm$ [Directory]$ux$ C:\\Temp", "backup", "B")]
        public string CreateBackup { get; set; }

        [Parameter("Hides the console", "hide", "H")]
        public bool HideConsole { get; set; }

        [Parameter("Shows all source urls.", "source", "s")]
        public bool ShowSourceUrls { get; private set; }

        [Parameter("Starts an update of the hosts file", "update", "u")]
        public bool Update { get; set; }

        public void Execute(ParameterParser parser)
        {
            try
            {
                if (this.HideConsole)
                    ConsoleUtils.HideConsole();

                if (!string.IsNullOrEmpty(this.CreateBackup) && !Utils.StartElevated(parser.Parameters))
                {
                    var hostsFile = new FileInfo(this.hostFileName);

                    if (hostsFile.Exists)
                        hostsFile.CopyTo(Path.Combine(this.CreateBackup, "hosts_backup.txt"), true);
                }

                if (this.ShowSourceUrls)
                {
                    var configs = JsonConvert.DeserializeObject<ConfigModel[]>(
                        File.ReadAllText(Path.Combine(ApplicationInfo.ApplicationPath.FullName, "config.json")));

                    Console.ForegroundColor = ConsoleColor.Cyan;

                    foreach (var item in configs)
                        Console.WriteLine(item.Url);
                }
                else if (this.Update)
                {
                    var configs = JsonConvert.DeserializeObject<ConfigModel[]>(
                        File.ReadAllText(Path.Combine(ApplicationInfo.ApplicationPath.FullName, "config.json")));

                    var hostBody = this.GetHostsFile();
                    this.AnalyseHosts(hostBody);

                    var list = new ConcurrentList<HostFileLine>();
                    Parallel.ForEach(configs, x => list.AddRange(this.StartImport(x)));
                    var result = hostBody.Concat(list).Distinct(this.equalityComparer).ToArray();
                    var whitelist = File.ReadAllLines(ApplicationInfo.ApplicationPath.GetFiles("whitelist.txt", SearchOption.AllDirectories).FirstOrDefault().FullName);

                    // Make sure that localhost has 127.0.0.1 assigned
                    this.EnsureHostHasIp(result, "localhost", "127.0.0.1");
                    this.EnsureHostHasIp(result, "localhost.localdomain", "127.0.0.1");
                    this.EnsureHostHasIp(result, "local", "127.0.0.1");
                    this.EnsureHostHasIp(result, "broadcasthost", "255.255.255.255");

                    if (whitelist.Length > 0)
                        result = result.Where(x => !whitelist.Any(y => Regex.Match(x.HostName, y, RegexOptions.IgnoreCase).Length > 0)).ToArray();

                    var blacklist = File.ReadAllLines(ApplicationInfo.ApplicationPath.GetFiles("blacklist.txt", SearchOption.AllDirectories).FirstOrDefault().FullName);

                    result = result
                            .Concat(blacklist.Select(x => new HostFileLine() { IP = "0.0.0.0", HostName = x }))
                            .Where(x => !x.IsComment && !string.IsNullOrEmpty(x.HostName))
                            .OrderByDescending(x => x.IP)
                            .ThenBy(x => x.HostName.Length)
                            .ThenBy(x => x.HostName).ToArray();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"The new hosts file will have {result.Count()} lines");

                    if (!Utils.StartElevated(parser.Parameters))
                        File.WriteAllLines(this.hostFileName, result.Select(x => x.ToString()));
                }
                else if (this.Analyse)
                    this.AnalyseHosts(this.GetHostsFile());
            }
            catch
            {
                throw;
            }
            finally
            {
                if (this.HideConsole)
                    ConsoleUtils.ShowConsole();
            }
        }

        private void AnalyseHosts(IEnumerable<HostFileLine> hosts)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Analysing Hosts file");

            foreach (var line in hosts)
            {
                Console.ForegroundColor = ConsoleColor.Red;

                if (!string.IsNullOrEmpty(line.HostName) && line.HostName != "localhost" && !line.IsComment && line.IP != "0.0.0.0")
                    Console.WriteLine($"The hosts '{line.HostName}' is not redirected to 0.0.0.0 but instead to {line.IP}");
                else if (!string.IsNullOrEmpty(line.HostName) && line.IsComment && !string.IsNullOrEmpty(line.IP))
                    Console.WriteLine($"The hosts '{line.HostName}' is commented out.");
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"The hosts file has {hosts.Count()} lines");
            Console.WriteLine("Analysing Hosts file finished");
            Console.ResetColor();
        }

        private void EnsureHostHasIp(IEnumerable<HostFileLine> content, string host, string ip)
        {
            var localHost = content.FirstOrDefault(x => !x.IsComment && string.Equals(x.HostName, host, StringComparison.InvariantCultureIgnoreCase));

            if (localHost != null)
                localHost.IP = ip;
        }

        private IEnumerable<HostFileLine> GetHostsFile() =>
                    File.ReadAllLines(this.hostFileName).Select(x => new HostFileLine(x)).ToArray();

        private IEnumerable<HostFileLine> StartImport(ConfigModel config)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Importing from: " + config.Url);

            var converter = this.importers.FirstOrDefault(x => x.GetType().Name.EndsWith(config.ImporterName));

            if (converter == null)
                throw new Exception("Unable to find importer: " + config.ImporterName);

            var imported = converter.Import(new WebClient().DownloadString(config.Url));
            return imported
                .Select(x => new HostFileLine() { IP = "0.0.0.0", HostName = x })
                .Where(x => x.HostName.IndexOf('.') > 0)
                .ToArray();
        }
    }
}