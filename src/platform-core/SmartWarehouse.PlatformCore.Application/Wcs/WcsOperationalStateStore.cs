using SmartWarehouse.PlatformCore.Domain.Devices;
using SmartWarehouse.PlatformCore.Domain.Operations;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using SmartWarehouse.PlatformCore.Domain.Stations;

namespace SmartWarehouse.PlatformCore.Application.Wcs;

public sealed class DeviceShadowState
{
  public DeviceShadowState(Device device, DateTimeOffset lastObservedAt)
  {
    Device = device ?? throw new ArgumentNullException(nameof(device));
    LastObservedAt = lastObservedAt;
  }

  public Device Device { get; }

  public DateTimeOffset LastObservedAt { get; }
}

public sealed class StationBoundaryStateSnapshot
{
  public StationBoundaryStateSnapshot(
      StationBoundary stationBoundary,
      PayloadId? currentPayloadId,
      DateTimeOffset lastUpdatedAt)
  {
    StationBoundary = stationBoundary ?? throw new ArgumentNullException(nameof(stationBoundary));
    CurrentPayloadId = currentPayloadId;
    LastUpdatedAt = lastUpdatedAt;
  }

  public StationBoundary StationBoundary { get; }

  public PayloadId? CurrentPayloadId { get; }

  public DateTimeOffset LastUpdatedAt { get; }
}

public interface IWcsOperationalStateStore
{
  Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

  Task<IReadOnlyList<DeviceShadowState>> ListDeviceShadowsAsync(CancellationToken cancellationToken = default);

  Task<DeviceShadowState?> FindDeviceShadowAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

  Task UpsertDeviceShadowAsync(DeviceShadowState deviceShadow, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<StationBoundaryStateSnapshot>> ListStationStatesAsync(CancellationToken cancellationToken = default);

  Task<StationBoundaryStateSnapshot?> FindStationStateAsync(StationId stationId, CancellationToken cancellationToken = default);

  Task UpsertStationStateAsync(StationBoundaryStateSnapshot stationState, CancellationToken cancellationToken = default);

  Task<DeviceSession?> FindDeviceSessionAsync(DeviceId deviceId, CancellationToken cancellationToken = default);

  Task UpsertDeviceSessionAsync(DeviceSession session, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<Fault>> ListFaultsBySourceAsync(FaultSourceRef source, CancellationToken cancellationToken = default);

  Task<Fault?> FindFaultAsync(FaultId faultId, CancellationToken cancellationToken = default);

  Task UpsertFaultAsync(Fault fault, CancellationToken cancellationToken = default);

  Task<IReadOnlyList<Reservation>> ListReservationsByOwnerAsync(
      ReservationOwnerRef owner,
      CancellationToken cancellationToken = default);

  Task<Reservation?> FindReservationAsync(ReservationId reservationId, CancellationToken cancellationToken = default);

  Task UpsertReservationAsync(Reservation reservation, CancellationToken cancellationToken = default);
}
