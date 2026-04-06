namespace SmartWarehouse.PlatformCore.Infrastructure.Messaging;

public static class MessagingModule
{
  public static InfrastructureModuleDescriptor Descriptor { get; } = new("Messaging", typeof(MessagingModule));
}
