using Microsoft.Extensions.Logging.Abstractions;
using SmartWarehouse.PlatformCore.DbMigrator;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class DbMigratorRunnerTests
{
  [Fact]
  public async Task RunAsyncAppliesPendingMigrations()
  {
    var migrationExecutor = new FakePlatformCoreMigrationExecutor
    {
      KnownMigrationIds = ["20260406081525_InitialPlatformCoreSchema"],
      PendingMigrationIdsBeforeApply = ["20260406081525_InitialPlatformCoreSchema"]
    };
    var runner = new DbMigratorRunner(migrationExecutor, NullLogger<DbMigratorRunner>.Instance);

    var exitCode = await runner.RunAsync(CancellationToken.None);

    Assert.Equal(DbMigratorExitCode.Success, exitCode);
    Assert.Equal(1, migrationExecutor.ApplyCallCount);
  }

  [Fact]
  public async Task RunAsyncSkipsApplyWhenDatabaseIsUpToDate()
  {
    var migrationExecutor = new FakePlatformCoreMigrationExecutor
    {
      KnownMigrationIds = ["20260406081525_InitialPlatformCoreSchema"]
    };
    var runner = new DbMigratorRunner(migrationExecutor, NullLogger<DbMigratorRunner>.Instance);

    var exitCode = await runner.RunAsync(CancellationToken.None);

    Assert.Equal(DbMigratorExitCode.Success, exitCode);
    Assert.Equal(1, migrationExecutor.ApplyCallCount);
  }

  [Fact]
  public async Task RunAsyncReturnsFailureWhenMigrationApplyFails()
  {
    var migrationExecutor = new FakePlatformCoreMigrationExecutor
    {
      KnownMigrationIds = ["20260406081525_InitialPlatformCoreSchema"],
      ApplyException = new InvalidOperationException("boom")
    };
    var runner = new DbMigratorRunner(migrationExecutor, NullLogger<DbMigratorRunner>.Instance);

    var exitCode = await runner.RunAsync(CancellationToken.None);

    Assert.Equal(DbMigratorExitCode.MigrationFailed, exitCode);
    Assert.Equal(1, migrationExecutor.ApplyCallCount);
  }

  [Fact]
  public async Task RunAsyncPropagatesCancellation()
  {
    var cancellationTokenSource = new CancellationTokenSource();
    var migrationExecutor = new FakePlatformCoreMigrationExecutor
    {
      KnownMigrationIds = ["20260406081525_InitialPlatformCoreSchema"],
      GetPendingMigrationException = new OperationCanceledException(cancellationTokenSource.Token)
    };
    var runner = new DbMigratorRunner(migrationExecutor, NullLogger<DbMigratorRunner>.Instance);

    await Assert.ThrowsAsync<OperationCanceledException>(() => runner.RunAsync(cancellationTokenSource.Token));
  }

  private sealed class FakePlatformCoreMigrationExecutor : IPlatformCoreMigrationExecutor
  {
    public IReadOnlyList<string> KnownMigrationIds { get; init; } = [];

    public IReadOnlyList<string> PendingMigrationIdsBeforeApply { get; init; } = [];

    public IReadOnlyList<string> PendingMigrationIdsAfterApply { get; init; } = [];

    public Exception? GetPendingMigrationException { get; init; }

    public Exception? ApplyException { get; init; }

    public int ApplyCallCount { get; private set; }

    public IReadOnlyList<string> GetKnownMigrationIds() => KnownMigrationIds;

    public Task<IReadOnlyList<string>> GetPendingMigrationIdsAsync(CancellationToken cancellationToken)
    {
      if (GetPendingMigrationException is not null)
      {
        return Task.FromException<IReadOnlyList<string>>(GetPendingMigrationException);
      }

      var pendingMigrationIds = ApplyCallCount == 0
          ? PendingMigrationIdsBeforeApply
          : PendingMigrationIdsAfterApply;

      return Task.FromResult(pendingMigrationIds);
    }

    public Task ApplyMigrationsAsync(CancellationToken cancellationToken)
    {
      ApplyCallCount++;

      if (ApplyException is not null)
      {
        return Task.FromException(ApplyException);
      }

      return Task.CompletedTask;
    }
  }
}
