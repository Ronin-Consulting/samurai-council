namespace SamurAICouncil.Eval.Support;

/// <summary>Resolves corpus/report locations robustly regardless of the working directory.</summary>
public static class Paths
{
    private const string ProjectFile = "SamurAICouncil.Eval.csproj";

    /// <summary>Corpus dir: prefer the copy shipped next to the binary, else the source tree.</summary>
    public static string CorpusDir()
    {
        var beside = Path.Combine(AppContext.BaseDirectory, "corpus");
        if (Directory.Exists(beside))
        {
            return beside;
        }
        var proj = ProjectDir();
        return proj is not null ? Path.Combine(proj, "corpus") : beside;
    }

    /// <summary>Default reports root (under the project when found, else cwd).</summary>
    public static string ReportsRoot()
    {
        var proj = ProjectDir();
        return proj is not null ? Path.Combine(proj, "reports") : Path.Combine(Directory.GetCurrentDirectory(), "reports");
    }

    /// <summary>Walk up from the binary to the source project directory (for `dotnet run`).</summary>
    public static string? ProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ProjectFile)))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
