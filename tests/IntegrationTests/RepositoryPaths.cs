namespace IntegrationTests;

/// <summary>Locates files in the repository from the test's bin folder (walks up to <c>CodeBench.slnx</c>).</summary>
internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string Problems => Path.Combine(Root, "problems");

    private static string FindRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CodeBench.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("CodeBench.slnx not found above " + AppContext.BaseDirectory);
    }
}
