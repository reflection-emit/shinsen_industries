using Newtonsoft.Json;

namespace HostsBlockUpdater
{
    public sealed class ConfigModel
    {
        [JsonProperty("file-importer")]
        public string ImporterName { get; private set; }

        [JsonProperty("update-url")]
        public string Url { get; private set; }
    }
}