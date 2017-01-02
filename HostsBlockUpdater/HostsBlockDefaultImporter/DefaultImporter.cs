using Cauldron.Activator;
using Cauldron.Core.Extensions;
using HostsBlockUpdater;
using System.Collections.Generic;
using System.Linq;

namespace HostsBlockDefaultImporter
{
    [Component(typeof(IFileImporter))]
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