using System.Collections.ObjectModel;

namespace SmartWarehouse.PlatformCore.Domain.Primitives;

internal static class DomainGuard
{
  public static string NotWhiteSpace(string? value, string paramName)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
    }

    return value.Trim();
  }

  public static decimal Positive(decimal value, string paramName)
  {
    if (value <= 0)
    {
      throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
    }

    return value;
  }

  public static int Positive(int value, string paramName)
  {
    if (value <= 0)
    {
      throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
    }

    return value;
  }

  public static int NonNegative(int value, string paramName)
  {
    if (value < 0)
    {
      throw new ArgumentOutOfRangeException(paramName, value, "Value cannot be negative.");
    }

    return value;
  }

  public static T NotNull<T>(T? value, string paramName)
      where T : class
  {
    ArgumentNullException.ThrowIfNull(value, paramName);
    return value;
  }

  public static ReadOnlyCollection<T> ReadOnlyList<T>(
      IEnumerable<T> values,
      string paramName,
      bool allowEmpty = true)
  {
    var materialized = Materialize(values, paramName, allowEmpty);
    return Array.AsReadOnly(materialized);
  }

  public static ReadOnlyCollection<T> UniqueReadOnlyList<T>(
      IEnumerable<T> values,
      string paramName,
      bool allowEmpty = true)
  {
    var materialized = Materialize(values, paramName, allowEmpty);
    var seen = new HashSet<T>();

    foreach (var item in materialized)
    {
      if (!seen.Add(item))
      {
        throw new ArgumentException("Collection cannot contain duplicate items.", paramName);
      }
    }

    return Array.AsReadOnly(materialized);
  }

  private static T[] Materialize<T>(IEnumerable<T> values, string paramName, bool allowEmpty)
  {
    ArgumentNullException.ThrowIfNull(values, paramName);

    var materialized = values.ToArray();
    if (!allowEmpty && materialized.Length == 0)
    {
      throw new ArgumentException("Collection cannot be empty.", paramName);
    }

    foreach (var item in materialized)
    {
      if (item is null)
      {
        throw new ArgumentException("Collection cannot contain null items.", paramName);
      }
    }

    return materialized;
  }
}
