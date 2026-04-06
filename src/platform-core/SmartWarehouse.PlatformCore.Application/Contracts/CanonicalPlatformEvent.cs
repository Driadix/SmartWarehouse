namespace SmartWarehouse.PlatformCore.Application.Contracts;

public sealed record CanonicalPlatformEvent<TPayload> : ApplicationEvent
{
  public CanonicalPlatformEvent(
      string eventName,
      ContractEnvelope envelope,
      DateTimeOffset occurredAt,
      PlatformEventVisibility visibility,
      TPayload payload)
      : base(eventName, envelope, occurredAt)
  {
    ArgumentNullException.ThrowIfNull(payload);

    Visibility = visibility;
    Payload = payload;
  }

  public PlatformEventVisibility Visibility { get; }

  public TPayload Payload { get; }
}
