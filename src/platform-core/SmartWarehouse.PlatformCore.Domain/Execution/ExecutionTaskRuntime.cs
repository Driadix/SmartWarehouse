using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Execution;

public sealed class ExecutionTaskRuntime
{
  public ExecutionTaskRuntime(
      ExecutionTask task,
      int taskRevision,
      RuntimePhase activeRuntimePhase,
      ReasonCode? reasonCode = null,
      ExecutionResolutionHint? resolutionHint = null,
      bool? replanRequired = null)
  {
    ArgumentNullException.ThrowIfNull(task);

    if (taskRevision <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(taskRevision), taskRevision, "Task revision must be positive.");
    }

    switch (task.State)
    {
      case ExecutionTaskState.Suspended:
      case ExecutionTaskState.Failed:
        ArgumentNullException.ThrowIfNull(reasonCode);
        if (resolutionHint is null)
        {
          throw new ArgumentNullException(nameof(resolutionHint));
        }

        if (replanRequired is null)
        {
          throw new ArgumentNullException(nameof(replanRequired));
        }

        break;
      case ExecutionTaskState.Cancelled:
        if (resolutionHint is not null)
        {
          throw new ArgumentException("Cancelled task cannot define resolution hint.", nameof(resolutionHint));
        }

        if (replanRequired is not null)
        {
          throw new ArgumentException("Cancelled task cannot define replan flag.", nameof(replanRequired));
        }

        break;
      default:
        if (reasonCode is not null)
        {
          throw new ArgumentException(
              $"Execution task state '{task.State}' cannot define reason code.",
              nameof(reasonCode));
        }

        if (resolutionHint is not null)
        {
          throw new ArgumentException(
              $"Execution task state '{task.State}' cannot define resolution hint.",
              nameof(resolutionHint));
        }

        if (replanRequired is not null)
        {
          throw new ArgumentException(
              $"Execution task state '{task.State}' cannot define replan flag.",
              nameof(replanRequired));
        }

        break;
    }

    Task = task;
    TaskRevision = taskRevision;
    ActiveRuntimePhase = activeRuntimePhase;
    ReasonCode = reasonCode;
    ResolutionHint = resolutionHint;
    ReplanRequired = replanRequired;
  }

  public ExecutionTask Task { get; }

  public int TaskRevision { get; }

  public RuntimePhase ActiveRuntimePhase { get; }

  public ReasonCode? ReasonCode { get; }

  public ExecutionResolutionHint? ResolutionHint { get; }

  public bool? ReplanRequired { get; }

  public bool IsTerminal => Task.State is ExecutionTaskState.Completed or ExecutionTaskState.Failed or ExecutionTaskState.Cancelled;

  public static ExecutionTaskRuntime CreateSubmitted(ExecutionTask task, int taskRevision) =>
      task.State == ExecutionTaskState.Planned
          ? new ExecutionTaskRuntime(task, taskRevision, new RuntimePhase("Accepted"))
          : throw new ArgumentException("Submitted execution task must start in Planned state.", nameof(task));

  public ExecutionTaskRuntime ConfirmInProgress(RuntimePhase runtimePhase)
  {
    EnsureTransitionAllowed(ExecutionTaskState.InProgress, [ExecutionTaskState.Planned, ExecutionTaskState.Suspended]);
    return new ExecutionTaskRuntime(Task.WithState(ExecutionTaskState.InProgress), TaskRevision, runtimePhase);
  }

  public ExecutionTaskRuntime Complete(RuntimePhase runtimePhase)
  {
    EnsureTransitionAllowed(ExecutionTaskState.Completed, [ExecutionTaskState.InProgress, ExecutionTaskState.Suspended]);
    return new ExecutionTaskRuntime(Task.WithState(ExecutionTaskState.Completed), TaskRevision, runtimePhase);
  }

  public ExecutionTaskRuntime Suspend(
      RuntimePhase runtimePhase,
      ReasonCode reasonCode,
      ExecutionResolutionHint resolutionHint,
      bool replanRequired)
  {
    EnsureTransitionAllowed(ExecutionTaskState.Suspended, [ExecutionTaskState.Planned, ExecutionTaskState.InProgress]);
    return new ExecutionTaskRuntime(
        Task.WithState(ExecutionTaskState.Suspended),
        TaskRevision,
        runtimePhase,
        reasonCode,
        resolutionHint,
        replanRequired);
  }

  public ExecutionTaskRuntime Fail(
      RuntimePhase runtimePhase,
      ReasonCode reasonCode,
      ExecutionResolutionHint resolutionHint,
      bool replanRequired)
  {
    EnsureTransitionAllowed(ExecutionTaskState.Failed, [ExecutionTaskState.Planned, ExecutionTaskState.InProgress, ExecutionTaskState.Suspended]);
    return new ExecutionTaskRuntime(
        Task.WithState(ExecutionTaskState.Failed),
        TaskRevision,
        runtimePhase,
        reasonCode,
        resolutionHint,
        replanRequired);
  }

  public ExecutionTaskRuntime Cancel(RuntimePhase runtimePhase, ReasonCode? reasonCode = null)
  {
    EnsureTransitionAllowed(ExecutionTaskState.Cancelled, [ExecutionTaskState.Planned, ExecutionTaskState.InProgress, ExecutionTaskState.Suspended]);
    return new ExecutionTaskRuntime(
        Task.WithState(ExecutionTaskState.Cancelled),
        TaskRevision,
        runtimePhase,
        reasonCode);
  }

  private void EnsureTransitionAllowed(ExecutionTaskState targetState, IReadOnlyCollection<ExecutionTaskState> allowedStates)
  {
    if (allowedStates.Contains(Task.State))
    {
      return;
    }

    throw new InvalidOperationException(
        $"Execution task '{Task.TaskId}' cannot transition from '{Task.State}' to '{targetState}'.");
  }
}
