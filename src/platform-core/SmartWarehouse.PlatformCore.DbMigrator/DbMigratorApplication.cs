using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.DbMigrator;

public static class DbMigratorApplication
{
  public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
  {
    var builder = Host.CreateApplicationBuilder(args);
    var connectionString = ResolveConnectionString(builder.Configuration);

    if (connectionString is null)
    {
      Console.Error.WriteLine("Connection string 'PlatformCore' is required.");
      return (int)DbMigratorExitCode.MissingConnectionString;
    }

    builder.Services.AddPlatformCorePersistence(connectionString);
    builder.Services.AddScoped<IPlatformCoreMigrationExecutor, EfCorePlatformCoreMigrationExecutor>();
    builder.Services.AddScoped<DbMigratorRunner>();

    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    var runner = scope.ServiceProvider.GetRequiredService<DbMigratorRunner>();

    return (int)await runner.RunAsync(cancellationToken);
  }

  internal static string? ResolveConnectionString(IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var connectionString = configuration.GetConnectionString("PlatformCore")
        ?? configuration["PlatformCore:ConnectionString"];

    return string.IsNullOrWhiteSpace(connectionString)
        ? null
        : connectionString;
  }
}
