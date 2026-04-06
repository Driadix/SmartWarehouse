using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Application.Contracts;

public readonly record struct ContractEnvelope
{
  public ContractEnvelope(
      EnvelopeId envelopeId,
      CorrelationId correlationId,
      CausationId? causationId = null,
      ApplicationContractVersion? contractVersion = null)
  {
    EnvelopeId = envelopeId;
    CorrelationId = correlationId;
    CausationId = causationId;
    ContractVersion = contractVersion ?? ApplicationContractVersion.V0;
  }

  public EnvelopeId EnvelopeId { get; }

  public CorrelationId CorrelationId { get; }

  public CausationId? CausationId { get; }

  public ApplicationContractVersion ContractVersion { get; }
}
