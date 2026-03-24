using System;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;
using DevKit.Helpers;

namespace DevKit.Handlers
{
    public class TestHandler : IExternalEventHandler
    {
        public void Execute(UIApplication uiApp)
        {
            try
            {
                Assembly asm = SharedState.CompiledAssembly;
                string className = SharedState.ClassName;
                if (asm == null || string.IsNullOrEmpty(className)) { Report(false, "No compiled assembly found."); return; }
                Type type = asm.GetType(className);
                if (type == null) { Report(false, $"Class '{className}' not found. Available: {string.Join(", ", asm.GetExportedTypes().Select(t => t.FullName))}"); return; }
                MethodInfo run = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(UIApplication) }, null);
                if (run == null) { Report(false, "Run(UIApplication) method not found."); return; }
                run.Invoke(null, new object[] { uiApp });
                Report(true, "Script executed successfully!");
            }
            catch (TargetInvocationException tie) { var ex = tie.InnerException ?? tie; Report(false, $"Runtime error:\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}"); }
            catch (Exception ex) { Report(false, $"Error:\n{ex.GetType().Name}: {ex.Message}"); }
        }
        public string GetName() => "DevKit_TestHandler";
        private void Report(bool ok, string msg) { try { SharedState.OnResultCallback?.Invoke(ok, msg); } catch { } }
    }
}
