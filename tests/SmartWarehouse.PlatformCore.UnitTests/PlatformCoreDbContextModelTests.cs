using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class PlatformCoreDbContextModelTests
{
  [Fact]
  public void PlatformCoreDbContextMapsAllRequiredSchemas()
  {
    using var context = CreateContext();

    var schemas = context.Model
        .GetEntityTypes()
        .Select(entityType => entityType.GetSchema())
        .OfType<string>()
        .Distinct()
        .OrderBy(value => value)
        .ToArray();

    Assert.Equal(PersistenceSchemas.All.OrderBy(value => value), schemas);
  }

  [Theory]
  [InlineData("topology_versions", PersistenceSchemas.Config)]
  [InlineData("jobs", PersistenceSchemas.Wes)]
  [InlineData("execution_task_runtime", PersistenceSchemas.Wcs)]
  [InlineData("outbox_messages", PersistenceSchemas.Integration)]
  [InlineData("payload_transfer_jobs", PersistenceSchemas.Projection)]
  [InlineData("platform_event_journal", PersistenceSchemas.Audit)]
  public void PlatformCoreDbContextMapsRepresentativeTablesToExpectedSchemas(string tableName, string schema)
  {
    using var context = CreateContext();

    Assert.Contains(
        context.Model.GetEntityTypes(),
        entityType => entityType.GetTableName() == tableName && entityType.GetSchema() == schema);
  }

  [Fact]
  public void AddPlatformCorePersistenceRegistersDbContext()
  {
    var services = new ServiceCollection();
    services.AddPlatformCorePersistence("Host=localhost;Port=5432;Database=smartwarehouse;Username=smartwarehouse;Password=smartwarehouse");

    using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
    {
      ValidateOnBuild = true,
      ValidateScopes = true
    });
    using var scope = serviceProvider.CreateScope();

    var context = scope.ServiceProvider.GetRequiredService<PlatformCoreDbContext>();

    Assert.NotNull(context);
  }

  [Fact]
  public void PlatformCoreDbContextExposesInitialMigration()
  {
    using var context = CreateContext();

    Assert.Contains(
        context.Database.GetMigrations(),
        migrationId => migrationId.EndsWith("_InitialPlatformCoreSchema", StringComparison.Ordinal));
  }

  private static PlatformCoreDbContext CreateContext()
  {
    var options = new DbContextOptionsBuilder<PlatformCoreDbContext>()
        .UseNpgsql("Host=localhost;Port=5432;Database=smartwarehouse;Username=smartwarehouse;Password=smartwarehouse")
        .Options;

    return new PlatformCoreDbContext(options);
  }
}
