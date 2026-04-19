using Newtonsoft.Json;
using VPMS.Models;

namespace VPMS.Services.AI;

public class PredictiveAgent(OpenAiClientService ai)
{
    private const string SystemPrompt = """
        You are a UPS predictive maintenance engineer.
        Based on the feature data and identified issues, estimate failure risks per subsystem.
        Return a JSON object with:
          subsystems (array), overallTrend (string), recommendation (string).
        Each subsystem entry: subsystem (Inverter/Battery/PFC/Thermal/DCLink/Fans),
          risk30Day (0.0-1.0), risk60Day (0.0-1.0), risk90Day (0.0-1.0),
          remainingLifeEstimate (string, e.g. "3-6 months" or "Unknown"),
          trendDescription (1 sentence), keyDrivers (list of strings, max 3).
        Risks must be increasing: risk30 ≤ risk60 ≤ risk90.
        Return ONLY valid JSON, no markdown.
        """;

    public async Task<PredictiveRiskReport> PredictAsync(FeatureSet fs, HealthIndex health,
        List<DiagnosticIssue> issues, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(fs, health, issues);
        var json = await ai.CompleteWithRetryAsync(SystemPrompt, prompt, ct: ct);

        try
        {
            json = CleanJson(json);
            var raw = JsonConvert.DeserializeObject<RawPrediction>(json);
            if (raw is null) return FallbackPrediction(fs, health);

            return new PredictiveRiskReport
            {
                OverallTrend = raw.OverallTrend ?? "Uncertain",
                Recommendation = raw.Recommendation ?? "Monitor closely and schedule maintenance.",
                Subsystems = raw.Subsystems?.Select(s => new SubsystemRisk
                {
                    Subsystem = s.Subsystem ?? "Unknown",
                    Risk30Day = Math.Clamp(s.Risk30Day, 0, 1),
                    Risk60Day = Math.Clamp(Math.Max(s.Risk60Day, s.Risk30Day), 0, 1),
                    Risk90Day = Math.Clamp(Math.Max(s.Risk90Day, s.Risk60Day), 0, 1),
                    RemainingLifeEstimate = s.RemainingLifeEstimate ?? "Unknown",
                    TrendDescription = s.TrendDescription ?? string.Empty,
                    KeyDrivers = s.KeyDrivers ?? []
                }).ToList() ?? []
            };
        }
        catch
        {
            return FallbackPrediction(fs, health);
        }
    }

    private static string BuildPrompt(FeatureSet fs, HealthIndex health, List<DiagnosticIssue> issues)
    {
        var issueList = string.Join("\n", issues.Where(i => i.IsApproved)
            .Select(i => $"- [{i.Severity}] {i.Title} (confidence: {i.Confidence:P0})"));

        return $"""
            HEALTH SCORES:
            - Battery: {health.Battery.Score:F1}/100 ({health.Battery.Status})
            - DC Link: {health.DcLinkCapacitor.Score:F1}/100 ({health.DcLinkCapacitor.Status})
            - Power Stage: {health.PowerStage.Score:F1}/100 ({health.PowerStage.Status})
            - Thermal: {health.Thermal.Score:F1}/100 ({health.Thermal.Status})
            - Overall: {health.OverallScore:F1}/100 ({health.OverallStatus})

            KEY DEGRADATION INDICATORS:
            - Rbatt proxy: {fs.RbattProxy * 1000:F1} mΩ
            - Vdc ripple: {fs.VdcStd:F3} V
            - Heatsink max: {fs.HeatsinkTempMax:F1}°C, headroom: {fs.ThermalHeadroom:F1}°C
            - Battery SoC min: {fs.SocMin:F1}%
            - Alarm storm index: {fs.AlarmStormIndex:F1}
            - Fan anomaly: {fs.FanAnomalyDetected}

            DETECTED ISSUES:
            {issueList}

            Estimate 30/60/90-day failure risks per subsystem based on degradation trends.
            """;
    }

    private static PredictiveRiskReport FallbackPrediction(FeatureSet fs, HealthIndex health)
    {
        double battRisk30 = Math.Min((100 - health.Battery.Score) / 100.0 * 0.5, 0.95);
        double dcRisk30 = Math.Min((100 - health.DcLinkCapacitor.Score) / 100.0 * 0.5, 0.95);
        double psRisk30 = Math.Min((100 - health.PowerStage.Score) / 100.0 * 0.5, 0.95);
        double thermRisk30 = Math.Min((100 - health.Thermal.Score) / 100.0 * 0.5, 0.95);

        return new PredictiveRiskReport
        {
            OverallTrend = health.OverallScore < 60 ? "Degrading" : health.OverallScore < 80 ? "Stable with concerns" : "Stable",
            Recommendation = "Schedule preventive maintenance based on the flagged subsystem risks.",
            Subsystems =
            [
                new SubsystemRisk { Subsystem = "Battery", Risk30Day = battRisk30, Risk60Day = Math.Min(battRisk30 * 1.3, 0.95), Risk90Day = Math.Min(battRisk30 * 1.6, 0.95), RemainingLifeEstimate = health.Battery.Score < 50 ? "< 6 months" : "6-18 months", TrendDescription = health.Battery.Status, KeyDrivers = ["Internal resistance", "SoC decline", "Cycle aging"] },
                new SubsystemRisk { Subsystem = "DC Link", Risk30Day = dcRisk30, Risk60Day = Math.Min(dcRisk30 * 1.2, 0.95), Risk90Day = Math.Min(dcRisk30 * 1.4, 0.95), RemainingLifeEstimate = health.DcLinkCapacitor.Score < 50 ? "< 3 months" : "1-2 years", TrendDescription = health.DcLinkCapacitor.Status, KeyDrivers = ["Ripple voltage", "Capacitor aging"] },
                new SubsystemRisk { Subsystem = "Power Stage", Risk30Day = psRisk30, Risk60Day = Math.Min(psRisk30 * 1.2, 0.95), Risk90Day = Math.Min(psRisk30 * 1.4, 0.95), RemainingLifeEstimate = health.PowerStage.Score < 50 ? "< 3 months" : "1-3 years", TrendDescription = health.PowerStage.Status, KeyDrivers = ["Frequency jitter", "Thermal stress", "Load cycles"] },
                new SubsystemRisk { Subsystem = "Thermal", Risk30Day = thermRisk30, Risk60Day = Math.Min(thermRisk30 * 1.15, 0.95), Risk90Day = Math.Min(thermRisk30 * 1.3, 0.95), RemainingLifeEstimate = "N/A (operational factor)", TrendDescription = health.Thermal.Status, KeyDrivers = ["Ambient temperature", "Fan performance", "Load-induced heating"] }
            ]
        };
    }

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }

    private record RawPrediction(List<RawSubsystemRisk>? Subsystems, string? OverallTrend, string? Recommendation);
    private record RawSubsystemRisk(string? Subsystem, double Risk30Day, double Risk60Day, double Risk90Day,
        string? RemainingLifeEstimate, string? TrendDescription, List<string>? KeyDrivers);
}
