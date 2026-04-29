using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Autodesk.Revit.UI;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace DevKit.Loader
{
    public class DevKitLoaderApp : IExternalApplication
    {
        // TODO: replace <OWNER>/<REPO> with the actual GitHub repo. If your default branch is "main"
        // instead of "master", change the path segment too.
        // Repo layout expected at this path:
        //   releases/version.txt          — single line, the current release version (bump to deploy)
        //   releases/DevKit-2022.zip      — one zip per Revit version, contents go into %AppData%\DevKit\<YYYY>
        //   releases/DevKit-2023.zip
        //   ...
        // Note: raw.githubusercontent.com applies a ~5-minute CDN cache, so updates can take up to that long
        // to propagate to users after you push.
        private const string RawBaseUrl = "https://raw.githubusercontent.com/Dar-Structural-Computational-Design/DevKit/master/releases";
        private const string UserAgent = "DevKit.Loader";
        private static readonly string LocalBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevKit");
        private const string VersionFile = "version.txt";
        private const string DevKitDllName = "DevKit.dll";
        private const string DevKitAppFullName = "DevKit.DevKitApp";

        private string _revitVersion;
        private string _localPath;
        private object _innerApp;
        private MethodInfo _shutdownMi;

        // The DevKit assembly we just loaded — exposed so the ribbon-button stub below can
        // resolve DevKit's command classes via reflection at button-click time.
        public static Assembly LoadedDevKitAssembly { get; private set; }

        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                _revitVersion = app.ControlledApplication.VersionNumber; // "2022", "2023", ...

                // Dev-mode shortcut: if the loader is running from its own VS bin folder
                // (e.g. when DevKitTester.addin points at it), point _localPath at the sibling
                // DevKit project's bin output for the same Revit version. Skip GitHub entirely.
                // Lets us iterate by Build → Restart Revit, no zip/push/version-bump cycle.
                string devLocal = TryResolveDevBinPath(_revitVersion);
                if (devLocal != null)
                {
                    _localPath = devLocal;
                }
                else
                {
                    _localPath = Path.Combine(LocalBasePath, _revitVersion);
                    Directory.CreateDirectory(_localPath);

                    if (!SyncFromServer())
                    {
                        TaskDialog.Show("DevKit Loader",
                            $"GitHub is unreachable and no local DevKit copy was found for Revit {_revitVersion}.\n\n" +
                            $"Release feed: {RawBaseUrl}\n" +
                            $"Local cache: {_localPath}");
                        return Result.Failed;
                    }
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
                        $"Make sure the release zip DevKit-{_revitVersion}.zip contains the FULL DevKit build output (not just DevKit.dll).\n" +
                        $"Release feed: {RawBaseUrl}");
                    return Result.Failed;
                }

                Assembly asm;
#if NET8_0_OR_GREATER
                // On .NET 8 (Revit 2025+), Revit preloads its own (older) Microsoft.CodeAnalysis.dll.
                // Use a custom AssemblyLoadContext so DevKit gets its OWN bundled Roslyn instead of
                // Revit's. RevitAPI/RevitAPIUI/runtime assemblies fall through to the default context
                // so types stay identity-equal across the boundary. Ribbon-button entry points are
                // routed through OpenEditorCommandStub (defined below in this loader assembly) so Revit
                // never tries to load DevKit.dll into the Default ALC — that would create a duplicate
                // instance and lose the Roslyn isolation.
                var alc = new IsolatedLoadContext(_localPath);
                alc.LoadFromAssemblyPath(roslynCommon);
                alc.LoadFromAssemblyPath(roslynCSharp);
                asm = alc.LoadFromAssemblyPath(localDll);

                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
#else
                // On .NET Framework, Revit does not preload Roslyn — simple LoadFrom + AssemblyResolve works.
                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
                asm = Assembly.LoadFrom(localDll);
#endif

                LoadedDevKitAssembly = asm;

                var type = asm.GetType(DevKitAppFullName, throwOnError: true);
                _innerApp = Activator.CreateInstance(type);

                // Tell DevKit which DLL to point its ribbon button at. PushButtonData uses this path,
                // so when Revit clicks the button it loads OUR loader DLL (already in Default ALC,
                // single instance) instead of trying to load DevKit.dll — which would duplicate it
                // and break Roslyn isolation.
                type.GetField("LoaderAssemblyPath", BindingFlags.Public | BindingFlags.Static)
                    ?.SetValue(null, typeof(DevKitLoaderApp).Assembly.Location);

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
            bool localDllExists = File.Exists(Path.Combine(_localPath, DevKitDllName));
            string versionUrl = $"{RawBaseUrl}/{VersionFile}";
            string zipUrl = $"{RawBaseUrl}/DevKit-{_revitVersion}.zip";

            // 1. Probe GitHub for the current published version.
            string remoteVersion;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) })
                {
                    http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                    remoteVersion = http.GetStringAsync(versionUrl).GetAwaiter().GetResult().Trim();
                }
                if (string.IsNullOrEmpty(remoteVersion))
                    throw new Exception("Remote version.txt was empty.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DevKit Loader] Remote version probe failed: " + ex.Message);
                if (localDllExists)
                {
                    TaskDialog.Show("DevKit Loader",
                        "GitHub unreachable — using last local copy of DevKit.\n\n" + versionUrl);
                    return true;
                }
                return false;
            }

            // 2. Skip download if local cache already matches the remote version.
            string localVersion = ReadVersionOrNull(_localPath);
            bool needDownload = !localDllExists
                                || string.IsNullOrEmpty(localVersion)
                                || !string.Equals(localVersion, remoteVersion, StringComparison.Ordinal);

            if (!needDownload) return true;

            // 3. Download zip → wipe cache → extract → write version.txt.
            string tempZip = Path.Combine(Path.GetTempPath(), $"DevKit-{_revitVersion}-{Guid.NewGuid():N}.zip");
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                    using (var resp = http.GetAsync(zipUrl).GetAwaiter().GetResult())
                    {
                        resp.EnsureSuccessStatusCode();
                        using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
                        {
                            resp.Content.CopyToAsync(fs).GetAwaiter().GetResult();
                        }
                    }
                }

                WipeDirectoryContents(_localPath);
                ZipFile.ExtractToDirectory(tempZip, _localPath);
                File.WriteAllText(Path.Combine(_localPath, VersionFile), remoteVersion);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DevKit Loader] Download/extract failed: " + ex.Message);
                if (localDllExists)
                {
                    TaskDialog.Show("DevKit Loader",
                        "Failed to update DevKit — using last local copy.\n\n" + ex.Message);
                    return true;
                }
                return false;
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
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

        // Returns the dev-mode local path if this loader is running from a Visual Studio bin folder
        // alongside a sibling DevKit project; null otherwise (production install). Side-steps the
        // GitHub fetch and lets the loader's IsolatedLoadContext logic operate on freshly built
        // local DLLs.
        private static string TryResolveDevBinPath(string revitVersion)
        {
            try
            {
                string loaderDll = typeof(DevKitLoaderApp).Assembly.Location;
                if (string.IsNullOrEmpty(loaderDll)) return null;
                string loaderDir = Path.GetDirectoryName(loaderDll);
                if (loaderDir == null) return null;

                // Expected dev layout: <repo>/DevKit.Loader/bin/REVIT<v>/DevKit.Loader.dll
                //                      <repo>/DevKit/bin/REVIT<v>/DevKit.dll
                string parent3 = Directory.GetParent(loaderDir)?.Parent?.Parent?.FullName;
                if (parent3 == null) return null;

                string candidate = Path.Combine(parent3, "DevKit", "bin", "REVIT" + revitVersion);
                if (File.Exists(Path.Combine(candidate, DevKitDllName)))
                    return candidate;
            }
            catch { }
            return null;
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

    // Ribbon-button entry point. Lives in the loader (Default ALC) so Revit doesn't load DevKit.dll
    // a second time on click. Forwards the call into DevKit's real command in the IsolatedLoadContext.
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class OpenEditorCommandStub : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                var devkit = DevKitLoaderApp.LoadedDevKitAssembly;
                if (devkit == null)
                {
                    message = "DevKit assembly not loaded — loader bootstrap failed.";
                    return Result.Failed;
                }
                var cmdType = devkit.GetType("DevKit.Commands.OpenEditorCommand", throwOnError: true);
                var cmd = (IExternalCommand)Activator.CreateInstance(cmdType);
                return cmd.Execute(commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }
    }
}
