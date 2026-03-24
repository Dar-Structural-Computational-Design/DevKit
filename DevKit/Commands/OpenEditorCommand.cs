using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DevKit.Views;

namespace DevKit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpenEditorCommand : IExternalCommand
    {
        private static EditorWindow _window;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                try { if (_window != null && _window.IsLoaded && _window.IsVisible) { _window.Activate(); return Result.Succeeded; } } catch { _window = null; }
                _window = new EditorWindow();
                new System.Windows.Interop.WindowInteropHelper(_window) { Owner = commandData.Application.MainWindowHandle };
                _window.Closed += (s, e) => _window = null;
                _window.Show();
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }
}
