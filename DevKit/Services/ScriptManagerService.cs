using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using DevKit.Models;

namespace DevKit.Services
{
    public class ScriptManagerService
    {
        private const string MANIFEST_FILE = "scripts.json";
        private readonly string _scriptsFolderPath;

        public ScriptManagerService(string scriptsFolderPath)
        {
            _scriptsFolderPath = scriptsFolderPath;
            if (!Directory.Exists(_scriptsFolderPath)) Directory.CreateDirectory(_scriptsFolderPath);
        }

        public ScriptsManifest LoadFullManifest()
        {
            string path = Path.Combine(_scriptsFolderPath, MANIFEST_FILE);
            if (!File.Exists(path)) return new ScriptsManifest();
            try
            {
                var m = JsonConvert.DeserializeObject<ScriptsManifest>(File.ReadAllText(path, Encoding.UTF8)) ?? new ScriptsManifest();
                if (m.Groups == null || m.Groups.Count == 0) m.Groups = new List<string> { "Scripts" };
                foreach (var s in m.Scripts) if (string.IsNullOrEmpty(s.Group)) s.Group = "Scripts";
                m.Groups = m.Groups.Distinct().ToList();
                return m;
            }
            catch { return new ScriptsManifest(); }
        }

        public List<ScriptEntry> LoadManifest() => LoadFullManifest().Scripts;
        public List<string> LoadGroups() => LoadFullManifest().Groups;

        public void SaveFullManifest(ScriptsManifest manifest)
        {
            File.WriteAllText(Path.Combine(_scriptsFolderPath, MANIFEST_FILE),
                JsonConvert.SerializeObject(manifest, Formatting.Indented), Encoding.UTF8);
        }

        public ScriptEntry AddScript(string buttonName, string className, string dllFileName, string sourceCode, string group = "Scripts")
        {
            string id = "script_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            string sourceFileName = Path.GetFileNameWithoutExtension(dllFileName) + ".cs";
            File.WriteAllText(Path.Combine(_scriptsFolderPath, sourceFileName), sourceCode, Encoding.UTF8);

            var entry = new ScriptEntry { Id = id, ButtonName = buttonName, ClassName = className, DllFileName = dllFileName, SourceFileName = sourceFileName, CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Group = group };
            var manifest = LoadFullManifest();
            manifest.Scripts.Add(entry);
            if (!manifest.Groups.Any(g => g.Equals(group, StringComparison.OrdinalIgnoreCase))) manifest.Groups.Add(group);
            SaveFullManifest(manifest);
            return entry;
        }

        public bool RemoveScript(string scriptId)
        {
            var manifest = LoadFullManifest();
            var entry = manifest.Scripts.FirstOrDefault(e => e.Id == scriptId);
            if (entry == null) return false;
            TryDelete(Path.Combine(_scriptsFolderPath, entry.DllFileName));
            TryDelete(Path.Combine(_scriptsFolderPath, entry.SourceFileName));
            manifest.Scripts.Remove(entry);
            SaveFullManifest(manifest);
            return true;
        }

        public void MoveScript(string scriptId, string newGroup)
        {
            var manifest = LoadFullManifest();
            var entry = manifest.Scripts.FirstOrDefault(e => e.Id == scriptId);
            if (entry == null) return;
            entry.Group = newGroup;
            if (!manifest.Groups.Contains(newGroup)) manifest.Groups.Add(newGroup);
            SaveFullManifest(manifest);
        }

        public void AddGroup(string groupName)
        {
            var manifest = LoadFullManifest();
            if (!manifest.Groups.Any(g => g.Equals(groupName, StringComparison.OrdinalIgnoreCase)))
            { manifest.Groups.Add(groupName); SaveFullManifest(manifest); }
        }

        public void RemoveGroup(string groupName)
        {
            if (groupName == "Scripts") return;
            var manifest = LoadFullManifest();
            foreach (var s in manifest.Scripts.Where(s => s.Group == groupName)) s.Group = "Scripts";
            manifest.Groups.Remove(groupName);
            SaveFullManifest(manifest);
        }

        public string GetScriptSource(string scriptId)
        {
            var entry = LoadFullManifest().Scripts.FirstOrDefault(e => e.Id == scriptId);
            if (entry == null) return null;
            string path = Path.Combine(_scriptsFolderPath, entry.SourceFileName);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        public string ScriptsFolderPath => _scriptsFolderPath;
        private void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    }
}
