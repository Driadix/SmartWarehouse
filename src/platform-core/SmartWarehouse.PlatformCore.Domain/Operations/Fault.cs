using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Operations;

public sealed class Fault
{
  public Fault(
      FaultId faultId,
      FaultSourceRef source,
      FaultCode faultCode,
      FaultSeverity severity,
      FaultState state)
  {
    FaultId = faultId;
    Source = source;
    FaultCode = faultCode;
    Severity = severity;
    State = state;
  }

  public FaultId FaultId { get; }

  public FaultSourceRef Source { get; }

  public FaultCode FaultCode { get; }

  public FaultSeverity Severity { get; }

  public FaultState State { get; }
}
