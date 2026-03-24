using System;
using Autodesk.Revit.UI;
using DevKit.Helpers;

namespace DevKit.Handlers
{
    public class DeleteHandler : IExternalEventHandler
    {
        public void Execute(UIApplication uiApp)
        {
            try
            {
                string scriptId = SharedState.DeleteScriptId;
                if (string.IsNullOrEmpty(scriptId)) { Report(false, "No script ID specified."); return; }
                bool removed = DevKitApp.ScriptManager.RemoveScript(scriptId);
                Report(removed, removed ? "Script deleted. Ribbon updates after restart." : $"Script '{scriptId}' not found.");
            }
            catch (Exception ex) { Report(false, $"Delete failed: {ex.Message}"); }
        }
        public string GetName() => "DevKit_DeleteHandler";
        private void Report(bool ok, string msg) { try { SharedState.OnResultCallback?.Invoke(ok, msg); } catch { } }
    }
}
