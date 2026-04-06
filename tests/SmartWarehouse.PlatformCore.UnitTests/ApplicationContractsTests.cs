using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class ApplicationContractsTests
{
  [Fact]
  public void ContractEnvelopeDefaultsToV0()
  {
    var envelope = new ContractEnvelope(
        new EnvelopeId("msg-01"),
        new CorrelationId("corr-01"));

    Assert.Equal(ApplicationContractVersion.V0, envelope.ContractVersion);
  }

  [Fact]
  public void SubmitExecutionTaskFromExecutionTaskCopiesCorrelationAndEnvelopeMetadata()
  {
    var task = CreateNavigateTask();

    var command = SubmitExecutionTask.FromExecutionTask(
        new EnvelopeId("msg-01"),
        new TaskRevision(1),
        task,
        CausationId.From(new EnvelopeId("job-accepted-01")));

    Assert.Equal(ApplicationContractKind.Command, command.Kind);
    Assert.Equal(new EnvelopeId("msg-01"), command.MessageId);
    Assert.Equal(task.CorrelationId, command.Envelope.CorrelationId);
    Assert.Equal(task.TaskId, command.ExecutionTaskId);
    Assert.Equal(task.TargetNode, command.TargetNode);
    Assert.Equal(nameof(SubmitExecutionTask), command.CommandName);
  }

  [Fact]
  public void SubmitExecutionTaskRejectsDuplicateParticipants()
  {
    Assert.Throws<ArgumentException>(() => new SubmitExecutionTask(
        new ContractEnvelope(new EnvelopeId("msg-01"), new CorrelationId("corr-01")),
        new ExecutionTaskId("task-01"),
        new TaskRevision(1),
        new JobId("job-01"),
        ExecutionTaskType.CarrierTransfer,
        ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
        [
            ExecutionResourceRef.ForDevice(new DeviceId("LIFT_A")),
            ExecutionResourceRef.ForDevice(new DeviceId("LIFT_A"))
        ],
        sourceNode: new NodeId("L1_TRANSFER_A"),
        targetNode: new NodeId("L2_TRANSFER_A"),
        transferMode: TransferMode.ShuttleRidesHybridLiftWithPayload));
  }

  [Fact]
  public void CanonicalPlatformEventSharesEnvelopeMetadataAndEventAliases()
  {
    var occurredAt = new DateTimeOffset(2026, 4, 6, 12, 30, 0, TimeSpan.Zero);
    var @event = new CanonicalPlatformEvent<string>(
        "ExecutionTaskStateChanged",
        new ContractEnvelope(
            new EnvelopeId("evt-01"),
            new CorrelationId("corr-01"),
            new CausationId("msg-00")),
        occurredAt,
        PlatformEventVisibility.Internal,
        "payload");

    Assert.Equal(ApplicationContractKind.Event, @event.Kind);
    Assert.Equal(new EnvelopeId("evt-01"), @event.EventId);
    Assert.Equal("ExecutionTaskStateChanged", @event.EventName);
    Assert.Equal(ApplicationContractVersion.V0, @event.EventVersion);
    Assert.Equal(occurredAt, @event.OccurredAt);
    Assert.Equal(PlatformEventVisibility.Internal, @event.Visibility);
  }

  [Fact]
  public void CancelExecutionTaskCarriesOptionalReasonCode()
  {
    var task = CreateNavigateTask();
    var reasonCode = new ReasonCode("OPERATOR_CANCELLED");

    var command = CancelExecutionTask.ForExecutionTask(
        new EnvelopeId("msg-02"),
        new TaskRevision(2),
        task,
        reasonCode);

    Assert.Equal(new EnvelopeId("msg-02"), command.MessageId);
    Assert.Equal(reasonCode, command.ReasonCode);
    Assert.Equal(task.JobId, command.JobId);
  }

  private static ExecutionTask CreateNavigateTask() =>
      new(
          new ExecutionTaskId("task-01"),
          new JobId("job-01"),
          ExecutionResourceRef.ForDevice(new DeviceId("SHUTTLE_01")),
          [],
          ExecutionTaskType.Navigate,
          ExecutionTaskState.Planned,
          new CorrelationId("corr-01"),
          targetNode: new NodeId("L1_TRAVEL_005"));
}
