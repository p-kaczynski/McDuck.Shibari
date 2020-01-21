using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace McDuck.Shibari
{
    public static class BindingsProcessor
    {
        private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger(nameof(BindingsProcessor));

        public static int Execute(Options options)
        {
            try
            {
                // 1. Run base checks on the options to avoid repeated tests
                CheckOptions(options);

                // 2. Load the assemblies that should be present in GAC on this system, for the framework declared by the user
                var (gacAssemblies, gacAssembliesByName) = GetGacAssemblies();

                // 3. Perform the dll scan and obtain the data about assemblies present and required
                var (_, availableWhere, required, requiredBy, availableVersions) = ScanForAssemblies(options, gacAssemblies);

                // 4. Process data
                // (Removed)

                // 5. Find assemblies which require bindings
                var requiredMappings = required
                    .GroupBy(a => a.Name)
                    .Where(g => availableVersions.ContainsKey(g.Key))
                    .Select(g => new { Name = g.Key, g.First().PublicKeyToken, Required = g.Select(x => x.Version).ToArray(), Available = availableVersions[g.Key].Select(x => x).ToArray() })
                    .Where(o => !string.IsNullOrWhiteSpace(o.PublicKeyToken))
                    .Where(o => !(o.Required.Length == 1 && o.Available.Length == 1 && Version.Equals(o.Required.Single(), o.Available.Single())));

                // 6. Scan for missing assemblies
                foreach (var miss in FindMissing(required, availableVersions, gacAssembliesByName, requiredBy))
                {
                    Log.LogWarning(miss);
                    Console.WriteLine(miss);
                }

                // 7. Final tasks
                // 7.1 If nothing else to do, exit
                if (!options.GenerateBindings && !options.ReplaceBindings)
                    return 0; //  job's done

                // 7.2 If we do something with bindings, let's generate them
                var binPath = GetBinDirPath(options);
                var result = new XElement(Constants.AssemblyBindingNamespace + Constants.AssemblyBindingElementName,
                    requiredMappings
                        .OrderBy(entry => entry.Name)
                        .Select(entry => CreateDependentAssembly(entry.Name, entry.PublicKeyToken, entry.Required, entry.Available, availableWhere, binPath))
                        .Where(x => x != null)
                        .Select(d => d.ToXElement(Constants.AssemblyBindingNamespace))
                );

                // 7.2.1 Output the bindings
                if (options.GenerateBindings)
                {
                    Console.WriteLine("Generating bindings:");
                    if (!string.IsNullOrWhiteSpace(options.OutputFileName))
                    {
                        var outputPath = Path.GetFullPath(options.OutputFileName);
                        File.WriteAllText(outputPath, result.ToString());
                    }
                    else
                        Console.WriteLine(result.ToString());
                }
                // 7.2.2 Replace the bindings in the config file
                else if (options.ReplaceBindings)
                {
                    ReplaceBindings(options, result);
                }

                return 0;

            }
            catch (Exception exception)
            {
                Log.LogError(exception,exception.Message);       
                Console.Error.WriteLine(exception.Message);
                Console.Error.WriteLine(exception.ToString());
                return 1;
            }

        }

        private static IEnumerable<string> FindMissing(HashSet<AssemblyDesc> required, Dictionary<string, Version[]> availableVersions, IDictionary<string, Dictionary<Version, AssemblyDesc>> gacAssembliesByName, Dictionary<string, List<AssemblyDesc>> requiredBy)
        {
            return required.GroupBy(a => a.Name)
                .Where(g => !availableVersions.ContainsKey(g.Key))
                .Where(g => !gacAssembliesByName.ContainsKey(g.Key))
                .Select(g => $"WARN: Missing '{g.Key}', required versions: [{string.Join(", ", g.Select(a => $"({a.Version})"))}], required by: [{string.Join(", ", requiredBy.TryGetValue(g.Key, out var r) ? r.Select(x => x.Name) : new[] { "unknown" })}]");
        }


        private static void CheckOptions(Options options)
        {
            if(!Directory.Exists(options.PathToDir))
                throw new DirectoryNotFoundException($"Cannot find the target application directory: '{options.PathToDir}'");
        }

        private static string GetConfigFilePath(Options options)
        {
            if (!string.IsNullOrWhiteSpace(options.ConfigFileName))
            {
                var path = Path.Combine(options.PathToDir, options.ConfigFileName);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Specified config file not found: '{path}'");
                
                return path;
            }

            // Web.Config?
            var p = Path.Combine(options.PathToDir, Constants.WebConfigFileName);
            if (File.Exists(p))
                return p;

            // Must be an app.config, which is renamed to dll's name
            var binDirPath = GetBinDirPath(options);

            if (!string.IsNullOrWhiteSpace(options.EntryAssemblyDll))
            {
                var entryAssemblyAppConfigPath =
                    Path.Combine(
                        binDirPath,
                        Path.ChangeExtension(options.EntryAssemblyDll, Constants.ConfigExtension)
                    );

                if (File.Exists(entryAssemblyAppConfigPath))
                    return entryAssemblyAppConfigPath;
                // else
                throw new FileNotFoundException($"Expected config file not found: '{entryAssemblyAppConfigPath}'");
            }
            // else

            var configFiles = Directory.GetFiles(binDirPath, $"*.{Constants.ConfigExtension}")
                .Where(configPath => File.Exists(Path.ChangeExtension(configPath, Constants.ConfigExtension))
                                     || File.Exists(Path.ChangeExtension(configPath, Constants.ExeExtension)))
                .ToArray();

            if (configFiles.Length == 1)
                return configFiles.Single();
            // else
            throw new ArgumentException("Too many possible candidates for a config file. Please specify config file explicitly.");
        }

        private static string GetBinDirPath(Options options)
        {
            var path = Path.Combine(options.PathToDir, options.BinDir);
            if (!Directory.Exists(path))
                path = options.PathToDir;

            if (!string.IsNullOrWhiteSpace(options.EntryAssemblyDll))
            {

                var entryPath = Path.Combine(path, options.EntryAssemblyDll);
                if (!File.Exists(entryPath))
                    throw new FileNotFoundException($"Cannot find specified entry assembly file: '{entryPath}'");
            }
            
            return path;
        }

        private static (ISet<AssemblyDesc> gacAssemblies,IDictionary<string,Dictionary<Version, AssemblyDesc>> gacAssembliesByName) GetGacAssemblies()
        {
            var gacAssemblies = GacInfo.LoadKnownAssemblies();

            var gacAssembliesByName = gacAssemblies
                .GroupBy(ad=>ad.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, 
                    g => g.ToDictionary(ad=>ad.Version, ad=>ad),
                    StringComparer.OrdinalIgnoreCase);

            return (gacAssemblies, gacAssembliesByName);
        }

        private static (HashSet<AssemblyDesc> available, Dictionary<string, Dictionary<Version, string>> availableWhere,
            HashSet<AssemblyDesc> required, Dictionary<string, List<AssemblyDesc>> requiredBy,
            Dictionary<string, Version[]> availableVersions) ScanForAssemblies(Options options,
                ISet<AssemblyDesc> gacAssemblies)
        {
            var available = new HashSet<AssemblyDesc>();
            var availableWhere = new Dictionary<string, Dictionary<Version, string>>(StringComparer.OrdinalIgnoreCase);

            var required = new HashSet<AssemblyDesc>();
            var requiredBy = new Dictionary<string, List<AssemblyDesc>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (path, assembly) in LoadAssemblies(options))
            {
                var desc = new AssemblyDesc(assembly);
                available.Add(desc);
                if (!availableWhere.ContainsKey(desc.Name))
                    availableWhere.Add(desc.Name, new Dictionary<Version, string>());

                availableWhere[desc.Name][desc.Version] = path;

                foreach (var req in assembly.GetReferencedAssemblies())
                {
                    var reqDesc = new AssemblyDesc(req);

                    if (gacAssemblies.Contains(reqDesc))
                        continue; // No point even caring about it

                    required.Add(reqDesc);
                    if (!requiredBy.ContainsKey(reqDesc.Name))
                        requiredBy.Add(reqDesc.Name, new List<AssemblyDesc>());

                    requiredBy[reqDesc.Name].Add(desc);
                }
            }

            var availableVersions = available.Concat(gacAssemblies)
                .GroupBy(a => a.Name, a => a.Version)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

            return (available, availableWhere, required, requiredBy, availableVersions);

        }

        private static IEnumerable<(string path, Assembly assembly)> LoadAssemblies(Options options)
        {
            return Directory
                .EnumerateFiles(GetBinDirPath(options), "*.dll", SearchOption.AllDirectories)
                .Select(path =>
                {
                    try
                    {
                        return (path, assembly: Assembly.LoadFrom(path));
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"INFO: Cannot load assembly from dll: '{path}'");
                        Log.LogTrace(exception, $"Cannot load assembly from dll: '{path}'. This might be normal.");
                        return (null, null);
                    }
                })
                .Where(x => x.path != null && x.assembly != null);
        }

        private static void ReplaceBindings(Options options, XElement bindingsElement)
        {
            Console.WriteLine("Preparing to fix the bindings");
            var configFilePath = GetConfigFilePath(options);
            
            var doc = XDocument.Load(configFilePath);
            var runtime = doc.Root.Element("runtime");
            if (runtime == null)
            {
                runtime = new XElement("runtime");
                doc.Root.Add(runtime);
            }

            var existing = runtime.Element(Constants.AssemblyBindingNamespace + Constants.AssemblyBindingElementName);
            if(existing != null)
                existing.ReplaceWith(bindingsElement);
            else
                runtime.Add(bindingsElement);

            if(options.Backup)
                File.Copy(configFilePath,Path.ChangeExtension(configFilePath,  "config.bak"),true);

            Console.WriteLine($"Replacing bindings in file: '{configFilePath}'");
            doc.Save(configFilePath);
            Console.WriteLine($"Saved file: '{configFilePath}'");
        }

        private static class Constants
        {
            public const string WebConfigFileName = "Web.config";
            public const string ConfigExtension = "config";
            public const string ExeExtension = "exe";

            public const string AssemblyBindingElementName = "assemblyBinding";
            public static readonly XNamespace AssemblyBindingNamespace = "urn:schemas-microsoft-com:asm.v1";
        }

        private static DependentAssembly CreateDependentAssembly(string name, string publicKeyToken, Version[] required, Version[] available, Dictionary<string, Dictionary<Version, string>> availableWhere, string basePath)
        {
            if (required.Max() > available.Max())
            {
                var message =
                    $"WARN: {name}: Required ({required.Max()}) is higher than highest available ({available.Max()})";
                Console.WriteLine(message);
                Log.LogWarning(message);
            }

            if (!availableWhere.ContainsKey(name))
            {
                var message =
                    $"INFO: {name}: The assembly is only present in the Global Assembly Cache. Will not create a binding.";
                Console.WriteLine(message);
                Log.LogInformation(message);
                return null;
            }

            var dependent = new DependentAssembly
            {
                Name = name,
                PublicKeyToken = publicKeyToken,
                Redirects = CreateBindingRedirects(required, available).ToHashSet()
            };

            dependent.CodeBases = CreateCodeBases(name, dependent.Redirects.Select(r => r.New).ToArray(),
                    availableWhere[name], basePath).ToHashSet();

            return dependent;

        }

        private static IEnumerable<BindingRedirect> CreateBindingRedirects(IEnumerable<Version> required, IReadOnlyCollection<Version> available)
        {
            if (available.Count == 1)
            {
                yield return new BindingRedirect { Min = Version.Parse("0.0.0.0"), Max = required.Max(), New = available.Single() };
            }
            else
            {

                var min = Version.Parse("0.0.0.0");

                using var avaE = available.OrderBy(x => x).GetEnumerator();
                using var reqE = required.OrderBy(x => x).GetEnumerator();

                avaE.MoveNext(); // at least one is present
                var neu = avaE.Current;

                reqE.MoveNext(); // at least one is present
                var max = reqE.Current;
                do
                {
                    while (reqE.Current > avaE.Current && avaE.MoveNext())
                        neu = avaE.Current;

                    while (reqE.MoveNext() && reqE.Current <= avaE.Current)
                        max = reqE.Current;

                    // Fact: max <= avaE.Current
                    // Fact: reqE.MoveNext() == false || reqE.Current > avaE.Current

                    if (avaE.MoveNext())
                    {
                        // we have another one, so we can now emit, but only
                        yield return new BindingRedirect { Min = min, Max = max, New = neu };

                        // Set next available
                        neu = avaE.Current;
                        // Do we have any more required?
                        if (reqE.Current > neu || reqE.MoveNext())
                        {
                            // yes, just set and loop.
                            min = reqE.Current;
                            max = reqE.Current;
                            continue;
                        }
                        // else

                        // no! so just in theory we probably can emit the final as the latest version available
                        min = avaE.Current;
                        while (avaE.MoveNext()) { }
                        yield return new BindingRedirect { Min = min, Max = avaE.Current, New = avaE.Current };
                        yield break;
                    }
                    // else

                    // this is the last available. We have to emit from current min, to the max required
                    while (reqE.MoveNext())
                    {
                    }

                    yield return new BindingRedirect { Min = min, Max = reqE.Current, New = neu };
                    yield break;
                } while (true);
            }

        }

        private static IEnumerable<CodeBase> CreateCodeBases(string name, Version[] versions, Dictionary<Version, string> availableWhere, string basePath)
        {
            if (versions.Length < 2) yield break;

            foreach (var version in versions)
            {
                if (!availableWhere.ContainsKey(version))
                {
                    var message =
                        $"INFO: {name}@{version}: The assembly version is only present in the Global Assembly Cache. Will not create a binding.";
                    Console.WriteLine(message);
                    Log.LogInformation(message);
                    continue;
                }
                yield return new CodeBase
                    {Version = version, RelativePath = GetRelativePath(basePath, availableWhere[version])};
            }
        }

        private static string GetRelativePath(string fromPath, string toPath)
        {
            try
            {
#if NETFRAMEWORK
            if (String.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
	        if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");


            if (!Path.IsPathRooted(fromPath))
                fromPath = Path.GetFullPath(fromPath);
            if (!Path.IsPathRooted(toPath))
                toPath = Path.GetFullPath(toPath);

	        Uri fromUri = new Uri(fromPath);
	        Uri toUri = new Uri(toPath);

	        if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

	        Uri relativeUri = fromUri.MakeRelativeUri(toUri);
	        String relativePath = Uri.UnescapeDataString(relativeUri.ToString());

	        if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
	        {
		        relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
	        }

	        return relativePath;
#else
                return Path.GetRelativePath(fromPath, toPath);
#endif
            }
            catch
            {
                Console.WriteLine($"{nameof(GetRelativePath)}({fromPath}, {toPath}): Error!");
                throw;
            }
        }
    }
}