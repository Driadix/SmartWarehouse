using SmartWarehouse.PlatformCore.Domain.Primitives;

namespace SmartWarehouse.PlatformCore.Domain.Operations;

public sealed class CapabilitySet
{
  public CapabilitySet(IEnumerable<CapabilityId> staticCapabilities, IEnumerable<CapabilityId> activeCapabilities)
  {
    StaticCapabilities = DomainGuard.UniqueReadOnlyList(staticCapabilities, nameof(staticCapabilities));
    ActiveCapabilities = DomainGuard.UniqueReadOnlyList(activeCapabilities, nameof(activeCapabilities));

    var staticSet = StaticCapabilities.ToHashSet();
    var missingCapabilities = ActiveCapabilities.Where(capability => !staticSet.Contains(capability)).ToArray();
    if (missingCapabilities.Length > 0)
    {
      var missingCapabilityList = string.Join(", ", missingCapabilities.Select(capability => capability.Value));
      throw new ArgumentException(
          $"Active capabilities must be a subset of static capabilities. Missing: {missingCapabilityList}.",
          nameof(activeCapabilities));
    }
  }

  public IReadOnlyList<CapabilityId> StaticCapabilities { get; }

  public IReadOnlyList<CapabilityId> ActiveCapabilities { get; }
}
