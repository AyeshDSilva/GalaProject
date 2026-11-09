using GalaReconciler.Tests.Helpers;

namespace GalaReconciler.Tests;

public class SampleWorkbookTests
{
    /// <summary>
    /// Writes samples/Source.xlsx and samples/Processed.xlsx at the repository root for manual testing.
    /// Run on its own with: dotnet test --filter SampleWorkbookTests
    /// </summary>
    [Fact]
    public void Writes_sample_workbooks()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "samples");

        SampleWorkbookBuilder.WriteSamples(directory);

        Assert.True(File.Exists(Path.Combine(directory, "Source.xlsx")));
        Assert.True(File.Exists(Path.Combine(directory, "Processed.xlsx")));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GalaReconciler.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Couldn't find GalaReconciler.sln above the test output folder.");
    }
}
