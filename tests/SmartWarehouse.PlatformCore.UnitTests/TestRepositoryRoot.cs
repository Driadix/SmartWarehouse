namespace SmartWarehouse.PlatformCore.UnitTests;

internal static class TestRepositoryRoot
{
  private static readonly Lazy<string> RepositoryRoot = new(ResolveRepositoryRoot);

  public static string Get() => RepositoryRoot.Value;

  private static string ResolveRepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SmartWarehouse.sln")))
      {
        return directory.FullName;
      }

      directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root was not found.");
  }
}
