using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class ExecutionTaskRuntimeTests
{
  [Fact]
  public void CreateSubmittedStartsInPlannedAcceptedPhase()
  {
    var runtime = ExecutionTaskRuntime.CreateSubmitted(CreateStationTransferTask(), taskRevision: 2);

    Assert.Equal(ExecutionTaskState.Planned, runtime.Task.State);
    Assert.Equal(new RuntimePhase("Accepted"), runtime.ActiveRuntimePhase);
    Assert.Equal(2, runtime.TaskRevision);
    Assert.Null(runtime.ReasonCode);
    Assert.Null(runtime.ResolutionHint);
    Assert.Null(runtime.ReplanRequired);
  }

  [Fact]
  public void RuntimeAllowsSuspendResumeAndCompleteFlow()
  {
    var submitted = ExecutionTaskRuntime.CreateSubmitted(CreateNavigateTask(), taskRevision: 1);

    var inProgress = submitted.ConfirmInProgress(new RuntimePhase("InMotion"));
    var suspended = inProgress.Suspend(
        new RuntimePhase("ReconciliationPending"),
        new ReasonCode("DEVICE_SESSION_LOST"),
        ExecutionResolutionHint.WaitAndRetry,
        replanRequired: false);
    var resumed = suspended.ConfirmInProgress(new RuntimePhase("InMotion"));
    var completed = resumed.Complete(new RuntimePhase("Completed"));

    Assert.Equal(ExecutionTaskState.InProgress, inProgress.Task.State);
    Assert.Equal(ExecutionTaskState.Suspended, suspended.Task.State);
    Assert.Equal(new ReasonCode("DEVICE_SESSION_LOST"), suspended.ReasonCode);
    Assert.Equal(ExecutionResolutionHint.WaitAndRetry, suspended.ResolutionHint);
    Assert.False(suspended.ReplanRequired);
    Assert.Equal(ExecutionTaskState.InProgress, resumed.Task.State);
    Assert.Equal(ExecutionTaskState.Completed, completed.Task.State);
    Assert.Equal(new RuntimePhase("Completed"), completed.ActiveRuntimePhase);
    Assert.Null(completed.ReasonCode);
  }

  [Fact]
  public void SuspendedAndFailedRequireResolutionMetadata()
  {
    var task = CreateNavigateTask().WithState(ExecutionTaskState.Suspended);

    Assert.Throws<ArgumentNullException>(() => new ExecutionTaskRuntime(
        task,
        taskRevision: 1,
        new RuntimePhase("ReconciliationPending"),
        new ReasonCode("DEVICE_SESSION_LOST")));
    Assert.Throws<ArgumentNullException>(() => new ExecutionTaskRuntime(
        task,
        taskRevision: 1,
        new RuntimePhase("ReconciliationPending"),
        new ReasonCode("DEVICE_SESSION_LOST"),
        ExecutionResolutionHint.WaitAndRetry));
  }

  [Fact]
  public void RuntimeRejectsInvalidTransitions()
  {
    var submitted = ExecutionTaskRuntime.CreateSubmitted(CreateNavigateTask(), taskRevision: 1);
    var cancelled = submitted.Cancel(new RuntimePhase("Cancelled"), new ReasonCode("OPERATOR_CANCELLED"));

    Assert.Throws<InvalidOperationException>(() => submitted.Complete(new RuntimePhase("Completed")));
    Assert.Throws<InvalidOperationException>(() => cancelled.ConfirmInProgress(new RuntimePhase("InMotion")));
  }

  private static ExecutionTask CreateNavigateTask() =>
      new(
          new ExecutionTaskId("task-nav-01"),
          new JobId("job-01"),
          ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
          [],
          ExecutionTaskType.Navigate,
          ExecutionTaskState.Planned,
          new CorrelationId("corr-nav-01"),
          targetNode: new NodeId("L1_TRAVEL_005"));

  private static ExecutionTask CreateStationTransferTask() =>
      new(
          new ExecutionTaskId("task-station-01"),
          new JobId("job-01"),
          ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
          [ExecutionResourceRef.ForStationBoundary(new StationId("LOAD_01"))],
          ExecutionTaskType.StationTransfer,
          ExecutionTaskState.Planned,
          new CorrelationId("corr-station-01"),
          targetNode: new NodeId("L1_LOAD_01"));
}
