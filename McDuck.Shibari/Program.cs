using System;
using CommandLine;

namespace McDuck.Shibari
{
    public static class Program
    {

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args)
                .MapResult(
                    options => BindingsProcessor.Execute(options),
                    _ => 1);
        }
    }
}
