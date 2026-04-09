using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartWarehouse.PlatformCore.Application.Northbound;
using SmartWarehouse.PlatformCore.Host.Northbound;

namespace SmartWarehouse.PlatformCore.Host.Controllers;

[ApiController]
[Route("api/v0/payload-transfer-jobs")]
public sealed class PayloadTransferJobsController(IPayloadTransferJobService payloadTransferJobService) : ControllerBase
{
  [HttpPost]
  [ProducesResponseType<PayloadTransferJobModel>(StatusCodes.Status202Accepted)]
  [ProducesResponseType<PayloadTransferJobModel>(StatusCodes.Status200OK)]
  [ProducesResponseType<NorthboundProblemResponse>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType<NorthboundProblemResponse>(StatusCodes.Status409Conflict)]
  [ProducesResponseType<NorthboundProblemResponse>(StatusCodes.Status422UnprocessableEntity)]
  public async Task<IActionResult> CreateAsync(
      [FromBody] CreatePayloadTransferJobHttpRequest request,
      CancellationToken cancellationToken)
  {
    if (!TryCreateCommand(request, out var command, out var validationProblem))
    {
      return validationProblem!;
    }

    try
    {
      var result = await payloadTransferJobService.CreateAsync(command!, cancellationToken);
      SetLocationHeader(result.Job.JobId);

      return result.IsIdempotentReplay
          ? Ok(result.Job)
          : StatusCode(StatusCodes.Status202Accepted, result.Job);
    }
    catch (NorthboundProblemException exception)
    {
      return ToProblemResult(exception);
    }
  }

  [HttpGet("{jobId}")]
  [ProducesResponseType<PayloadTransferJobModel>(StatusCodes.Status200OK)]
  [ProducesResponseType<NorthboundProblemResponse>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetByJobIdAsync(string jobId, CancellationToken cancellationToken)
  {
    try
    {
      var job = await payloadTransferJobService.GetByJobIdAsync(jobId, cancellationToken);
      return Ok(job);
    }
    catch (NorthboundProblemException exception)
    {
      return ToProblemResult(exception);
    }
  }

  [HttpGet("by-client-order/{clientOrderId}")]
  [ProducesResponseType<PayloadTransferJobModel>(StatusCodes.Status200OK)]
  [ProducesResponseType<NorthboundProblemResponse>(StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetByClientOrderIdAsync(string clientOrderId, CancellationToken cancellationToken)
  {
    try
    {
      var job = await payloadTransferJobService.GetByClientOrderIdAsync(clientOrderId, cancellationToken);
      return Ok(job);
    }
    catch (NorthboundProblemException exception)
    {
      return ToProblemResult(exception);
    }
  }

  [HttpPost("{jobId}/cancel")]
  [ProducesResponseType<PayloadTransferJobModel>(StatusCodes.Status202Accepted)]
  [ProducesResponseType<PayloadTransferJobModel>(StatusCodes.Status200OK)]
  [ProducesResponseType<NorthboundProblemResponse>(StatusCodes.Status404NotFound)]
  [ProducesResponseType<NorthboundProblemResponse>(StatusCodes.Status409Conflict)]
  public async Task<IActionResult> CancelAsync(string jobId, CancellationToken cancellationToken)
  {
    try
    {
      var result = await payloadTransferJobService.CancelAsync(jobId, cancellationToken);
      return result.WasAlreadyCancelled
          ? Ok(result.Job)
          : StatusCode(StatusCodes.Status202Accepted, result.Job);
    }
    catch (NorthboundProblemException exception)
    {
      return ToProblemResult(exception);
    }
  }

  private bool TryCreateCommand(
      CreatePayloadTransferJobHttpRequest request,
      out CreatePayloadTransferJobCommand? command,
      out IActionResult? problem)
  {
    if (!PayloadTransferJobContract.TryParsePriority(request.Priority, out var priority))
    {
      command = null;
      problem = BadRequest(CreateProblem(
          code: "INVALID_REQUEST",
          title: "Некорректный формат запроса",
          detail: "Поле `priority` должно иметь одно из значений `LOW`, `NORMAL`, `HIGH`."));
      return false;
    }

    if (!IsJsonObjectOrMissing(request.PayloadRef))
    {
      command = null;
      problem = BadRequest(CreateProblem(
          code: "INVALID_REQUEST",
          title: "Некорректный формат запроса",
          detail: "Поле `payloadRef` должно быть JSON-объектом."));
      return false;
    }

    if (!IsJsonObjectOrMissing(request.Attributes))
    {
      command = null;
      problem = BadRequest(CreateProblem(
          code: "INVALID_REQUEST",
          title: "Некорректный формат запроса",
          detail: "Поле `attributes` должно быть JSON-объектом."));
      return false;
    }

    command = new CreatePayloadTransferJobCommand(
        request.ClientOrderId,
        new SmartWarehouse.PlatformCore.Domain.Primitives.EndpointId(request.SourceEndpointId),
        new SmartWarehouse.PlatformCore.Domain.Primitives.EndpointId(request.TargetEndpointId),
        priority,
        request.PayloadRef,
        request.Attributes);
    problem = null;
    return true;
  }

  private ObjectResult ToProblemResult(NorthboundProblemException exception)
  {
    var response = CreateProblem(exception.ProblemCode, exception.Title, exception.Detail);
    return new ObjectResult(response)
    {
      StatusCode = exception.StatusCode
    };
  }

  private NorthboundProblemResponse CreateProblem(string code, string title, string? detail = null) =>
      new(code, title, detail, HttpContext.Request.Path.Value);

  private void SetLocationHeader(string jobId)
  {
    Response.Headers.Location = $"/api/v0/payload-transfer-jobs/{Uri.EscapeDataString(jobId)}";
  }

  private static bool IsJsonObjectOrMissing(JsonElement? value) =>
      value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.Object;
}

public sealed class CreatePayloadTransferJobHttpRequest
{
  [Required]
  [MinLength(1)]
  public string ClientOrderId { get; init; } = null!;

  [Required]
  [MinLength(1)]
  public string SourceEndpointId { get; init; } = null!;

  [Required]
  [MinLength(1)]
  public string TargetEndpointId { get; init; } = null!;

  public string? Priority { get; init; }

  public JsonElement? PayloadRef { get; init; }

  public JsonElement? Attributes { get; init; }
}
