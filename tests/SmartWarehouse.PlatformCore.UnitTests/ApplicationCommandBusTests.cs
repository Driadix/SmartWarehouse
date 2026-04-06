using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Contracts;
using SmartWarehouse.PlatformCore.Application.Dispatching;
using SmartWarehouse.PlatformCore.Application.Wcs;
using SmartWarehouse.PlatformCore.Application.Wes;
using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Execution;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.UnitTests;

public sealed class ApplicationCommandBusTests
{
  [Fact]
  public async Task WesGatewayDeliversSubmitExecutionTaskToWcsProcessor()
  {
    var serviceProvider = CreateServiceProvider(services =>
    {
      services.AddWesExecutionTaskCommandGateway();
      services.AddWcsExecutionTaskCommandProcessing<RecordingWcsExecutionTaskCommandProcessor>();
    });

    await using var scope = serviceProvider.CreateAsyncScope();
    var gateway = scope.ServiceProvider.GetRequiredService<IWesExecutionTaskCommandGateway>();
    var processor = scope.ServiceProvider.GetRequiredService<RecordingWcsExecutionTaskCommandProcessor>();
    var task = CreateNavigateTask();

    await gateway.SubmitAsync(
        task,
        new TaskRevision(3),
        new EnvelopeId("msg-submit-01"),
        new CausationId("job-accepted-01"));

    var command = Assert.Single(processor.SubmittedCommands);
    Assert.Equal(new EnvelopeId("msg-submit-01"), command.MessageId);
    Assert.Equal(new TaskRevision(3), command.TaskRevision);
    Assert.Equal(task.TaskId, command.ExecutionTaskId);
    Assert.Equal(task.CorrelationId, command.Envelope.CorrelationId);
    Assert.Equal(task.TargetNode, command.TargetNode);
  }

  [Fact]
  public async Task WesGatewayDeliversCancelExecutionTaskToWcsProcessor()
  {
    var serviceProvider = CreateServiceProvider(services =>
    {
      services.AddWesExecutionTaskCommandGateway();
      services.AddWcsExecutionTaskCommandProcessing<RecordingWcsExecutionTaskCommandProcessor>();
    });

    await using var scope = serviceProvider.CreateAsyncScope();
    var gateway = scope.ServiceProvider.GetRequiredService<IWesExecutionTaskCommandGateway>();
    var processor = scope.ServiceProvider.GetRequiredService<RecordingWcsExecutionTaskCommandProcessor>();
    var task = CreateNavigateTask();
    var reasonCode = new ReasonCode("OPERATOR_CANCELLED");

    await gateway.CancelAsync(
        task,
        new TaskRevision(4),
        new EnvelopeId("msg-cancel-01"),
        reasonCode);

    var command = Assert.Single(processor.CancelledCommands);
    Assert.Equal(new EnvelopeId("msg-cancel-01"), command.MessageId);
    Assert.Equal(new TaskRevision(4), command.TaskRevision);
    Assert.Equal(reasonCode, command.ReasonCode);
    Assert.Equal(task.JobId, command.JobId);
  }

  [Fact]
  public async Task CommandBusFailsWhenNoHandlerIsRegistered()
  {
    var serviceProvider = CreateServiceProvider(services => services.AddInProcessApplicationCommandBus());

    await using var scope = serviceProvider.CreateAsyncScope();
    var commandBus = scope.ServiceProvider.GetRequiredService<IApplicationCommandBus>();
    var command = new TestCommand(new ContractEnvelope(new EnvelopeId("msg-01"), new CorrelationId("corr-01")));

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        await commandBus.SendAsync(command));

    Assert.Contains("No application command handler is registered", exception.Message);
  }

  [Fact]
  public async Task CommandBusFailsWhenMultipleHandlersAreRegisteredForSameCommand()
  {
    var serviceProvider = CreateServiceProvider(services =>
    {
      services.AddInProcessApplicationCommandBus();
      services.AddApplicationCommandHandler<TestCommand, FirstTestCommandHandler>();
      services.AddApplicationCommandHandler<TestCommand, SecondTestCommandHandler>();
    });

    await using var scope = serviceProvider.CreateAsyncScope();
    var commandBus = scope.ServiceProvider.GetRequiredService<IApplicationCommandBus>();
    var command = new TestCommand(new ContractEnvelope(new EnvelopeId("msg-02"), new CorrelationId("corr-02")));

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        await commandBus.SendAsync(command));

    Assert.Contains("Exactly one application command handler must be registered", exception.Message);
  }

  private static ServiceProvider CreateServiceProvider(Action<IServiceCollection> configureServices)
  {
    var services = new ServiceCollection();
    configureServices(services);

    return services.BuildServiceProvider(new ServiceProviderOptions
    {
      ValidateOnBuild = true,
      ValidateScopes = true
    });
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

  private sealed record TestCommand : ApplicationCommand
  {
    public TestCommand(ContractEnvelope envelope)
        : base(nameof(TestCommand), envelope)
    {
    }
  }

  private sealed class FirstTestCommandHandler : IApplicationCommandHandler<TestCommand>
  {
    public ValueTask HandleAsync(TestCommand command, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
  }

  private sealed class SecondTestCommandHandler : IApplicationCommandHandler<TestCommand>
  {
    public ValueTask HandleAsync(TestCommand command, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
  }

  private sealed class RecordingWcsExecutionTaskCommandProcessor : IWcsExecutionTaskCommandProcessor
  {
    private readonly List<SubmitExecutionTask> submittedCommands = [];
    private readonly List<CancelExecutionTask> cancelledCommands = [];

    public IReadOnlyList<SubmitExecutionTask> SubmittedCommands => submittedCommands;

    public IReadOnlyList<CancelExecutionTask> CancelledCommands => cancelledCommands;

    public ValueTask SubmitAsync(SubmitExecutionTask command, CancellationToken cancellationToken = default)
    {
      submittedCommands.Add(command);
      return ValueTask.CompletedTask;
    }

    public ValueTask CancelAsync(CancelExecutionTask command, CancellationToken cancellationToken = default)
    {
      cancelledCommands.Add(command);
      return ValueTask.CompletedTask;
    }
  }
}
