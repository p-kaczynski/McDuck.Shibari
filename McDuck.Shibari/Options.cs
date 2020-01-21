using CommandLine;

namespace McDuck.Shibari
{
    public class Options
    {
        [Option('p', "path", Required = true, HelpText = "Path to the folder with the built application")]
        public string PathToDir { get; set; }
        
        [Option('e', "entry", Required = false, HelpText = "Specify to start resolving dependencies from a specific dll, instead of scanning entire target directory")]
        public string EntryAssemblyDll { get; set; }

        [Option("bindir", Required = false, Default = "bin", HelpText = "Specify to override the default 'bin' folder as a location for application binaries")]
        public string BinDir { get; set; }

        [Option("configfile", Required = false, HelpText = "Specify to use particular file as the config containing the bindings. Omit to automatically find app.config or web.config")]
        public string ConfigFileName { get; set; }

        // === Generate ===
        [Option('g', "generate", Required = false, SetName = "generate", HelpText = "Generate new bindings to output or specified file")]
        public bool GenerateBindings { get; set; }

        [Option('o', "output", Required = false, SetName = "generate", HelpText = "Specify to set the output file for generated bindings. Omit to generate to standard output. Used  with -g option")]
        public string OutputFileName { get; set; }

        // === Fix/Replace ===
        [Option('f', "fix", Required = false, SetName = "fix", HelpText = "Fix (replace) the bindings in the config file")]
        public bool ReplaceBindings { get; set; }

        [Option('b', "backup", Required = false, Default = false, SetName = "fix", HelpText = "Backup the config file before replacing bindings. Used with -f option")]
        public bool Backup { get; set; }
    }
}