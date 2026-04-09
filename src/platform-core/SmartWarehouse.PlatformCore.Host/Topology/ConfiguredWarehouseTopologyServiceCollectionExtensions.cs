using Microsoft.Extensions.DependencyInjection;
using SmartWarehouse.PlatformCore.Application.Topology;

namespace SmartWarehouse.PlatformCore.Host.Topology;

public static class ConfiguredWarehouseTopologyServiceCollectionExtensions
{
  public static IServiceCollection AddConfiguredWarehouseTopology(
      this IServiceCollection services,
      IConfiguration configuration,
      IHostEnvironment environment)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentNullException.ThrowIfNull(environment);

    services.AddWarehouseTopologyServices();
    services.AddSingleton(serviceProvider =>
    {
      var configuredPath = configuration["Topology:ConfigurationFile"];
      if (string.IsNullOrWhiteSpace(configuredPath))
      {
        throw new InvalidOperationException("Configuration value 'Topology:ConfigurationFile' is required.");
      }

      var topologyFilePath = ResolveTopologyFilePath(
          configuredPath,
          environment.ContentRootPath,
          AppContext.BaseDirectory);

      var loader = serviceProvider.GetRequiredService<IWarehouseTopologyConfigLoader>();
      var compiler = serviceProvider.GetRequiredService<IWarehouseTopologyCompiler>();

      return compiler.Compile(loader.LoadFromFile(topologyFilePath));
    });

    return services;
  }

  private static string ResolveTopologyFilePath(
      string configuredPath,
      string contentRootPath,
      string baseDirectory)
  {
    if (Path.IsPathRooted(configuredPath))
    {
      var absolutePath = Path.GetFullPath(configuredPath);
      return File.Exists(absolutePath)
          ? absolutePath
          : throw new FileNotFoundException($"Topology configuration file was not found at '{absolutePath}'.", absolutePath);
    }

    var candidates = new[]
    {
      Path.Combine(contentRootPath, configuredPath),
      Path.Combine(baseDirectory, configuredPath)
    };

    foreach (var candidate in candidates.Select(Path.GetFullPath))
    {
      if (File.Exists(candidate))
      {
        return candidate;
      }
    }

    throw new FileNotFoundException(
        $"Topology configuration file '{configuredPath}' was not found under content root '{contentRootPath}' or base directory '{baseDirectory}'.",
        configuredPath);
  }
}
