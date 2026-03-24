using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using DevKit.Models;

namespace DevKit.Services
{
    public class RoslynCompilerService
    {
        private const string TEMPLATE = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.Attributes;
{2}

namespace DevKit.DynamicScripts
{{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class {0} : IExternalCommand
    {{
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {{
            try
            {{
                return RunBody(commandData.Application);
            }}
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {{
                return Result.Cancelled;
            }}
            catch (Exception ex)
            {{
                message = ex.Message;
                TaskDialog.Show(""Script Error"", ex.ToString());
                return Result.Failed;
            }}
        }}

        public static void Run(UIApplication uiApp)
        {{
            try
            {{
                RunBody(uiApp);
            }}
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) {{ }}
            catch (Exception ex)
            {{
                throw;
            }}
        }}

        private static Result RunBody(UIApplication uiApp)
        {{
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            {1}

            return Result.Succeeded;
        }}
    }}
}}
";

        private readonly string _librariesPath;
        private readonly string _userLibrariesPath;

        public RoslynCompilerService(string scriptsFolderPath)
        {
            string addinFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _librariesPath = Path.Combine(addinFolder, "Libraries");
            _userLibrariesPath = Path.Combine(scriptsFolderPath, "Libraries");
            if (!Directory.Exists(_userLibrariesPath)) Directory.CreateDirectory(_userLibrariesPath);
        }

        public static string GenerateClassName() => "DynCmd_" + Guid.NewGuid().ToString("N").Substring(0, 12);

        public CompilationResult CompileForTest(string userCode, string className)
        {
            var (extraUsings, body) = ExtractUsings(userCode);
            string src = string.Format(TEMPLATE, className, body, extraUsings);
            var (lineOff, colOff) = CountOffsets(src, body);
            return Compile(src, "DK_Test_" + Guid.NewGuid().ToString("N").Substring(0, 8), className, false, null, lineOff, colOff);
        }

        public CompilationResult CompileForBuild(string userCode, string className, string outputDllPath)
        {
            var (extraUsings, body) = ExtractUsings(userCode);
            string src = string.Format(TEMPLATE, className, body, extraUsings);
            var (lineOff, colOff) = CountOffsets(src, body);
            return Compile(src, Path.GetFileNameWithoutExtension(outputDllPath), className, true, outputDllPath, lineOff, colOff);
        }

        private static (string extraUsings, string codeBody) ExtractUsings(string userCode)
        {
            var lines = userCode.Split(new[] { '\n' }, StringSplitOptions.None);
            var usings = new List<string>();
            var body = new List<string>();
            bool doneWithUsings = false;

            foreach (var line in lines)
            {
                string trimmed = line.TrimStart();
                if (!doneWithUsings && trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
                    usings.Add(trimmed);
                else
                {
                    doneWithUsings = true;
                    body.Add(line);
                }
            }
            return (string.Join("\n", usings), string.Join("\n", body));
        }

        private CompilationResult Compile(string source, string asmName, string className, bool toDisk, string outputPath, int lineOffset, int colOffset)
        {
            var tree = CSharpSyntaxTree.ParseText(source);
            var refs = GatherReferences();
            var compilation = CSharpCompilation.Create(asmName,
                new[] { tree }, refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release).WithPlatform(Platform.AnyCpu));

            if (toDisk)
            {
                EmitResult r = compilation.Emit(outputPath);
                if (!r.Success) { TryDelete(outputPath); return ErrResult(r, lineOffset, colOffset); }
                return new CompilationResult { Success = true, DllPath = outputPath, ClassName = $"DevKit.DynamicScripts.{className}" };
            }
            using (var ms = new MemoryStream())
            {
                EmitResult r = compilation.Emit(ms);
                if (!r.Success) return ErrResult(r, lineOffset, colOffset);
                ms.Seek(0, SeekOrigin.Begin);
                return new CompilationResult { Success = true, CompiledAssembly = Assembly.Load(ms.ToArray()), ClassName = $"DevKit.DynamicScripts.{className}" };
            }
        }

        private List<MetadataReference> GatherReferences()
        {
            var refs = new List<MetadataReference>();
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { if (!asm.IsDynamic && !string.IsNullOrEmpty(asm.Location) && added.Add(asm.Location)) refs.Add(MetadataReference.CreateFromFile(asm.Location)); } catch { }
            }
            AddDlls(refs, added, _librariesPath);
            AddDlls(refs, added, _userLibrariesPath);
            return refs;
        }

        private void AddDlls(List<MetadataReference> refs, HashSet<string> added, string folder)
        {
            if (!Directory.Exists(folder)) return;
            foreach (string dll in Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories))
                try { if (added.Add(dll)) refs.Add(MetadataReference.CreateFromFile(dll)); } catch { }
        }

        private CompilationResult ErrResult(EmitResult r, int lineOffset, int colOffset) => new CompilationResult
        {
            Success = false,
            Errors = r.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d =>
            {
                var s = d.Location.GetLineSpan();
                int line = Math.Max(1, s.StartLinePosition.Line - lineOffset + 1);
                int col = s.StartLinePosition.Character + 1;
                if (s.StartLinePosition.Line == lineOffset)
                    col = Math.Max(1, col - colOffset);
                return $"Line {line}, Col {col}: {d.GetMessage()} [{d.Id}]";
            }).ToList()
        };

        private static (int lineOffset, int colOffset) CountOffsets(string fullSource, string userBody)
        {
            if (string.IsNullOrEmpty(userBody)) return (0, 0);
            int idx = fullSource.IndexOf(userBody);
            if (idx < 0) return (0, 0);
            int lines = 0;
            int colOffset = 0;
            for (int i = 0; i < idx; i++)
            {
                if (fullSource[i] == '\n') { lines++; colOffset = 0; }
                else colOffset++;
            }
            return (lines, colOffset);
        }

        private void TryDelete(string p) { try { if (p != null && File.Exists(p)) File.Delete(p); } catch { } }
    }
}
