using System.Runtime.InteropServices;
using GalaReconciler.Core;
using Microsoft.Extensions.DependencyInjection;

namespace GalaReconciler.Cli;

internal static partial class Program
{
    private static int Main(string[] args)
    {
        var options = CliOptions.Parse(args);

        using var services = new ServiceCollection()
            .AddComparison()
            .Configure<ComparisonOptions>(o => o.StrictNotAttending = options.StrictNotAttending)
            .BuildServiceProvider();
        using var scope = services.CreateScope();

        var exitCode = new CliApp(scope.ServiceProvider.GetRequiredService<CompareFilesHandler>(), Console.Out)
            .Run(options, Environment.CurrentDirectory);

        // Started by double-clicking the exe: keep the window open so the result can be read.
        if (!options.NoPause && OwnsConsoleWindow())
        {
            Console.WriteLine();
            Console.Write("Press any key to close...");
            Console.ReadKey(intercept: true);
        }

        return exitCode;
    }

    /// <summary>
    /// True when no other process (such as a command prompt) shares this console, which means Windows opened
    /// the window just for us and will close it as soon as we exit.
    /// </summary>
    private static bool OwnsConsoleWindow()
    {
        if (!OperatingSystem.IsWindows() || Console.IsInputRedirected)
        {
            return false;
        }

        try
        {
            var processes = new uint[2];
            return GetConsoleProcessList(processes, (uint)processes.Length) == 1;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial uint GetConsoleProcessList([Out] uint[] processList, uint processCount);
}
