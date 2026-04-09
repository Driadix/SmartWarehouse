using SmartWarehouse.PlatformCore.Domain;
using SmartWarehouse.PlatformCore.Domain.Primitives;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartWarehouse.PlatformCore.Application.Northbound;

public interface IPayloadTransferJobService
{
  Task<CreatePayloadTransferJobResult> CreateAsync(
      CreatePayloadTransferJobCommand command,
      CancellationToken cancellationToken = default);

  Task<PayloadTransferJobModel> GetByJobIdAsync(
      string jobId,
      CancellationToken cancellationToken = default);

  Task<PayloadTransferJobModel> GetByClientOrderIdAsync(
      string clientOrderId,
      CancellationToken cancellationToken = default);

  Task<CancelPayloadTransferJobResult> CancelAsync(
      string jobId,
      CancellationToken cancellationToken = default);
}

public sealed class CreatePayloadTransferJobCommand
{
  public CreatePayloadTransferJobCommand(
      string clientOrderId,
      EndpointId sourceEndpointId,
      EndpointId targetEndpointId,
      JobPriority priority,
      JsonElement? payloadRef = null,
      JsonElement? attributes = null)
  {
    ClientOrderId = Guard.NotWhiteSpace(clientOrderId, nameof(clientOrderId));
    SourceEndpointId = sourceEndpointId;
    TargetEndpointId = targetEndpointId;
    Priority = priority;
    PayloadRef = payloadRef;
    Attributes = attributes;
  }

  public string ClientOrderId { get; }

  public EndpointId SourceEndpointId { get; }

  public EndpointId TargetEndpointId { get; }

  public JobPriority Priority { get; }

  public JsonElement? PayloadRef { get; }

  public JsonElement? Attributes { get; }
}

public sealed class PayloadTransferJobModel
{
  public PayloadTransferJobModel(
      string jobId,
      string clientOrderId,
      string state,
      string sourceEndpointId,
      string targetEndpointId,
      string priority,
      DateTimeOffset createdAt,
      DateTimeOffset updatedAt,
      JsonElement? payloadRef = null,
      JsonElement? attributes = null,
      PayloadTransferJobReasonModel? reason = null,
      DateTimeOffset? completedAt = null)
  {
    JobId = Guard.NotWhiteSpace(jobId, nameof(jobId));
    ClientOrderId = Guard.NotWhiteSpace(clientOrderId, nameof(clientOrderId));
    State = Guard.NotWhiteSpace(state, nameof(state));
    SourceEndpointId = Guard.NotWhiteSpace(sourceEndpointId, nameof(sourceEndpointId));
    TargetEndpointId = Guard.NotWhiteSpace(targetEndpointId, nameof(targetEndpointId));
    Priority = Guard.NotWhiteSpace(priority, nameof(priority));
    CreatedAt = createdAt;
    UpdatedAt = updatedAt;
    PayloadRef = payloadRef;
    Attributes = attributes;
    Reason = reason;
    CompletedAt = completedAt;
  }

  public string JobId { get; }

  public string ClientOrderId { get; }

  public string State { get; }

  public string SourceEndpointId { get; }

  public string TargetEndpointId { get; }

  public string Priority { get; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public JsonElement? PayloadRef { get; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public JsonElement? Attributes { get; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public PayloadTransferJobReasonModel? Reason { get; }

  public DateTimeOffset CreatedAt { get; }

  public DateTimeOffset UpdatedAt { get; }

  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public DateTimeOffset? CompletedAt { get; }
}

public sealed class PayloadTransferJobReasonModel
{
  public PayloadTransferJobReasonModel(string code, string? message = null)
  {
    Code = Guard.NotWhiteSpace(code, nameof(code));
    Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
  }

  public string Code { get; }

  public string? Message { get; }
}

public sealed class CreatePayloadTransferJobResult(PayloadTransferJobModel job, bool isIdempotentReplay)
{
  public PayloadTransferJobModel Job { get; } = job ?? throw new ArgumentNullException(nameof(job));

  public bool IsIdempotentReplay { get; } = isIdempotentReplay;
}

public sealed class CancelPayloadTransferJobResult(PayloadTransferJobModel job, bool wasAlreadyCancelled)
{
  public PayloadTransferJobModel Job { get; } = job ?? throw new ArgumentNullException(nameof(job));

  public bool WasAlreadyCancelled { get; } = wasAlreadyCancelled;
}

public sealed class NorthboundProblemException : Exception
{
  public NorthboundProblemException(
      int statusCode,
      string problemCode,
      string title,
      string? detail = null)
      : base(detail ?? title)
  {
    if (statusCode < 400)
    {
      throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "Status code must represent an error.");
    }

    StatusCode = statusCode;
    ProblemCode = Guard.NotWhiteSpace(problemCode, nameof(problemCode));
    Title = Guard.NotWhiteSpace(title, nameof(title));
    Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
  }

  public int StatusCode { get; }

  public string ProblemCode { get; }

  public string Title { get; }

  public string? Detail { get; }
}

public static class PayloadTransferJobContract
{
  public static string ToExternalState(JobState state) =>
      state switch
      {
        JobState.Accepted => "ACCEPTED",
        JobState.Planned => "ACCEPTED",
        JobState.InProgress => "IN_PROGRESS",
        JobState.Suspended => "SUSPENDED",
        JobState.Completed => "COMPLETED",
        JobState.Failed => "FAILED",
        JobState.Cancelled => "CANCELLED",
        _ => throw new InvalidOperationException($"Job state '{state}' is not exposed by Northbound API v0.")
      };

  public static string ToExternalPriority(JobPriority priority) =>
      priority switch
      {
        JobPriority.Low => "LOW",
        JobPriority.Normal => "NORMAL",
        JobPriority.High => "HIGH",
        _ => throw new InvalidOperationException($"Job priority '{priority}' is not exposed by Northbound API v0.")
      };

  public static bool TryParsePriority(string? value, out JobPriority priority)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      priority = JobPriority.Normal;
      return true;
    }

    switch (value.Trim().ToUpperInvariant())
    {
      case "LOW":
        priority = JobPriority.Low;
        return true;
      case "NORMAL":
        priority = JobPriority.Normal;
        return true;
      case "HIGH":
        priority = JobPriority.High;
        return true;
      default:
        priority = default;
        return false;
    }
  }
}

internal static class Guard
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
