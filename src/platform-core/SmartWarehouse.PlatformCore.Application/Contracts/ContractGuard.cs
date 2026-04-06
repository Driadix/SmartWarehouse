using System.Collections.ObjectModel;

namespace SmartWarehouse.PlatformCore.Application.Contracts;

internal static class ContractGuard
{
  public static string NotWhiteSpace(string? value, string paramName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
    }

    return value.Trim();
  }

  public static int Positive(int value, string paramName)
  {
    if (value <= 0)
    {
      throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
    }

    return value;
  }

  public static ReadOnlyCollection<T> UniqueReadOnlyList<T>(
      IEnumerable<T> values,
      string paramName,
      bool allowEmpty = true)
  {
    ArgumentNullException.ThrowIfNull(values, paramName);

    var materialized = values.ToArray();
    if (!allowEmpty && materialized.Length == 0)
    {
      throw new ArgumentException("Collection cannot be empty.", paramName);
    }

    var seen = new HashSet<T>();
    foreach (var item in materialized)
    {
      if (item is null)
      {
        throw new ArgumentException("Collection cannot contain null items.", paramName);
      }

      if (!seen.Add(item))
      {
        throw new ArgumentException("Collection cannot contain duplicate items.", paramName);
      }
    }

    return Array.AsReadOnly(materialized);
  }
}
