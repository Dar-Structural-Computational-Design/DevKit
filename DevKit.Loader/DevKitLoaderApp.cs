using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace DevKit.Loader
{
    public class DevKitLoaderApp : IExternalApplication
    {
        // TODO: adjust when server path changes. Revit version is appended at runtime (e.g. "\2022").
        //private static readonly string ServerBasePath = @"K:\Dar_Cads\General\Computational Design Team\Revit API Tools\DevKit";
        //private static readonly string ServerBasePath = @"C:\Users\mosta\Desktop\DevKit Server"; // Personal PC
        private static readonly string ServerBasePath = @"C:\Users\mostafa.elbagoury\Desktop\ServerTest\DevKit"; // Dar PC
        private static readonly string LocalBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevKit");
        private const string VersionFile = "version.txt";
        private const string DevKitDllName = "DevKit.dll";
        private const string DevKitAppFullName = "DevKit.DevKitApp";

        private string _serverPath;
        private string _localPath;
        private object _innerApp;
        private MethodInfo _shutdownMi;

        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                string revitVersion = app.ControlledApplication.VersionNumber; // "2022", "2023", ...
                _serverPath = Path.Combine(ServerBasePath, revitVersion);
                _localPath = Path.Combine(LocalBasePath, revitVersion);

                Directory.CreateDirectory(_localPath);

                if (!SyncFromServer())
                {
                    TaskDialog.Show("DevKit Loader",
                        $"Server is unreachable and no local DevKit copy was found for Revit {revitVersion}.\n\n" +
                        $"Expected server: {_serverPath}\n" +
                        $"Local cache: {_localPath}");
                    return Result.Failed;
                }

                string localDll = Path.Combine(_localPath, DevKitDllName);
                if (!File.Exists(localDll))
                {
                    TaskDialog.Show("DevKit Loader", $"DevKit.dll not found at:\n{localDll}");
                    return Result.Failed;
                }

                // Sanity check: Roslyn DLLs MUST be present in the local cache, otherwise the ALC
                // can't isolate them and DevKit will pick up Revit's older preloaded version.
                string roslynCommon = Path.Combine(_localPath, "Microsoft.CodeAnalysis.dll");
                string roslynCSharp = Path.Combine(_localPath, "Microsoft.CodeAnalysis.CSharp.dll");
                if (!File.Exists(roslynCommon) || !File.Exists(roslynCSharp))
                {
                    TaskDialog.Show("DevKit Loader Error",
                        "Roslyn DLLs are missing from the local cache.\n\n" +
                        $"Cache: {_localPath}\n\n" +
                        $"Missing: {(File.Exists(roslynCommon) ? "" : "Microsoft.CodeAnalysis.dll  ")}" +
                        $"{(File.Exists(roslynCSharp) ? "" : "Microsoft.CodeAnalysis.CSharp.dll")}\n\n" +
                        "Make sure the server folder contains the FULL DevKit build output (not just DevKit.dll).\n" +
                        $"Server: {_serverPath}");
                    return Result.Failed;
                }

                Assembly asm;
#if NET8_0_OR_GREATER
                // On .NET 8 (Revit 2025+), Revit preloads its own (older) Microsoft.CodeAnalysis.dll.
                // Use a custom AssemblyLoadContext so DevKit gets its OWN bundled Roslyn instead of
                // Revit's. RevitAPI/RevitAPIUI/runtime assemblies fall through to the default context
                // so types stay identity-equal across the boundary.
                var alc = new IsolatedLoadContext(_localPath);

                // CRITICAL: Pre-load Roslyn into the isolated ALC BEFORE DevKit.dll is touched.
                // This guarantees that when DevKit's JIT first resolves Microsoft.CodeAnalysis.CSharp,
                // our 4.8.0 copy is already in the ALC and gets used instead of Revit's older one.
                alc.LoadFromAssemblyPath(roslynCommon);
                alc.LoadFromAssemblyPath(roslynCSharp);

                asm = alc.LoadFromAssemblyPath(localDll);
#else
                // On .NET Framework, Revit does not preload Roslyn — simple LoadFrom + AssemblyResolve works.
                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
                asm = Assembly.LoadFrom(localDll);
#endif

                var type = asm.GetType(DevKitAppFullName, throwOnError: true);
                _innerApp = Activator.CreateInstance(type);

                var onStartup = type.GetMethod("OnStartup", new[] { typeof(UIControlledApplication) });
                _shutdownMi = type.GetMethod("OnShutdown", new[] { typeof(UIControlledApplication) });

                if (onStartup == null)
                {
                    TaskDialog.Show("DevKit Loader", $"Could not find OnStartup method on {DevKitAppFullName}.");
                    return Result.Failed;
                }

                return (Result)onStartup.Invoke(_innerApp, new object[] { app });
            }
            catch (Exception ex)
            {
                TaskDialog.Show("DevKit Loader Error", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            try
            {
                if (_innerApp != null && _shutdownMi != null)
                    return (Result)_shutdownMi.Invoke(_innerApp, new object[] { app });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DevKit Loader] OnShutdown error: " + ex.Message);
            }
            return Result.Succeeded;
        }

        // Returns true if DevKit is available locally (either freshly synced or falling back to cache).
        private bool SyncFromServer()
        {
            string serverVersion = null;
            bool serverReachable = false;
            try
            {
                if (Directory.Exists(_serverPath))
                {
                    serverReachable = true;
                    serverVersion = ReadVersionOrNull(_serverPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DevKit Loader] Server probe failed: " + ex.Message);
                serverReachable = false;
            }

            bool localDllExists = File.Exists(Path.Combine(_localPath, DevKitDllName));

            if (!serverReachable)
            {
                if (localDllExists)
                {
                    TaskDialog.Show("DevKit Loader",
                        "Server unreachable — using last local copy of DevKit.\n\n" +
                        $"Server: {_serverPath}");
                    return true;
                }
                return false;
            }

            string localVersion = ReadVersionOrNull(_localPath);
            bool needCopy = !localDllExists
                            || string.IsNullOrEmpty(localVersion)
                            || !string.Equals(localVersion, serverVersion, StringComparison.Ordinal);

            if (needCopy)
            {
                try
                {
                    // Wipe stale files first so removed/renamed deps don't linger in the cache
                    WipeDirectoryContents(_localPath);
                    CopyDirectory(_serverPath, _localPath);
                    if (!string.IsNullOrEmpty(serverVersion))
                        File.WriteAllText(Path.Combine(_localPath, VersionFile), serverVersion);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[DevKit Loader] Copy failed: " + ex.Message);
                    if (localDllExists)
                    {
                        TaskDialog.Show("DevKit Loader",
                            "Failed to update DevKit from server — using last local copy.\n\n" + ex.Message);
                        return true;
                    }
                    return false;
                }
            }

            return File.Exists(Path.Combine(_localPath, DevKitDllName));
        }

        private static string ReadVersionOrNull(string folder)
        {
            try
            {
                string path = Path.Combine(folder, VersionFile);
                if (!File.Exists(path)) return null;
                return File.ReadAllText(path).Trim();
            }
            catch { return null; }
        }

        private static void WipeDirectoryContents(string folder)
        {
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder))
            {
                try { File.Delete(file); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DevKit Loader] Wipe skip {file}: {ex.Message}"); }
            }
            foreach (string dir in Directory.GetDirectories(folder))
            {
                try { Directory.Delete(dir, recursive: true); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DevKit Loader] Wipe skip {dir}: {ex.Message}"); }
            }
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);

            foreach (string file in Directory.GetFiles(src))
            {
                string dest = Path.Combine(dst, Path.GetFileName(file));
                try
                {
                    File.Copy(file, dest, overwrite: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DevKit Loader] Skipped {file}: {ex.Message}");
                }
            }

            foreach (string dir in Directory.GetDirectories(src))
            {
                string destSub = Path.Combine(dst, Path.GetFileName(dir));
                CopyDirectory(dir, destSub);
            }
        }

        private Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            try
            {
                string simpleName = new AssemblyName(args.Name).Name + ".dll";
                string candidate = Path.Combine(_localPath, simpleName);
                if (File.Exists(candidate))
                    return Assembly.LoadFrom(candidate);
            }
            catch { }
            return null;
        }

#if NET8_0_OR_GREATER
        // Isolated context: prefer DLLs from _localPath, fall through to default for shared assemblies
        // (RevitAPI, RevitAPIUI, AdWindows, mscorlib, System.*, WindowsBase, PresentationFramework, etc.).
        private sealed class IsolatedLoadContext : AssemblyLoadContext
        {
            private readonly string _baseDir;

            public IsolatedLoadContext(string baseDir)
                : base("DevKitIsolated", isCollectible: false)
            {
                _baseDir = baseDir;
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                try
                {
                    string candidate = Path.Combine(_baseDir, assemblyName.Name + ".dll");
                    if (File.Exists(candidate))
                        return LoadFromAssemblyPath(candidate);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DevKit Loader] ALC load failed for {assemblyName.Name}: {ex.Message}");
                }
                return null; // fall through to Default ALC
            }
        }
#endif
    }
}
