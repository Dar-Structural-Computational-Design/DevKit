using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace DevKit.Models
{
    public class UserSettings
    {
        [JsonProperty("claudeApiKey")] public string ClaudeApiKey { get; set; } = "";
        [JsonProperty("claudeModel")] public string ClaudeModel { get; set; } = "claude-sonnet-4-20250514";
        private const string FILE = "devkit_settings.json";

        public static UserSettings Load(string folder)
        {
            string p = Path.Combine(folder, FILE);
            if (!File.Exists(p)) return new UserSettings();
            try { return JsonConvert.DeserializeObject<UserSettings>(File.ReadAllText(p, Encoding.UTF8)) ?? new UserSettings(); }
            catch { return new UserSettings(); }
        }

        public void Save(string folder)
        {
            File.WriteAllText(Path.Combine(folder, FILE), JsonConvert.SerializeObject(this, Formatting.Indented), Encoding.UTF8);
        }
    }
}
