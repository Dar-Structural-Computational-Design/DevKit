using System;
using Autodesk.Revit.UI;
using DevKit.Helpers;

namespace DevKit.Handlers
{
    public class CreateGroupHandler : IExternalEventHandler
    {
        public void Execute(UIApplication uiApp)
        {
            try
            {
                string group = SharedState.GroupName;
                if (string.IsNullOrEmpty(group)) { Report(false, "No group name."); return; }
                DevKitApp.CreateGroupPulldown(group);
                Report(true, $"Group '{group}' added to the ribbon!");
            }
            catch (Exception ex) { Report(false, $"Failed: {ex.Message}"); }
        }
        public string GetName() => "DevKit_CreateGroupHandler";
        private void Report(bool ok, string msg) { try { SharedState.OnResultCallback?.Invoke(ok, msg); } catch { } }
    }
}
