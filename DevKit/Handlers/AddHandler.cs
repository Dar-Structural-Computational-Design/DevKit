using System;
using Autodesk.Revit.UI;
using DevKit.Helpers;
using DevKit.Services;

namespace DevKit.Handlers
{
    public class AddHandler : IExternalEventHandler
    {
        public void Execute(UIApplication uiApp)
        {
            try
            {
                string dllPath = SharedState.DllPath, className = SharedState.ClassName;
                string buttonName = SharedState.ButtonName, buttonId = SharedState.ButtonId;
                string group = SharedState.GroupName ?? "Scripts";
                if (string.IsNullOrEmpty(dllPath) || string.IsNullOrEmpty(className)) { Report(false, "Missing DLL path or class name."); return; }
                if (string.IsNullOrEmpty(buttonName)) buttonName = "Unnamed Script";
                if (string.IsNullOrEmpty(buttonId)) buttonId = "dk_" + Guid.NewGuid().ToString("N").Substring(0, 10);

                if (!DevKitApp.GroupPulldowns.ContainsKey(group)) DevKitApp.CreateGroupPulldown(group);
                var pulldown = DevKitApp.GroupPulldowns[group];
                if (pulldown == null) { Report(false, $"Pulldown '{group}' not found."); return; }

                pulldown.AddPushButton(new PushButtonData(buttonId, buttonName, dllPath, className)
                {
                    ToolTip = $"DevKit: {buttonName}",
                    LargeImage = IconGeneratorService.CreateScriptButtonIcon(buttonName, 32),
                    Image = IconGeneratorService.CreateScriptButtonIcon(buttonName, 16)
                });
                Report(true, $"Button '{buttonName}' added to [{group}]!");
            }
            catch (Exception ex) { Report(false, $"Failed: {ex.GetType().Name}: {ex.Message}"); }
        }
        public string GetName() => "DevKit_AddHandler";
        private void Report(bool ok, string msg) { try { SharedState.OnResultCallback?.Invoke(ok, msg); } catch { } }
    }
}
