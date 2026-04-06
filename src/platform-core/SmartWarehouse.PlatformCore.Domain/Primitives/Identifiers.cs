namespace SmartWarehouse.PlatformCore.Domain.Primitives;

public readonly record struct JobId
{
  public JobId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(JobId value) => value.Value;
}

public readonly record struct ExecutionTaskId
{
  public ExecutionTaskId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(ExecutionTaskId value) => value.Value;
}

public readonly record struct PayloadId
{
  public PayloadId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(PayloadId value) => value.Value;
}

public readonly record struct DeviceId
{
  public DeviceId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(DeviceId value) => value.Value;
}

public readonly record struct StationId
{
  public StationId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(StationId value) => value.Value;
}

public readonly record struct NodeId
{
  public NodeId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(NodeId value) => value.Value;
}

public readonly record struct EdgeId
{
  public EdgeId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(EdgeId value) => value.Value;
}

public readonly record struct ReservationId
{
  public ReservationId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(ReservationId value) => value.Value;
}

public readonly record struct DeviceSessionId
{
  public DeviceSessionId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(DeviceSessionId value) => value.Value;
}

public readonly record struct FaultId
{
  public FaultId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(FaultId value) => value.Value;
}

public readonly record struct CorrelationId
{
  public CorrelationId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(CorrelationId value) => value.Value;
}

public readonly record struct EndpointId
{
  public EndpointId(string value) => Value = DomainGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(EndpointId value) => value.Value;
}
