namespace SmartWarehouse.PlatformCore.Infrastructure;

public readonly record struct InfrastructureModuleDescriptor
{
  public InfrastructureModuleDescriptor(string name, Type markerType)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentNullException.ThrowIfNull(markerType);

    Name = name;
    MarkerType = markerType;
    RootNamespace = markerType.Namespace
        ?? throw new ArgumentException("Module marker type must belong to a namespace.", nameof(markerType));
  }

  public string Name { get; }

  public Type MarkerType { get; }

  public string RootNamespace { get; }
}
