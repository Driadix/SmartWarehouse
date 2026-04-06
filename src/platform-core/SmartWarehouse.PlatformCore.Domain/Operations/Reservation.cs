using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Operations;

public sealed class Reservation
{
  public Reservation(
      ReservationId reservationId,
      ReservationOwnerRef owner,
      IEnumerable<NodeId> nodes,
      ReservationHorizon horizon,
      ReservationState state)
  {
    ReservationId = reservationId;
    Owner = owner;
    Nodes = DomainGuard.UniqueReadOnlyList(nodes, nameof(nodes), allowEmpty: false);
    Horizon = horizon;
    State = state;
  }

  public ReservationId ReservationId { get; }

  public ReservationOwnerRef Owner { get; }

  public IReadOnlyList<NodeId> Nodes { get; }

  public ReservationHorizon Horizon { get; }

  public ReservationState State { get; }
}
