using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;
using SmartWarehouse.PlatformCore.Infrastructure.Wcs;
using System.Text.Json;

namespace SmartWarehouse.PlatformCore.IntegrationTests;

[Collection(PlatformCoreIntegrationFixtureDefinition.Name)]
public sealed class WcsExecutionTaskRuntimeIntegrationTests
{
  private readonly PlatformCoreTestcontainersHarness _harness;

  public WcsExecutionTaskRuntimeIntegrationTests(PlatformCoreTestcontainersHarness harness)
  {
    _harness = harness;
  }

  [Fact]
  public async Task SubmitPersistsRuntimeModelAndTreatsEquivalentReplayAsIdempotent()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var command = CreateSubmitCommand();

    await processor.SubmitAsync(command);
    await processor.SubmitAsync(command);

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, command.ExecutionTaskId.Value);

    Assert.Equal(command.JobId.Value, runtime.JobId);
    Assert.Equal(command.TaskRevision.Value, runtime.TaskRevision);
    Assert.Equal(ExecutionTaskType.StationTransfer, runtime.TaskType);
    Assert.Equal(ExecutionTaskState.Planned, runtime.State);
    Assert.Equal("Device", runtime.AssigneeType);
    Assert.Equal("SHUTTLE_01", runtime.AssigneeId);
    Assert.Equal("Accepted", runtime.ActiveRuntimePhase);
    Assert.Equal(command.Envelope.CorrelationId.Value, runtime.CorrelationId);
    Assert.Null(runtime.ReasonCode);
    Assert.Null(runtime.ResolutionHint);
    Assert.Null(runtime.ReplanRequired);

    using var participantDocument = JsonDocument.Parse(runtime.ParticipantRefs);
    var participant = Assert.Single(participantDocument.RootElement.EnumerateArray());
    Assert.Equal("stationBoundary", participant.GetProperty("type").GetString());
    Assert.Equal("LOAD_01", participant.GetProperty("resourceId").GetString());
  }

  [Fact]
  public async Task CancelTransitionsSubmittedRuntimeToCancelled()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var serviceProvider = CreateServiceProvider(environment.PlatformCoreConnectionString);
    await using var scope = serviceProvider.CreateAsyncScope();

    var processor = scope.ServiceProvider.GetRequiredService<IWcsExecutionTaskCommandProcessor>();
    var submitCommand = CreateSubmitCommand();

    await processor.SubmitAsync(submitCommand);
    await processor.CancelAsync(
        new CancelExecutionTask(
            new ContractEnvelope(
                new EnvelopeId("msg-cancel-01"),
                submitCommand.Envelope.CorrelationId),
            submitCommand.ExecutionTaskId,
            submitCommand.TaskRevision,
            submitCommand.JobId,
            new ReasonCode("OPERATOR_CANCELLED")));

    var runtime = await LoadRuntimeAsync(environment.PlatformCoreConnectionString, submitCommand.ExecutionTaskId.Value);

    Assert.Equal(ExecutionTaskState.Cancelled, runtime.State);
    Assert.Equal("Cancelled", runtime.ActiveRuntimePhase);
    Assert.Equal("OPERATOR_CANCELLED", runtime.ReasonCode);
    Assert.Null(runtime.ResolutionHint);
    Assert.Null(runtime.ReplanRequired);
  }

  private static SubmitExecutionTask CreateSubmitCommand() =>
      new(
          new ContractEnvelope(
              new EnvelopeId("msg-submit-01"),
              new CorrelationId("corr-task-01")),
          new ExecutionTaskId("task-runtime-01"),
          new TaskRevision(1),
          new JobId("job-01"),
          ExecutionTaskType.StationTransfer,
          ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
          [ExecutionResourceRef.ForStationBoundary(new StationId("LOAD_01"))],
          targetNode: new NodeId("L1_LOAD_01"));

  private static async Task ApplyMigrationsAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    await context.Database.MigrateAsync();
  }

  private static async Task<ExecutionTaskRuntimeRecord> LoadRuntimeAsync(string connectionString, string executionTaskId)
  {
    await using var context = CreateContext(connectionString);

    return await context.Set<ExecutionTaskRuntimeRecord>()
        .AsNoTracking()
        .SingleAsync(record => record.ExecutionTaskId == executionTaskId);
  }

  private static ServiceProvider CreateServiceProvider(string connectionString)
  {
    var services = new ServiceCollection();
    services.AddPlatformCorePersistence(connectionString);
    services.AddPersistenceWcsExecutionTaskCommandProcessing();

    return services.BuildServiceProvider(new ServiceProviderOptions
    {
      ValidateOnBuild = true,
      ValidateScopes = true
    });
  }

  private static PlatformCoreDbContext CreateContext(string connectionString)
  {
    var options = new DbContextOptionsBuilder<PlatformCoreDbContext>()
        .UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", PersistenceSchemas.Integration))
        .Options;

    return new PlatformCoreDbContext(options);
  }
}
