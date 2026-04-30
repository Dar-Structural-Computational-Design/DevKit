using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Autodesk.Revit.UI;
using DevKit.Handlers;
using DevKit.Services;

namespace DevKit
{
    public class DevKitApp : IExternalApplication
    {
        public static ExternalEvent TestEvent { get; private set; }
        public static ExternalEvent AddEvent { get; private set; }
        public static ExternalEvent DeleteEvent { get; private set; }
        public static ExternalEvent CreateGroupEvent { get; private set; }
        public static RibbonPanel ScriptsPanel { get; private set; }
        public static Dictionary<string, PulldownButton> GroupPulldowns { get; private set; } = new Dictionary<string, PulldownButton>(StringComparer.OrdinalIgnoreCase);
        public static ScriptManagerService ScriptManager { get; private set; }
        public static RoslynCompilerService Compiler { get; private set; }
        public static string ScriptsFolderPath { get; private set; }

        // Set by DevKit.Loader before OnStartup runs. Used to point ribbon-button PushButtonData
        // at the loader DLL (which lives in Default ALC, single-instance) so Revit doesn't try to
        // load DevKit.dll a second time when the user clicks a button.
        public static string LoaderAssemblyPath;

        private const string TAB = "DevKit";

        public static string IconsPath = "DevKit.Icons";
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                string addinFolder = Path.GetDirectoryName(typeof(DevKitApp).Assembly.Location);
                if (string.IsNullOrEmpty(addinFolder))
                {
                    addinFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "DevKit",
                        app.ControlledApplication.VersionNumber);
                }

                // Scripts folder lives OUTSIDE the loader's cache path (addinFolder), as a sibling
                // of the per-version cache directory. The loader wipes addinFolder on every update,
                // so storing scripts there used to nuke all user work. New layout:
                //   %AppData%\DevKit\<RevitVersion>\           ← cache, wiped on update (addinFolder)
                //   %AppData%\DevKit\Scripts\<RevitVersion>\   ← user scripts, never wiped
                ScriptsFolderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DevKit", "Scripts", app.ControlledApplication.VersionNumber);

                // One-time migration from the legacy in-cache location. This is a no-op for users
                // who already lost their scripts to a wipe (source folder won't exist), but covers
                // the edge case where DevKit updates without the loader running its wipe pass.
                try
                {
                    string legacyScripts = Path.Combine(addinFolder, "DevKit_Scripts");
                    if (Directory.Exists(legacyScripts) && !Directory.Exists(ScriptsFolderPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(ScriptsFolderPath));
                        Directory.Move(legacyScripts, ScriptsFolderPath);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DevKit] Script migration skipped: {ex.Message}"); }

                string scriptsRoot = ScriptsFolderPath; // local capture for the closure
                AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
                {
                    string dll = new AssemblyName(args.Name).Name + ".dll";
                    foreach (string folder in new[] { addinFolder, Path.Combine(addinFolder, "Libraries"), Path.Combine(scriptsRoot, "Libraries") })
                    {
                        string p = Path.Combine(folder, dll);
                        if (File.Exists(p)) return Assembly.LoadFrom(p);
                    }
                    return null;
                };

                ScriptManager = new ScriptManagerService(ScriptsFolderPath);
                Compiler = new RoslynCompilerService(ScriptsFolderPath);

                app.CreateRibbonTab(TAB);
                var editorPanel = app.CreateRibbonPanel(TAB, "Editor");

                // Route through the loader's stub so Revit doesn't load DevKit.dll into the Default ALC.
                // Falls back to direct DevKit load if running without the loader (dev/test scenarios).
                string buttonDllPath = !string.IsNullOrEmpty(LoaderAssemblyPath) ? LoaderAssemblyPath : Assembly.GetExecutingAssembly().Location;
                string buttonClassName = !string.IsNullOrEmpty(LoaderAssemblyPath) ? "DevKit.Loader.OpenEditorCommandStub" : "DevKit.Commands.OpenEditorCommand";
                editorPanel.AddItem(new PushButtonData("cmdOpenEditor", "Open\nEditor", buttonDllPath, buttonClassName)
                {
                    ToolTip = "Open the DevKit script editor",
                    LargeImage = PngImageSource($"{IconsPath}.Devkit32.png"),
                    Image = PngImageSource($"{IconsPath}.Devkit32.png"),
                });

                ScriptsPanel = app.CreateRibbonPanel(TAB, "My Scripts");
                var manifest = ScriptManager.LoadFullManifest();
                foreach (string group in manifest.Groups) CreateGroupPulldown(group);

                foreach (var entry in manifest.Scripts)
                {
                    try
                    {
                        string dllPath = Path.Combine(ScriptsFolderPath, entry.DllFileName);
                        if (!File.Exists(dllPath)) continue;
                        string grp = entry.Group ?? "Scripts";
                        if (!GroupPulldowns.ContainsKey(grp)) CreateGroupPulldown(grp);
                        GroupPulldowns[grp].AddPushButton(new PushButtonData(entry.Id, entry.ButtonName, dllPath, entry.ClassName)
                        {
                            ToolTip = $"DevKit: {entry.ButtonName}",
                            LargeImage = IconGeneratorService.CreateScriptButtonIcon(entry.ButtonName, entry.IconGlyph, 32),
                            Image = IconGeneratorService.CreateScriptButtonIcon(entry.ButtonName, entry.IconGlyph, 16)
                        });
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DevKit] Restore failed: {entry.ButtonName}: {ex.Message}"); }
                }

                TestEvent = ExternalEvent.Create(new TestHandler());
                AddEvent = ExternalEvent.Create(new AddHandler());
                DeleteEvent = ExternalEvent.Create(new DeleteHandler());
                CreateGroupEvent = ExternalEvent.Create(new CreateGroupHandler());
                return Result.Succeeded;
            }
            catch (Exception ex) { TaskDialog.Show("DevKit Error", ex.ToString()); return Result.Failed; }
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;

        public static void CreateGroupPulldown(string groupName)
        {
            if (GroupPulldowns.ContainsKey(groupName)) return;
            var pulldown = ScriptsPanel.AddItem(new PulldownButtonData("pulldown_" + groupName.Replace(" ", "_").ToLower(), groupName)
            {
                ToolTip = $"Scripts: {groupName}",
                LargeImage = IconGeneratorService.CreateScriptsIcon(32),
                Image = IconGeneratorService.CreateScriptsIcon(16)
            }) as PulldownButton;
            GroupPulldowns[groupName] = pulldown;
        }


        private ImageSource PngImageSource(string embeddedPath)
        {
            Stream stream = this.GetType().Assembly.GetManifestResourceStream(embeddedPath);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);

            return decoder.Frames[0];
        }


    }
}
