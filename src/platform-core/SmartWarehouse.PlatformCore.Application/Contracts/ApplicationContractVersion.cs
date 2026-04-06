namespace SmartWarehouse.PlatformCore.Application.Contracts;

public readonly record struct ApplicationContractVersion
{
  public static ApplicationContractVersion V0 { get; } = new("v0");

  public ApplicationContractVersion(string value) => Value = ContractGuard.NotWhiteSpace(value, nameof(value));

  public string Value { get; }

  public override string ToString() => Value;

  public static implicit operator string(ApplicationContractVersion value) => value.Value;
}
