using Cauldron.Consoles;
using Cauldron.Core;
using Cauldron.Core.Collections;
using Cauldron.Core.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace HostsBlockUpdater
{
    [ExecutionGroup("General")]
    public sealed class MainExecutionGroup : IExecutionGroup
    {
        private DynamicEqualityComparer<HostFileLine> equalityComparer;
        private string hostFileName;

        public MainExecutionGroup()
        {
            this.hostFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Drivers", "etc", "hosts");
            this.equalityComparer = new DynamicEqualityComparer<HostFileLine>((a, b) =>
                string.Equals(a.IP, b.IP, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(a.HostName, b.HostName, StringComparison.InvariantCultureIgnoreCase));
        }

        [Parameter("Analyses the hosts file", "analyse", "a")]
        public bool Analyse { get; set; }

        [Parameter("Hides the console", "hide", "H")]
        public bool HideConsole { get; set; }

        [Parameter("Shows all source urls.", "source", "s")]
        public bool ShowSourceUrls { get; private set; }

        [Parameter("Starts an update of the hosts file", "update", "u")]
        public bool Update { get; set; }

        public void Execute(ParameterParser parser)
        {
            if (this.HideConsole)
                ConsoleUtils.HideConsole();

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

                var list = new ConcurrentList<HostFileLine>();
                Parallel.ForEach(configs, x => list.AddRange(this.StartImport(x)));
                var result = list.Distinct(this.equalityComparer).Select(x => x.ToString());

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"The new hosts file will have {result.Count()} lines");

                if (!Win32Api.StartElevated(parser.Parameters))
                    File.WriteAllLines(this.hostFileName, result);
            }
            else if (this.Analyse)
                this.AnalyseHosts(this.GetHostsFile());

            if (this.HideConsole)
                ConsoleUtils.ShowConsole();
        }

        private void AnalyseHosts(IEnumerable<HostFileLine> hosts)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Analysing Hosts file");

            foreach (var line in hosts)
            {
                Console.ForegroundColor = ConsoleColor.Red;

                if (!string.IsNullOrEmpty(line.HostName) && !line.IsComment && line.IP != "0.0.0.0")
                    Console.WriteLine($"The hosts '{line.HostName}' is not redirected to 0.0.0.0 but instead to {line.IP}");
                else if (!string.IsNullOrEmpty(line.HostName) && line.IsComment && !string.IsNullOrEmpty(line.IP))
                    Console.WriteLine($"The hosts '{line.HostName}' is commented out.");
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"The hosts file has {hosts.Count()} lines");
            Console.WriteLine("Analysing Hosts file finished");
            Console.ResetColor();
        }

        private IEnumerable<HostFileLine> GetHostsFile() =>
                    File.ReadAllLines(this.hostFileName).Select(x => new HostFileLine(x)).ToArray();

        private IEnumerable<HostFileLine> StartImport(ConfigModel config)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Importing from: " + config.Url);

            var converterType = Assemblies.Classes.FirstOrDefault(x => x.Name.EndsWith(config.ImporterName));

            if (converterType == null)
                throw new Exception("Unable to find importer: " + config.ImporterName);

            var hostBody = this.GetHostsFile();
            var converter = converterType.AsType().CreateInstance().As<IFileImporter>();

            this.AnalyseHosts(hostBody);

            var imported = converter.Import(new WebClient().DownloadString(config.Url));

            return hostBody
                 .Concat(imported.Select(x => new HostFileLine("0.0.0.0 " + x)))
                 .Distinct(this.equalityComparer);
        }
    }
}