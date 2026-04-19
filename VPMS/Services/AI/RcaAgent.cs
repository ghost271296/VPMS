using Newtonsoft.Json;
using VPMS.Models;

namespace VPMS.Services.AI;

public class RcaAgent(OpenAiClientService ai)
{
    private const string SystemPrompt = """
        You are a senior UPS field engineer performing Root Cause Analysis.
        Given the approved issues and engineering features, identify root causes ranked by probability.
        Return a JSON array of root causes. Each must have:
          rank (integer, 1=most likely), title, explanation, probability (0.0-1.0),
          confidence (0.0-1.0), category (Thermal/Electrical/Component/Environmental/Operational/Human),
          supportingEvidence (list of strings), conflictingEvidence (list of strings).
        Be concise. Rank highest probability cause first.
        Return ONLY valid JSON, no markdown.
        """;

    public async Task<List<RootCause>> AnalyzeAsync(List<DiagnosticIssue> issues, FeatureSet fs,
        DataQualityReport dq, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(issues, fs, dq);
        var json = await ai.CompleteWithRetryAsync(SystemPrompt, prompt, ct: ct);

        try
        {
            json = CleanJson(json);
            var raw = JsonConvert.DeserializeObject<List<RawRca>>(json) ?? [];
            return raw.Select(r => new RootCause
            {
                Rank = r.Rank,
                Title = r.Title ?? "Unknown",
                Explanation = r.Explanation ?? string.Empty,
                Probability = Math.Clamp(r.Probability - dq.ConfidencePenalty * 0.5, 0.01, 1.0),
                Confidence = Math.Clamp(r.Confidence - dq.ConfidencePenalty, 0.05, 1.0),
                Category = r.Category ?? "Unknown",
                SupportingEvidence = r.SupportingEvidence ?? [],
                ConflictingEvidence = r.ConflictingEvidence ?? []
            }).OrderBy(rc => rc.Rank).ToList();
        }
        catch
        {
            return FallbackRca(issues);
        }
    }

    private static string BuildPrompt(List<DiagnosticIssue> issues, FeatureSet fs, DataQualityReport dq)
    {
        var issueList = string.Join("\n", issues.Where(i => i.IsApproved)
            .Select(i => $"- [{i.Severity}] {i.Title}: {i.Evidence}"));

        return $"""
            APPROVED ISSUES (Gate 1 validated):
            {issueList}

            KEY FEATURES:
            - Rbatt proxy: {fs.RbattProxy * 1000:F1} mΩ
            - Vdc ripple std: {fs.VdcStd:F3} V
            - Heatsink max: {fs.HeatsinkTempMax:F1}°C
            - Load max: {fs.LoadMax:F1}%, mean: {fs.LoadMean:F1}%
            - Freq output std: {fs.FreqOutputStd:F5} Hz
            - Alarm storm index: {fs.AlarmStormIndex:F1}
            - Fan anomaly: {fs.FanAnomalyDetected}
            - SoC min: {fs.SocMin:F1}%
            - Battery temp max: {fs.BattTempMax:F1}°C
            - Data quality score: {dq.Score}/100

            Perform root cause analysis. What are the underlying causes of the detected issues?
            """;
    }

    private static List<RootCause> FallbackRca(List<DiagnosticIssue> issues)
    {
        return issues.Where(i => i.IsApproved).Select((issue, idx) => new RootCause
        {
            Rank = idx + 1,
            Title = $"Root cause of: {issue.Title}",
            Explanation = $"The issue '{issue.Title}' in subsystem {issue.Subsystem} requires further investigation. {issue.Evidence}",
            Probability = issue.Confidence * 0.8,
            Confidence = issue.Confidence * 0.7,
            Category = MapCategory(issue.Subsystem),
            SupportingEvidence = [issue.Evidence]
        }).ToList();
    }

    private static string MapCategory(string subsystem) => subsystem switch
    {
        "Battery" => "Component",
        "DCLink" => "Component",
        "Thermal" => "Thermal",
        "Inverter" => "Electrical",
        "Fans" => "Component",
        _ => "Electrical"
    };

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }

    private record RawRca(int Rank, string? Title, string? Explanation, double Probability,
        double Confidence, string? Category, List<string>? SupportingEvidence, List<string>? ConflictingEvidence);
}
