namespace SmartWarehouse.PlatformCore.Application.Contracts;

public readonly record struct EnvelopeId
{
  public EnvelopeId(string value) => Value = ContractGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(EnvelopeId value) => value.Value;
}

public readonly record struct CausationId
{
  public CausationId(string value) => Value = ContractGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(CausationId value) => value.Value;

  public static CausationId From(EnvelopeId envelopeId) => new(envelopeId.Value);
}
