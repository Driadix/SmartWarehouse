namespace SmartWarehouse.PlatformCore.Application.Contracts;

public static class PayloadTransferJobEventNames
{
  public const string JobAccepted = "JobAccepted";

  public const string JobStateChanged = "JobStateChanged";
}

public sealed record JobAcceptedPayload(
    string JobId,
    string ClientOrderId,
    string JobType,
    string SourceEndpoint,
    string TargetEndpoint,
    string State,
    string Priority);

public sealed record JobStateChangedPayload(
    string JobId,
    string PreviousState,
    string NewState,
    string? ReasonCode = null,
    string? ActiveExecutionTaskId = null);
