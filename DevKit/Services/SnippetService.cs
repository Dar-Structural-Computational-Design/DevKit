using System.Collections.Generic;
using DevKit.Models;

namespace DevKit.Services
{
    public static class SnippetService
    {
        public static List<Snippet> GetAll() => new List<Snippet>
        {
            new Snippet { Name = "Hello World", Code = "TaskDialog.Show(\"Hello\", \"Hello from DevKit!\\nDocument: \" + doc.Title);" },
            new Snippet { Name = "Collect All Walls", Code = @"var walls = new FilteredElementCollector(doc)
    .OfClass(typeof(Wall)).WhereElementIsNotElementType().ToElements();
TaskDialog.Show(""Walls"", $""Found {walls.Count} walls."");" },
            new Snippet { Name = "Get Selected Elements", Code = @"var ids = uiDoc.Selection.GetElementIds();
if (ids.Count == 0) { TaskDialog.Show(""Selection"", ""Nothing selected.""); }
else { string info = """"; foreach (ElementId id in ids) { Element elem = doc.GetElement(id); info += $""{elem.Name} (Id: {id.IntegerValue})\n""; } TaskDialog.Show(""Selected"", info); }" },
            new Snippet { Name = "Delete Selected", Code = @"var ids = uiDoc.Selection.GetElementIds();
if (ids.Count == 0) { TaskDialog.Show(""Delete"", ""Nothing selected.""); }
else { using (Transaction tx = new Transaction(doc, ""Delete Selected"")) { tx.Start(); doc.Delete(ids); tx.Commit(); } TaskDialog.Show(""Delete"", $""Deleted {ids.Count} elements.""); }" },
            new Snippet { Name = "Transaction Template", Code = @"using (Transaction tx = new Transaction(doc, ""My Transaction""))
{
    tx.Start();
    // Your code here
    tx.Commit();
}" },
            new Snippet { Name = "Get All Levels", Code = @"var levels = new FilteredElementCollector(doc)
    .OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
string info = """"; foreach (var level in levels) info += $""{level.Name}: {level.Elevation:F2} ft\n"";
TaskDialog.Show(""Levels"", info);" },
            new Snippet { Name = "Active View Info", Code = @"View v = doc.ActiveView;
TaskDialog.Show(""Active View"", $""Name: {v.Name}\nType: {v.ViewType}\nScale: 1:{v.Scale}\nDetail: {v.DetailLevel}"");" },
            new Snippet { Name = "Rooms with Area", Code = @"var rooms = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().Cast<Room>().Where(r => r.Area > 0).ToList();
string info = """"; foreach (var r in rooms) info += $""{r.Number} - {r.Name}: {r.Area:F1} sq ft\n"";
TaskDialog.Show(""Rooms"", $""Found {rooms.Count} rooms:\n\n{info}"");" },
            new Snippet { Name = "Export Walls to CSV", Code = @"var walls = new FilteredElementCollector(doc)
    .OfClass(typeof(Wall)).WhereElementIsNotElementType().Cast<Wall>().ToList();
string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
string filePath = Path.Combine(desktop, ""WallExport.csv"");
var lines = new List<string> { ""Id,Name,Length(ft),Height(ft)"" };
foreach (var w in walls) { double len = w.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0; double ht = w.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0; lines.Add($""{w.Id.IntegerValue},{w.Name},{len:F2},{ht:F2}""); }
File.WriteAllLines(filePath, lines);
TaskDialog.Show(""Export"", $""Exported {walls.Count} walls to:\n{filePath}"");" },
        };
    }
}
