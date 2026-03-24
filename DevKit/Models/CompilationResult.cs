using System;
using System.Collections.Generic;
using System.Reflection;

namespace DevKit.Models
{
    public class CompilationResult
    {
        public bool Success { get; set; }
        public Assembly CompiledAssembly { get; set; }
        public string DllPath { get; set; }
        public string ClassName { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string ErrorSummary => string.Join(Environment.NewLine, Errors);
    }
}
