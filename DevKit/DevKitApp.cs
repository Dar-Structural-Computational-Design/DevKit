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

        private const string TAB = "DevKit";

        private static string api_key = "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";


        public static string IconsPath = "DevKit.Icons";
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                string addinFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);


                AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
                {
                    string dll = new AssemblyName(args.Name).Name + ".dll";
                    foreach (string folder in new[] { addinFolder, Path.Combine(addinFolder, "Libraries"), Path.Combine(addinFolder, "DevKit_Scripts", "Libraries") })
                    {
                        string p = Path.Combine(folder, dll);
                        if (File.Exists(p)) return Assembly.LoadFrom(p);
                    }
                    return null;
                };

                ScriptsFolderPath = Path.Combine(addinFolder, "DevKit_Scripts");
                ScriptManager = new ScriptManagerService(ScriptsFolderPath);
                Compiler = new RoslynCompilerService(ScriptsFolderPath);

                app.CreateRibbonTab(TAB);
                var editorPanel = app.CreateRibbonPanel(TAB, "Editor");

                editorPanel.AddItem(new PushButtonData("cmdOpenEditor", "Open\nEditor", Assembly.GetExecutingAssembly().Location, "DevKit.Commands.OpenEditorCommand")
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
                            LargeImage = IconGeneratorService.CreateScriptButtonIcon(entry.ButtonName, 32),
                            Image = IconGeneratorService.CreateScriptButtonIcon(entry.ButtonName, 16)
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
