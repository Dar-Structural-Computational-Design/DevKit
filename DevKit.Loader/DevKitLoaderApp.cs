using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;

namespace DevKit.Loader
{
    public class DevKitLoaderApp : IExternalApplication
    {
        // TODO: adjust when server path changes
        private static readonly string ServerPath = @"K:\Dar_Cads\General\Computational Design Team\Revit API Tools\Dar Piles Suite V3";
        private static readonly string LocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevKit");
        private const string VersionFile = "version.txt";
        private const string DevKitDllName = "DevKit.dll";
        private const string DevKitAppFullName = "DevKit.DevKitApp";

        private object _innerApp;
        private MethodInfo _shutdownMi;

        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                Directory.CreateDirectory(LocalPath);

                if (!SyncFromServer())
                {
                    TaskDialog.Show("DevKit Loader",
                        "Server is unreachable and no local DevKit copy was found.\n\n" +
                        $"Expected server: {ServerPath}\n" +
                        $"Local cache: {LocalPath}");
                    return Result.Failed;
                }

                string localDll = Path.Combine(LocalPath, DevKitDllName);
                if (!File.Exists(localDll))
                {
                    TaskDialog.Show("DevKit Loader", $"DevKit.dll not found at:\n{localDll}");
                    return Result.Failed;
                }

                // Install resolver BEFORE loading so dependent assemblies can be found
                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

                var asm = Assembly.LoadFrom(localDll);
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
                if (Directory.Exists(ServerPath))
                {
                    serverReachable = true;
                    serverVersion = ReadVersionOrNull(ServerPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DevKit Loader] Server probe failed: " + ex.Message);
                serverReachable = false;
            }

            bool localDllExists = File.Exists(Path.Combine(LocalPath, DevKitDllName));

            if (!serverReachable)
            {
                if (localDllExists)
                {
                    TaskDialog.Show("DevKit Loader",
                        "Server unreachable — using last local copy of DevKit.\n\n" +
                        $"Server: {ServerPath}");
                    return true;
                }
                return false;
            }

            string localVersion = ReadVersionOrNull(LocalPath);
            bool needCopy = !localDllExists
                            || string.IsNullOrEmpty(localVersion)
                            || !string.Equals(localVersion, serverVersion, StringComparison.Ordinal);

            if (needCopy)
            {
                try
                {
                    CopyDirectory(ServerPath, LocalPath);
                    if (!string.IsNullOrEmpty(serverVersion))
                        File.WriteAllText(Path.Combine(LocalPath, VersionFile), serverVersion);
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

            return File.Exists(Path.Combine(LocalPath, DevKitDllName));
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

        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            try
            {
                string simpleName = new AssemblyName(args.Name).Name + ".dll";
                string candidate = Path.Combine(LocalPath, simpleName);
                if (File.Exists(candidate))
                    return Assembly.LoadFrom(candidate);
            }
            catch { }
            return null;
        }
    }
}
