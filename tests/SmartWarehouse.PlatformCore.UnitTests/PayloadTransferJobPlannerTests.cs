using SmartWarehouse.PlatformCore.Application.Topology;
using SmartWarehouse.PlatformCore.Application.Wes;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class PayloadTransferJobPlannerTests
{
  private readonly YamlWarehouseTopologyConfigLoader _loader = new();
  private readonly WarehouseTopologyCompiler _compiler = new(new WarehouseTopologyConfigValidator());

  [Fact]
  public void PlannerBuildsCanonicalExecutionTaskSequenceForNominalPayloadTransfer()
  {
    var topology = CompileFixture("warehouse-a.nominal.yaml");
    var planner = new PayloadTransferJobPlanner(topology, new WarehouseRouteService());

    var job = planner.Plan(
        new JobId("job-01"),
        new EndpointId("inbound.main"),
        new EndpointId("outbound.main"),
        JobPriority.Normal);

    Assert.Equal(JobState.Planned, job.State);
    Assert.NotNull(job.PlannedRoute);
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
        job.PlannedRoute!.NodePath.Select(static nodeId => nodeId.Value).ToArray());

    Assert.Collection(
        job.ExecutionTasks,
        task =>
        {
          Assert.Equal(ExecutionTaskType.StationTransfer, task.TaskType);
          Assert.Equal("SHUTTLE_01", task.Assignee.ResourceId);
          Assert.Equal("LOAD_01", Assert.Single(task.ParticipantRefs).ResourceId);
          Assert.Equal("L1_LOAD_01", task.TargetNode?.Value);
        },
        task =>
        {
          Assert.Equal(ExecutionTaskType.Navigate, task.TaskType);
          Assert.Equal("SHUTTLE_01", task.Assignee.ResourceId);
          Assert.Empty(task.ParticipantRefs);
          Assert.Equal("L1_TP_LIFT_A", task.TargetNode?.Value);
        },
        task =>
        {
          Assert.Equal(ExecutionTaskType.CarrierTransfer, task.TaskType);
          Assert.Equal("SHUTTLE_01", task.Assignee.ResourceId);
          Assert.Equal("LIFT_A_DEVICE", Assert.Single(task.ParticipantRefs).ResourceId);
          Assert.Equal("L1_TP_LIFT_A", task.SourceNode?.Value);
          Assert.Equal("L2_TP_LIFT_A", task.TargetNode?.Value);
          Assert.Equal(TransferMode.ShuttleRidesHybridLiftWithPayload, task.TransferMode);
        },
        task =>
        {
          Assert.Equal(ExecutionTaskType.Navigate, task.TaskType);
          Assert.Equal("SHUTTLE_01", task.Assignee.ResourceId);
          Assert.Empty(task.ParticipantRefs);
          Assert.Equal("L2_UNLOAD_01", task.TargetNode?.Value);
        },
        task =>
        {
          Assert.Equal(ExecutionTaskType.StationTransfer, task.TaskType);
          Assert.Equal("SHUTTLE_01", task.Assignee.ResourceId);
          Assert.Equal("UNLOAD_01", Assert.Single(task.ParticipantRefs).ResourceId);
          Assert.Equal("L2_UNLOAD_01", task.TargetNode?.Value);
        });
  }

  private CompiledWarehouseTopology CompileFixture(string fileName) =>
      _compiler.Compile(_loader.LoadFromFile(GetTopologyFixturePath(fileName)));

  private static string GetTopologyFixturePath(string fileName) =>
      Path.Combine(TestRepositoryRoot.Get(), "topologies", "phase1", fileName);
}
