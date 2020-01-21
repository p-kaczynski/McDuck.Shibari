using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace McDuck.Shibari
{
    internal class GacInfo
    {
        internal static ISet<AssemblyDesc> LoadKnownAssemblies()
        {
            var allAssemblies = new HashSet<AssemblyDesc>();

            var gacPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET",
                "assembly");
            
            if (!Directory.Exists(gacPath)) return allAssemblies;

            foreach (var dllPath in Directory.EnumerateFiles(gacPath, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(dllPath);
                    if (assemblyName != null)
                        allAssemblies.Add(new AssemblyDesc(assemblyName));
                }
                catch
                {
                    // ignore
                }
            }

            return allAssemblies;
        }
    }
}