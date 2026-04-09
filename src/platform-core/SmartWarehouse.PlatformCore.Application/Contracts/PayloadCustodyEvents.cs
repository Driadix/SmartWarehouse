namespace SmartWarehouse.PlatformCore.Application.Contracts;

public static class PayloadCustodyEventNames
{
  public const string PayloadCustodyChanged = "PayloadCustodyChanged";
}

public sealed record PayloadCustodyChangedPayload(
    string PayloadId,
    string PreviousHolderType,
    string PreviousHolderId,
    string NewHolderType,
    string NewHolderId);
