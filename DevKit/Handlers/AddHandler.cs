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

                string iconGlyph = SharedState.IconGlyph;
                pulldown.AddPushButton(new PushButtonData(buttonId, buttonName, dllPath, className)
                {
                    ToolTip = $"DevKit: {buttonName}",
                    LargeImage = IconGeneratorService.CreateScriptButtonIcon(buttonName, iconGlyph, 32),
                    Image = IconGeneratorService.CreateScriptButtonIcon(buttonName, iconGlyph, 16)
                });
                Report(true, $"Button '{buttonName}' added to [{group}]!");

                // Telemetry / audit fields — populated for downstream implementation
                // (logging, usage reporting, etc).
                //string pcName = Environment.MachineName;
                //string domainName = Environment.UserDomainName;
                //string toolName = buttonName;
                //string groupName = group;
                //double toolCost = SharedState.ToolCost; // set by EditorViewModel from AI cost tracking before raising AddEvent
                //DateTime timestamp = DateTime.Now;


            }
            catch (Exception ex) { Report(false, $"Failed: {ex.GetType().Name}: {ex.Message}"); }
        }
        public string GetName() => "DevKit_AddHandler";
        private void Report(bool ok, string msg) { try { SharedState.OnResultCallback?.Invoke(ok, msg); } catch { } }
    }
}
