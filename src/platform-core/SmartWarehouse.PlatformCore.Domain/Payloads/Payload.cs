using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Payloads;

public sealed class Payload
{
  public Payload(
      PayloadId payloadId,
      PayloadKind payloadKind,
      Dimensions dimensions,
      decimal weight,
      PayloadCustodyHolder custodyHolder)
  {
    PayloadId = payloadId;
    PayloadKind = payloadKind;
    Dimensions = dimensions;
    Weight = DomainGuard.Positive(weight, nameof(weight));
    CustodyHolder = custodyHolder;
  }

  public PayloadId PayloadId { get; }

  public PayloadKind PayloadKind { get; }

  public Dimensions Dimensions { get; }

  public decimal Weight { get; }

  public PayloadCustodyHolder CustodyHolder { get; }
}
