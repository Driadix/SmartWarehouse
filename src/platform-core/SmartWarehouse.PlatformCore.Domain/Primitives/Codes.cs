namespace SmartWarehouse.PlatformCore.Domain.Primitives;

public readonly record struct PayloadKind
{
  public PayloadKind(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(PayloadKind value) => value.Value;
}

public readonly record struct CapabilityId
{
  public CapabilityId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(CapabilityId value) => value.Value;
}

public readonly record struct ReasonCode
{
  public ReasonCode(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(ReasonCode value) => value.Value;
}

public readonly record struct RuntimePhase
{
  public RuntimePhase(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(RuntimePhase value) => value.Value;
}

public readonly record struct FaultCode
{
  public FaultCode(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(FaultCode value) => value.Value;
}

public readonly record struct FaultSeverity
{
  public FaultSeverity(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(FaultSeverity value) => value.Value;
}

public readonly record struct DeviceHealthState
{
  public DeviceHealthState(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(DeviceHealthState value) => value.Value;
}

public readonly record struct DeviceSessionState
{
  public DeviceSessionState(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(DeviceSessionState value) => value.Value;
}

public readonly record struct ReservationState
{
  public ReservationState(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(ReservationState value) => value.Value;
}
