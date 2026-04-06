using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Operations;

public sealed class DeviceSession
{
  public DeviceSession(
      DeviceSessionId sessionId,
      DeviceId deviceId,
      DeviceSessionState state,
      DateTimeOffset leaseUntil,
      DateTimeOffset lastHeartbeatAt)
  {
    if (lastHeartbeatAt > leaseUntil)
    {
      throw new ArgumentException("Last heartbeat cannot be later than the lease expiration.", nameof(lastHeartbeatAt));
    }

    SessionId = sessionId;
    DeviceId = deviceId;
    State = state;
    LeaseUntil = leaseUntil;
    LastHeartbeatAt = lastHeartbeatAt;
  }

  public DeviceSessionId SessionId { get; }

  public DeviceId DeviceId { get; }

  public DeviceSessionState State { get; }

  public DateTimeOffset LeaseUntil { get; }

  public DateTimeOffset LastHeartbeatAt { get; }
}
