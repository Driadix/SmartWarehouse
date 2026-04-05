namespace SmartWarehouse.PlatformCore.ContractTests;

public sealed class NorthboundAcceptanceContractTests
{
  [Fact]
  public void CreatePayloadTransferJobOperationCoversFirstAcceptanceScenarios()
  {
    var contractText = ReadContractText();
    var createOperation = GetSection(
        contractText,
        "/api/v0/payload-transfer-jobs:",
        "/api/v0/payload-transfer-jobs/{jobId}:");

    Assert.Contains("post:", createOperation);
    Assert.Contains("operationId: createPayloadTransferJob", createOperation);
    Assert.Contains("requestBody:", createOperation);
    Assert.Contains("'202':", createOperation);
    Assert.Contains("'200':", createOperation);
    Assert.Contains("'409':", createOperation);
    Assert.Contains("'422':", createOperation);
    Assert.Contains("Location:", createOperation);
    Assert.Contains("$ref: '#/components/schemas/PayloadTransferJob'", createOperation);
    Assert.Contains("accepted:", createOperation);
    Assert.Contains("idempotentRepeat:", createOperation);

    var webhooksSection = GetSection(contractText, "webhooks:", "components:");
    var jobAcceptedWebhook = GetSection(webhooksSection, "jobAccepted:", "jobStateChanged:");

    Assert.Contains("operationId: receiveJobAcceptedWebhook", jobAcceptedWebhook);
    Assert.Contains("'200':", jobAcceptedWebhook);
    Assert.Contains("'204':", jobAcceptedWebhook);
  }

  [Fact]
  public void ProblemExamplesCoverFirstNegativeAcceptanceScenarios()
  {
    var contractText = ReadContractText();

    var problemSchema = GetSection(contractText, "    Problem:", "    JobAcceptedWebhookEvent:");
    Assert.Contains("- code", problemSchema);
    Assert.Contains("- title", problemSchema);

    var conflictResponse = GetSection(contractText, "    Conflict:", "    UnprocessableEntity:");
    Assert.Contains("idempotencyConflict:", conflictResponse);
    Assert.Contains("code: IDEMPOTENCY_CONFLICT", conflictResponse);

    var unprocessableEntityResponse = GetSection(contractText, "    UnprocessableEntity:", "  schemas:");
    Assert.Contains("code: UNKNOWN_SOURCE_ENDPOINT", unprocessableEntityResponse);
    Assert.Contains("code: UNKNOWN_TARGET_ENDPOINT", unprocessableEntityResponse);
    Assert.Contains("code: IDENTICAL_ENDPOINTS", unprocessableEntityResponse);
    Assert.Contains("code: NO_ADMISSIBLE_ROUTE", unprocessableEntityResponse);
  }

  private static string ReadContractText()
  {
    var contractPath = Path.Combine(FindRepositoryRoot(), "docs", "api", "northbound", "openapi-v0.yaml");
    return File.ReadAllText(contractPath);
  }

  private static string FindRepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SmartWarehouse.slnx")))
      {
        return directory.FullName;
      }

      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Repository root with SmartWarehouse.slnx was not found.");
  }

  private static string GetSection(string text, string startMarker, string endMarker)
  {
    var startIndex = text.IndexOf(startMarker, StringComparison.Ordinal);
    Assert.True(startIndex >= 0, $"Section start '{startMarker}' is missing.");

    var endIndex = text.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.Ordinal);
    Assert.True(endIndex >= 0, $"Section end '{endMarker}' is missing.");

    return text[startIndex..endIndex];
  }
}
