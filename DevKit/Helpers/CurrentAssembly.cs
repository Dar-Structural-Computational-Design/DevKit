using System;
using System.IO;
using System.Reflection;

namespace DevKit.Helpers
{
    public class CurrentAssembly
    {
        public static string GetCurrentAssemblyDirectory()
        {
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }
    }
}
