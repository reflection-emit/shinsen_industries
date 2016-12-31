using System.Collections.Generic;

namespace HostsBlockUpdater
{
    public interface IFileImporter
    {
        IEnumerable<string> Import(string fileBody);
    }
}