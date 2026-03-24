using System.Collections.Generic;
using Newtonsoft.Json;

namespace DevKit.Models
{
    public class ScriptEntry
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("buttonName")] public string ButtonName { get; set; }
        [JsonProperty("className")] public string ClassName { get; set; }
        [JsonProperty("dllFileName")] public string DllFileName { get; set; }
        [JsonProperty("sourceFileName")] public string SourceFileName { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; }
        [JsonProperty("group")] public string Group { get; set; } = "Scripts";
    }

    public class ScriptsManifest
    {
        [JsonProperty("scripts")] public List<ScriptEntry> Scripts { get; set; } = new List<ScriptEntry>();
        [JsonProperty("groups")] public List<string> Groups { get; set; } = new List<string> { "Scripts" };
    }
}
