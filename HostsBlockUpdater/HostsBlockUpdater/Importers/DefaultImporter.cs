using Cauldron.Core.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace HostsBlockUpdater.Importers
{
    public class DefaultImporter : IFileImporter
    {
        public IEnumerable<string> Import(string fileBody) => fileBody
            .GetLines()
            .Where(x => !x.StartsWith("#"))
            .Select(x =>
            {
                var splitted = x.Split(' ');

                if (splitted.Length > 1)
                    return splitted[1];

                return null;
            })
            .Where(x => x != null)
            .ToArray();
    }
}