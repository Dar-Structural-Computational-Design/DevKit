using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using DevKit.Models;

namespace DevKit.Services
{
    public class ToolPackageService
    {
        private readonly ScriptManagerService _scriptManager;
        private readonly string _scriptsFolderPath;

        public ToolPackageService(ScriptManagerService scriptManager, string scriptsFolderPath)
        {
            _scriptManager = scriptManager;
            _scriptsFolderPath = scriptsFolderPath;
        }

        public string ExportScripts(List<ScriptEntry> scripts, string outputPath)
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            using (var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create))
            {
                foreach (var s in scripts)
                {
                    string dll = Path.Combine(_scriptsFolderPath, s.DllFileName);
                    if (File.Exists(dll)) zip.CreateEntryFromFile(dll, s.DllFileName);
                    string src = Path.Combine(_scriptsFolderPath, s.SourceFileName);
                    if (File.Exists(src)) zip.CreateEntryFromFile(src, s.SourceFileName);
                }
                var me = zip.CreateEntry("package_manifest.json");
                using (var w = new StreamWriter(me.Open(), Encoding.UTF8))
                    w.Write(JsonConvert.SerializeObject(scripts, Formatting.Indented));
            }
            return outputPath;
        }

        public List<ScriptEntry> ImportPackage(string packagePath, string targetGroup = "Scripts")
        {
            var imported = new List<ScriptEntry>();
            using (var zip = ZipFile.OpenRead(packagePath))
            {
                var me = zip.GetEntry("package_manifest.json");
                if (me == null) throw new Exception("Invalid package: no manifest.");
                List<ScriptEntry> entries;
                using (var r = new StreamReader(me.Open(), Encoding.UTF8))
                    entries = JsonConvert.DeserializeObject<List<ScriptEntry>>(r.ReadToEnd()) ?? new List<ScriptEntry>();

                foreach (var entry in entries)
                {
                    var de = zip.GetEntry(entry.DllFileName);
                    if (de != null)
                    {
                        string dest = Path.Combine(_scriptsFolderPath, entry.DllFileName);
                        if (File.Exists(dest))
                        {
                            entry.DllFileName = Path.GetFileNameWithoutExtension(entry.DllFileName) + "_imp_" + Guid.NewGuid().ToString("N").Substring(0, 4) + ".dll";
                            dest = Path.Combine(_scriptsFolderPath, entry.DllFileName);
                        }
                        de.ExtractToFile(dest, true);
                    }
                    var se = zip.GetEntry(entry.SourceFileName);
                    if (se != null)
                    {
                        entry.SourceFileName = Path.GetFileNameWithoutExtension(entry.DllFileName) + ".cs";
                        se.ExtractToFile(Path.Combine(_scriptsFolderPath, entry.SourceFileName), true);
                    }
                    entry.Id = "script_" + Guid.NewGuid().ToString("N").Substring(0, 10);
                    entry.Group = targetGroup;
                    entry.CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    var manifest = _scriptManager.LoadFullManifest();
                    manifest.Scripts.Add(entry);
                    if (!manifest.Groups.Any(g => g.Equals(targetGroup, StringComparison.OrdinalIgnoreCase)))
                        manifest.Groups.Add(targetGroup);
                    _scriptManager.SaveFullManifest(manifest);
                    imported.Add(entry);
                }
            }
            return imported;
        }
    }
}
