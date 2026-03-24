using System;
using System.Reflection;

namespace DevKit.Helpers
{
    public static class SharedState
    {
        public static Assembly CompiledAssembly { get; set; }
        public static string ClassName { get; set; }
        public static string DllPath { get; set; }
        public static string ButtonName { get; set; }
        public static string ButtonId { get; set; }
        public static string GroupName { get; set; }
        public static string DeleteScriptId { get; set; }
        public static Action<bool, string> OnResultCallback { get; set; }
    }
}
