namespace SmartWarehouse.PlatformCore.Application.Contracts;

public static class TransferEventNames
{
  public const string TransferCommitted = "TransferCommitted";
}

public sealed record TransferParticipantPayload(
    string ParticipantType,
    string ParticipantId);

public sealed record TransferCommittedPayload(
    string ExecutionTaskId,
    string TransferMode,
    string? TransferPointId,
    IReadOnlyList<TransferParticipantPayload> Participants);
