namespace SmartWarehouse.PlatformCore.Application.Topology;

public sealed class TopologyConfigurationException : Exception
{
  public TopologyConfigurationException(string message)
      : base(message)
  {
  }

  public TopologyConfigurationException(string message, Exception innerException)
      : base(message, innerException)
  {
  }
}
