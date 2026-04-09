using System.Text.Json.Serialization;

namespace SmartWarehouse.PlatformCore.Host.Northbound;

public sealed class NorthboundProblemResponse
{
  public NorthboundProblemResponse(
      string code,
      string title,
      string? detail = null,
      string? instance = null)
  {
    Code = Guard.NotWhiteSpace(code, nameof(code));
    Title = Guard.NotWhiteSpace(title, nameof(title));
    Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
    Instance = string.IsNullOrWhiteSpace(instance) ? null : instance.Trim();
  }

  public string Code { get; }

  public string Title { get; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? Detail { get; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? Instance { get; }

  private static class Guard
  {
    public static string NotWhiteSpace(string? value, string paramName)
    {
      if (string.IsNullOrWhiteSpace(value))
      {
        throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
      }

      return value.Trim();
    }
  }
}
