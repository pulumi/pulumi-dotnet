namespace Pulumi.Automation;

public sealed class ImportResult
{
    public string StandardOutput { get; init; }
    public string StandardError { get; init; }
    public string? GeneratedCode { get; set; }
    public UpdateSummary Summary { get; init; }

    public ImportResult(
        string standardOutput,
        string standardError,
        UpdateSummary summary,
        string? generatedCode = null)
    {
        StandardOutput = standardOutput;
        StandardError = standardError;
        GeneratedCode = generatedCode;
        Summary = summary;
    }
}
