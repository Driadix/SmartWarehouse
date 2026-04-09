using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Host;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence;
using SmartWarehouse.PlatformCore.Infrastructure.Persistence.Model;

namespace SmartWarehouse.PlatformCore.IntegrationTests;

[Collection(PlatformCoreIntegrationFixtureDefinition.Name)]
public sealed class NorthboundApiIntegrationTests
{
  private readonly PlatformCoreTestcontainersHarness _harness;

  public NorthboundApiIntegrationTests(PlatformCoreTestcontainersHarness harness)
  {
    _harness = harness;
  }

  [Fact]
  public async Task CreateGetRepeatAndCancelFollowNorthboundContract()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var factory = CreateFactory(environment, "warehouse-a.nominal.yaml");
    using var client = factory.CreateClient();

    var request = new
    {
      clientOrderId = "WMS-12345",
      sourceEndpointId = "inbound.main",
      targetEndpointId = "outbound.main",
      priority = "NORMAL",
      payloadRef = new
      {
        clientPayloadId = "SSCC-001"
      },
      attributes = new
      {
        batchId = "BATCH-01"
      }
    };

    using var createResponse = await client.PostAsJsonAsync("/api/v0/payload-transfer-jobs", request);
    Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

    var createdJob = await createResponse.Content.ReadFromJsonAsync<PayloadTransferJobResponse>();
    var location = AssertSingleHeaderValue(createResponse, "Location");

    Assert.NotNull(createdJob);
    Assert.StartsWith("/api/v0/payload-transfer-jobs/", location, StringComparison.Ordinal);
    Assert.Equal("WMS-12345", createdJob.ClientOrderId);
    Assert.Equal("ACCEPTED", createdJob.State);
    Assert.Equal("inbound.main", createdJob.SourceEndpointId);
    Assert.Equal("outbound.main", createdJob.TargetEndpointId);
    Assert.Equal("NORMAL", createdJob.Priority);
    Assert.NotNull(createdJob.PayloadRef);
    Assert.NotNull(createdJob.Attributes);

    var initialPlan = await LoadPlanningSnapshotAsync(environment.PlatformCoreConnectionString, createdJob.JobId);

    Assert.Equal(JobState.Planned, initialPlan.Job.State);
    Assert.Equal(
        [
            "L1_LOAD_01",
            "L1_TRAVEL_001",
            "L1_SWITCH_A",
            "L1_TP_LIFT_A",
            "L1_CARRIER_A",
            "L2_CARRIER_A",
            "L2_TP_LIFT_A",
            "L2_UNLOAD_01"
        ],
        initialPlan.RouteSegments.Select(static segment => segment.NodeId).ToArray());
    Assert.Collection(
        initialPlan.TaskPlans,
        task =>
        {
          Assert.Equal(1, task.TaskRevision);
          Assert.Equal(ExecutionTaskType.StationTransfer, task.TaskType);
          Assert.Equal(ExecutionTaskState.Planned, task.State);
          Assert.Equal("Device", task.AssigneeType);
          Assert.Equal("SHUTTLE_01", task.AssigneeId);
          Assert.Equal("L1_LOAD_01", task.TargetNodeId);
          Assert.Null(task.SourceNodeId);
          Assert.Null(task.TransferMode);
        },
        task =>
        {
          Assert.Equal(2, task.TaskRevision);
          Assert.Equal(ExecutionTaskType.Navigate, task.TaskType);
          Assert.Equal(ExecutionTaskState.Planned, task.State);
          Assert.Equal("Device", task.AssigneeType);
          Assert.Equal("SHUTTLE_01", task.AssigneeId);
          Assert.Equal("L1_TP_LIFT_A", task.TargetNodeId);
          Assert.Null(task.SourceNodeId);
          Assert.Null(task.TransferMode);
        },
        task =>
        {
          Assert.Equal(3, task.TaskRevision);
          Assert.Equal(ExecutionTaskType.CarrierTransfer, task.TaskType);
          Assert.Equal(ExecutionTaskState.Planned, task.State);
          Assert.Equal("Device", task.AssigneeType);
          Assert.Equal("SHUTTLE_01", task.AssigneeId);
          Assert.Equal("L1_TP_LIFT_A", task.SourceNodeId);
          Assert.Equal("L2_TP_LIFT_A", task.TargetNodeId);
          Assert.Equal(TransferMode.ShuttleRidesHybridLiftWithPayload, task.TransferMode);
        },
        task =>
        {
          Assert.Equal(4, task.TaskRevision);
          Assert.Equal(ExecutionTaskType.Navigate, task.TaskType);
          Assert.Equal(ExecutionTaskState.Planned, task.State);
          Assert.Equal("Device", task.AssigneeType);
          Assert.Equal("SHUTTLE_01", task.AssigneeId);
          Assert.Equal("L2_UNLOAD_01", task.TargetNodeId);
          Assert.Null(task.SourceNodeId);
          Assert.Null(task.TransferMode);
        },
        task =>
        {
          Assert.Equal(5, task.TaskRevision);
          Assert.Equal(ExecutionTaskType.StationTransfer, task.TaskType);
          Assert.Equal(ExecutionTaskState.Planned, task.State);
          Assert.Equal("Device", task.AssigneeType);
          Assert.Equal("SHUTTLE_01", task.AssigneeId);
          Assert.Equal("L2_UNLOAD_01", task.TargetNodeId);
          Assert.Null(task.SourceNodeId);
          Assert.Null(task.TransferMode);
        });
    Assert.Collection(
        initialPlan.ResourceAssignments,
        assignment =>
        {
          Assert.EndsWith("-01", assignment.ExecutionTaskId, StringComparison.Ordinal);
          Assert.Equal(1, assignment.SequenceNo);
          Assert.Equal("PARTICIPANT_01", assignment.AssignmentRole);
          Assert.Equal("StationBoundary", assignment.ResourceType);
          Assert.Equal("LOAD_01", assignment.ResourceId);
        },
        assignment =>
        {
          Assert.EndsWith("-03", assignment.ExecutionTaskId, StringComparison.Ordinal);
          Assert.Equal(1, assignment.SequenceNo);
          Assert.Equal("PARTICIPANT_01", assignment.AssignmentRole);
          Assert.Equal("Device", assignment.ResourceType);
          Assert.Equal("LIFT_A_DEVICE", assignment.ResourceId);
        },
        assignment =>
        {
          Assert.EndsWith("-05", assignment.ExecutionTaskId, StringComparison.Ordinal);
          Assert.Equal(1, assignment.SequenceNo);
          Assert.Equal("PARTICIPANT_01", assignment.AssignmentRole);
          Assert.Equal("StationBoundary", assignment.ResourceType);
          Assert.Equal("UNLOAD_01", assignment.ResourceId);
        });

    using var getByIdResponse = await client.GetAsync($"/api/v0/payload-transfer-jobs/{createdJob.JobId}");
    Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);
    var jobById = await getByIdResponse.Content.ReadFromJsonAsync<PayloadTransferJobResponse>();

    Assert.NotNull(jobById);
    Assert.Equal(createdJob.JobId, jobById.JobId);

    using var getByClientOrderResponse = await client.GetAsync("/api/v0/payload-transfer-jobs/by-client-order/WMS-12345");
    Assert.Equal(HttpStatusCode.OK, getByClientOrderResponse.StatusCode);
    var jobByClientOrderId = await getByClientOrderResponse.Content.ReadFromJsonAsync<PayloadTransferJobResponse>();

    Assert.NotNull(jobByClientOrderId);
    Assert.Equal(createdJob.JobId, jobByClientOrderId.JobId);

    using var repeatResponse = await client.PostAsJsonAsync("/api/v0/payload-transfer-jobs", request);
    Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
    var repeatedJob = await repeatResponse.Content.ReadFromJsonAsync<PayloadTransferJobResponse>();

    Assert.NotNull(repeatedJob);
    Assert.Equal(createdJob.JobId, repeatedJob.JobId);
    Assert.Equal(location, AssertSingleHeaderValue(repeatResponse, "Location"));

    var repeatedPlan = await LoadPlanningSnapshotAsync(environment.PlatformCoreConnectionString, createdJob.JobId);
    Assert.Equal(initialPlan.RouteSegments.Count, repeatedPlan.RouteSegments.Count);
    Assert.Equal(initialPlan.TaskPlans.Count, repeatedPlan.TaskPlans.Count);
    Assert.Equal(initialPlan.ResourceAssignments.Count, repeatedPlan.ResourceAssignments.Count);

    using var cancelResponse = await client.PostAsync($"/api/v0/payload-transfer-jobs/{createdJob.JobId}/cancel", content: null);
    Assert.Equal(HttpStatusCode.Accepted, cancelResponse.StatusCode);
    var cancelledJob = await cancelResponse.Content.ReadFromJsonAsync<PayloadTransferJobResponse>();

    Assert.NotNull(cancelledJob);
    Assert.Equal(createdJob.JobId, cancelledJob.JobId);
    Assert.Equal("CANCELLED", cancelledJob.State);
    Assert.NotNull(cancelledJob.CompletedAt);

    using var repeatCancelResponse = await client.PostAsync($"/api/v0/payload-transfer-jobs/{createdJob.JobId}/cancel", content: null);
    Assert.Equal(HttpStatusCode.OK, repeatCancelResponse.StatusCode);
    var repeatCancelledJob = await repeatCancelResponse.Content.ReadFromJsonAsync<PayloadTransferJobResponse>();

    Assert.NotNull(repeatCancelledJob);
    Assert.Equal("CANCELLED", repeatCancelledJob.State);
  }

  [Fact]
  public async Task CreateRejectsUnknownSourceEndpoint()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var factory = CreateFactory(environment, "warehouse-a.nominal.yaml");
    using var client = factory.CreateClient();

    using var response = await client.PostAsJsonAsync(
        "/api/v0/payload-transfer-jobs",
        new
        {
          clientOrderId = "WMS-20001",
          sourceEndpointId = "missing.source",
          targetEndpointId = "outbound.main"
        });

    await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "UNKNOWN_SOURCE_ENDPOINT");
  }

  [Fact]
  public async Task CreateRejectsUnknownTargetEndpoint()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var factory = CreateFactory(environment, "warehouse-a.nominal.yaml");
    using var client = factory.CreateClient();

    using var response = await client.PostAsJsonAsync(
        "/api/v0/payload-transfer-jobs",
        new
        {
          clientOrderId = "WMS-20002",
          sourceEndpointId = "inbound.main",
          targetEndpointId = "missing.target"
        });

    await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "UNKNOWN_TARGET_ENDPOINT");
  }

  [Fact]
  public async Task CreateRejectsIdenticalEndpoints()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var factory = CreateFactory(environment, "warehouse-a.nominal.yaml");
    using var client = factory.CreateClient();

    using var response = await client.PostAsJsonAsync(
        "/api/v0/payload-transfer-jobs",
        new
        {
          clientOrderId = "WMS-20003",
          sourceEndpointId = "inbound.main",
          targetEndpointId = "inbound.main"
        });

    await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "IDENTICAL_ENDPOINTS");
  }

  [Fact]
  public async Task CreateRejectsMissingRouteBetweenValidEndpoints()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var factory = CreateFactory(environment, "warehouse-a.no-route.yaml");
    using var client = factory.CreateClient();

    using var response = await client.PostAsJsonAsync(
        "/api/v0/payload-transfer-jobs",
        new
        {
          clientOrderId = "WMS-20004",
          sourceEndpointId = "inbound.main",
          targetEndpointId = "outbound.main"
        });

    await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "NO_ADMISSIBLE_ROUTE");
  }

  [Fact]
  public async Task GetByJobIdReturnsNotFoundWhenJobIsMissing()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);
    await using var factory = CreateFactory(environment, "warehouse-a.nominal.yaml");
    using var client = factory.CreateClient();

    using var response = await client.GetAsync("/api/v0/payload-transfer-jobs/job-missing");

    await AssertProblemAsync(response, HttpStatusCode.NotFound, "JOB_NOT_FOUND");
  }

  [Fact]
  public async Task CancelReturnsConflictForCompletedJob()
  {
    await using var environment = await _harness.CreateEnvironmentAsync();
    await ApplyMigrationsAsync(environment.PlatformCoreConnectionString);

    const string jobId = "job-completed-01";
    await SeedJobAsync(
        environment.PlatformCoreConnectionString,
        new JobRecord
        {
          JobId = jobId,
          ClientOrderId = "WMS-30001",
          JobType = JobType.PayloadTransfer,
          SourceEndpointId = "inbound.main",
          TargetEndpointId = "outbound.main",
          State = JobState.Completed,
          Priority = JobPriority.Normal,
          CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
          UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
          CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

    await using var factory = CreateFactory(environment, "warehouse-a.nominal.yaml");
    using var client = factory.CreateClient();

    using var response = await client.PostAsync($"/api/v0/payload-transfer-jobs/{jobId}/cancel", content: null);

    await AssertProblemAsync(response, HttpStatusCode.Conflict, "CANCEL_NOT_ALLOWED");
  }

  private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode statusCode, string problemCode)
  {
    Assert.Equal(statusCode, response.StatusCode);

    var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();

    Assert.NotNull(problem);
    Assert.Equal(problemCode, problem.Code);
    Assert.False(string.IsNullOrWhiteSpace(problem.Title));
  }

  private static string AssertSingleHeaderValue(HttpResponseMessage response, string headerName)
  {
    Assert.True(response.Headers.TryGetValues(headerName, out var values));
    return Assert.Single(values);
  }

  private static async Task ApplyMigrationsAsync(string connectionString)
  {
    await using var context = CreateContext(connectionString);
    await context.Database.MigrateAsync();
  }

  private static async Task SeedJobAsync(string connectionString, JobRecord jobRecord)
  {
    await using var context = CreateContext(connectionString);
    context.Add(jobRecord);
    await context.SaveChangesAsync();
  }

  private static async Task<PlanningSnapshot> LoadPlanningSnapshotAsync(string connectionString, string jobId)
  {
    await using var context = CreateContext(connectionString);

    var job = await context.Set<JobRecord>()
        .AsNoTracking()
        .SingleAsync(record => record.JobId == jobId);
    var routeSegments = await context.Set<JobRouteSegmentRecord>()
        .AsNoTracking()
        .Where(record => record.JobId == jobId)
        .OrderBy(record => record.SequenceNo)
        .ToListAsync();
    var taskPlans = await context.Set<ExecutionTaskPlanRecord>()
        .AsNoTracking()
        .Where(record => record.JobId == jobId)
        .OrderBy(record => record.TaskRevision)
        .ToListAsync();
    var taskIds = taskPlans.Select(static record => record.ExecutionTaskId).ToArray();
    var resourceAssignments = await context.Set<ResourceAssignmentRecord>()
        .AsNoTracking()
        .Where(record => taskIds.Contains(record.ExecutionTaskId))
        .OrderBy(record => record.ExecutionTaskId)
        .ThenBy(record => record.SequenceNo)
        .ToListAsync();

    return new PlanningSnapshot(job, routeSegments, taskPlans, resourceAssignments);
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

  private static PlatformCoreHostApplicationFactory CreateFactory(
      PlatformCoreIntegrationTestEnvironment environment,
      string topologyFixtureFileName)
  {
    var topologyFixturePath = Path.Combine(
        TestRepositoryRoot.Get(),
        "topologies",
        "phase1",
        topologyFixtureFileName);

    var overrides = environment.CreateConfigurationOverrides()
        .Concat(
            new[]
            {
              KeyValuePair.Create("Topology:ConfigurationFile", topologyFixturePath)
            })
        .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    return new PlatformCoreHostApplicationFactory(overrides);
  }

  private sealed class PlatformCoreHostApplicationFactory(
      IReadOnlyDictionary<string, string> configurationOverrides) : WebApplicationFactory<Program>
  {
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Development");
      builder.ConfigureAppConfiguration((_, configuration) =>
          configuration.AddInMemoryCollection(
              configurationOverrides.Select(static pair => KeyValuePair.Create(pair.Key, (string?)pair.Value))));
    }
  }

  private sealed class PayloadTransferJobResponse
  {
    public string JobId { get; set; } = null!;

    public string ClientOrderId { get; set; } = null!;

    public string State { get; set; } = null!;

    public string SourceEndpointId { get; set; } = null!;

    public string TargetEndpointId { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public JsonElement? PayloadRef { get; set; }

    public JsonElement? Attributes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
  }

  private sealed class ProblemResponse
  {
    public string Code { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Detail { get; set; }
  }

  private sealed record PlanningSnapshot(
      JobRecord Job,
      IReadOnlyList<JobRouteSegmentRecord> RouteSegments,
      IReadOnlyList<ExecutionTaskPlanRecord> TaskPlans,
      IReadOnlyList<ResourceAssignmentRecord> ResourceAssignments);
}
