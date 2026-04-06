namespace SmartWarehouse.PlatformCore.Application.Contracts;

public interface IApplicationContract
{
  string ContractName { get; }

  ContractEnvelope Envelope { get; }

  ApplicationContractKind Kind { get; }
}

public interface IApplicationCommand : IApplicationContract
{
  EnvelopeId MessageId { get; }
}

public interface IApplicationEvent : IApplicationContract
{
  EnvelopeId EventId { get; }

  DateTimeOffset OccurredAt { get; }
}

public abstract record ApplicationContract : IApplicationContract
{
  protected ApplicationContract(string contractName, ContractEnvelope envelope)
  {
    ContractName = ContractGuard.NotWhiteSpace(contractName, nameof(contractName));
    Envelope = envelope;
  }

  public string ContractName { get; }

  public ContractEnvelope Envelope { get; }

  public ApplicationContractVersion ContractVersion => Envelope.ContractVersion;

  public abstract ApplicationContractKind Kind { get; }
}

public abstract record ApplicationCommand : ApplicationContract, IApplicationCommand
{
  protected ApplicationCommand(string commandName, ContractEnvelope envelope)
      : base(commandName, envelope)
  {
  }

  public override ApplicationContractKind Kind => ApplicationContractKind.Command;

  public string CommandName => ContractName;

  public EnvelopeId MessageId => Envelope.EnvelopeId;
}

public abstract record ApplicationEvent : ApplicationContract, IApplicationEvent
{
  protected ApplicationEvent(string eventName, ContractEnvelope envelope, DateTimeOffset occurredAt)
      : base(eventName, envelope)
  {
    OccurredAt = occurredAt;
  }

  public override ApplicationContractKind Kind => ApplicationContractKind.Event;

  public string EventName => ContractName;

  public EnvelopeId EventId => Envelope.EnvelopeId;

  public ApplicationContractVersion EventVersion => ContractVersion;

  public DateTimeOffset OccurredAt { get; }
}
