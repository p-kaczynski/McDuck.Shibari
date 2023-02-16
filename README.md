*Note: This is shared mostly for archival reasons, I hope never to use or even think about it again.*

# McDuck.Shibari
.NET Assembly Binding Tool

## Problem

A .NET application (e.g. but not necessairly ASP.NET website) in the config file (e.g. `Web.Config`) has an ability to set some binding redirects:
```
<dependentAssembly>
  <assemblyIdentity name="someAssembly"
    publicKeyToken="32ab4ba45e0a69a1"
    culture="en-us" />
  <bindingRedirect oldVersion="7.0.0.0" newVersion="8.0.0.0" />
</dependentAssembly>
```

This is often done automatically by IDE (I know that Visual Studio offers it), and sometimes is a result of having a variety of project imports (e.g. NuGet packages) that import not identical versions of some shared dependency.

Sometimes it is enough to just redirect everything to the newest version that is imported, and quite often it all works fine.

Sometimes you need to provide two separate versions of the file (one older, one newer), if there were some breaking changes and you are unfortunate enough to require BOTH in your single application.

There are two usual symptoms:

1. `MissingMethodException` - or more generally `MissingMemberException` this **might** mean sth in your code is calling a method that is no longer part of the imported dll, and you need to provide a compatabile version

2. `Could not load file or assembly` - this **might** mean that a version of a dll requested by some code is not available (either in the `bin`, or in GAC), which means that you have to add a binding redirect to the version you have on hand and hope this doesn't cause problem #1

**TL;DR: Assembly binding redirects bad, head hurt a lot**

## Storytime

I had routinely found myself seeing Yellow Screen Of Death with "assembly not found" error, having to go to `Web.Config`, find relevant entry for the missing assembly and ensure the versions match.

I grew tired of this, especially because even if you have tested sth locally, it might still fail when deployed to a server, beause you might have had the missing assemblies in your GAC from e.g. some other/older development.

So I created a command line utility that runs on the target machine against a particular ASP.NET deployment in IIS and does its best to identify and put all the necessary bindings and redirects into the config file.

## Solution?

At this time I'm not providing compiled binaries, because if you are cursed with actually needing help of this tool, you probably can easily build it yourself, and I don't think anyone would (or should) trust random binaries from the internet ;)

So build and copy the output (with all dlls) to a folder on the machine where the app has to run. Then from command line run:

`McDuck.Shibari.exe -p C:\inetpub\wwwroot -f`

and you should end up with the bindigs section of the `Web.Config` updated to reflect what is required/possible on this machine.

As a bonus it will write out to console info about e.g. possibly missing dependencies and stuff like that. YMMV.

Below is the command line options class, or you can just run it with `--help` IIRC to see the options. The help text should explain what it does.

```
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
```

## FAQ

### It's called Shibari because...
...because it's job is "fixing bindings", yes.
