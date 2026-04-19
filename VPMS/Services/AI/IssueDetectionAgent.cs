using Newtonsoft.Json;
using VPMS.Models;

namespace VPMS.Services.AI;

public class IssueDetectionAgent(OpenAiClientService ai)
{
    private const string SystemPrompt = """
        You are an expert UPS (Uninterruptible Power Supply) diagnostic engineer.
        Analyze the provided engineering feature set and return a JSON array of detected issues.
        Each issue must have: title, description, severity (Low/Medium/High/Critical),
        confidence (0.0-1.0), subsystem (Battery/DCLink/Inverter/Thermal/Fans/Electrical/Control),
        supportingFeatures (list of feature names with values), evidence (1-2 sentence rationale).
        Focus on factual, evidence-backed issues. Do not invent issues not supported by the data.
        Return ONLY valid JSON, no markdown, no explanation.
        """;

    public async Task<List<DiagnosticIssue>> DetectAsync(FeatureSet fs, DataQualityReport dq, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(fs, dq);
        var json = await ai.CompleteWithRetryAsync(SystemPrompt, prompt, ct: ct);

        try
        {
            // Strip any markdown code fences if present
            json = CleanJson(json);
            var raw = JsonConvert.DeserializeObject<List<RawIssue>>(json) ?? [];
            return raw.Select(r => new DiagnosticIssue
            {
                Title = r.Title ?? "Unknown Issue",
                Description = r.Description ?? string.Empty,
                Severity = Enum.TryParse<IssueSeverity>(r.Severity, true, out var sev) ? sev : IssueSeverity.Medium,
                Confidence = Math.Clamp(r.Confidence - dq.ConfidencePenalty, 0.05, 1.0),
                Subsystem = r.Subsystem ?? "Unknown",
                SupportingFeatures = r.SupportingFeatures ?? [],
                Evidence = r.Evidence ?? string.Empty,
                Source = IssueSource.AI
            }).ToList();
        }
        catch
        {
            return FallbackRules(fs, dq);
        }
    }

    private static string BuildPrompt(FeatureSet fs, DataQualityReport dq) => $"""
        DATA QUALITY SCORE: {dq.Score}/100 (apply {dq.ConfidencePenalty * 100:F0}% confidence penalty)
        LOG DURATION: {fs.LogDuration.TotalMinutes:F1} minutes
        TOTAL ROWS: {fs.TotalRows}

        DC BUS:
        - Vdc mean: {fs.VdcMean:F2} V, std: {fs.VdcStd:F3} V, P-P: {fs.VdcRipplePeakToPeak:F2} V

        BATTERY:
        - Vbatt mean: {fs.VbattMean:F2} V, sag depth: {fs.VbattSagDepth:F2} V
        - Ibatt mean: {fs.IbattMean:F2} A
        - Rbatt proxy: {fs.RbattProxy * 1000:F1} mΩ
        - SoC mean: {fs.SocMean:F1}%, SoC min: {fs.SocMin:F1}%
        - Battery temp max: {fs.BattTempMax:F1}°C

        FREQUENCY:
        - Output freq mean: {fs.FreqOutputMean:F4} Hz, std: {fs.FreqOutputStd:F5} Hz
        - Output freq max deviation: {fs.FreqOutputMaxDeviation:F4} Hz
        - Input freq std: {fs.FreqInputStd:F5} Hz

        LOAD:
        - Mean: {fs.LoadMean:F1}%, Max: {fs.LoadMax:F1}%, StdDev: {fs.LoadStd:F1}%
        - Time above 80%: {fs.LoadDutyCycleHigh:F1}%

        THERMAL:
        - Ambient max: {fs.AmbientTempMax:F1}°C, Heatsink max: {fs.HeatsinkTempMax:F1}°C
        - Thermal headroom: {fs.ThermalHeadroom:F1}°C
        - Fan anomaly detected: {fs.FanAnomalyDetected}

        ALARMS:
        - Total: {fs.TotalAlarmCount}, Faults: {fs.FaultCount}, Critical: {fs.CriticalAlarmCount}
        - Alarm storm index (alarms/hr worst 5-min): {fs.AlarmStormIndex:F1}
        - By subsystem: {string.Join(", ", fs.AlarmsBySubsystem.Select(kv => $"{kv.Key}:{kv.Value}"))}

        Detect all engineering issues evident in this data.
        """;

    private static List<DiagnosticIssue> FallbackRules(FeatureSet fs, DataQualityReport dq)
    {
        var issues = new List<DiagnosticIssue>();
        double pen = dq.ConfidencePenalty;

        if (fs.RbattProxy > 0.12)
            issues.Add(new() { Title = "High Battery Internal Resistance", Severity = IssueSeverity.High, Confidence = 0.85 - pen, Subsystem = "Battery", Evidence = $"Rbatt proxy = {fs.RbattProxy * 1000:F1} mΩ (threshold: 120 mΩ)" });

        if (fs.VdcStd > 4)
            issues.Add(new() { Title = "Excessive DC Bus Ripple", Severity = IssueSeverity.High, Confidence = 0.90 - pen, Subsystem = "DCLink", Evidence = $"Vdc std = {fs.VdcStd:F2} V (threshold: 4 V)" });

        if (fs.FreqOutputStd > 0.2)
            issues.Add(new() { Title = "Output Frequency Instability", Severity = IssueSeverity.Medium, Confidence = 0.80 - pen, Subsystem = "Inverter", Evidence = $"Freq std = {fs.FreqOutputStd:F4} Hz (threshold: 0.2 Hz)" });

        if (fs.HeatsinkTempMax > 65)
            issues.Add(new() { Title = "Elevated Heatsink Temperature", Severity = IssueSeverity.High, Confidence = 0.88 - pen, Subsystem = "Thermal", Evidence = $"Heatsink max = {fs.HeatsinkTempMax:F1}°C (threshold: 65°C)" });

        if (fs.AlarmStormIndex > 60)
            issues.Add(new() { Title = "Alarm Storm Detected", Severity = IssueSeverity.Critical, Confidence = 0.95 - pen, Subsystem = "Control", Evidence = $"Alarm storm index = {fs.AlarmStormIndex:F0} alarms/hr in 5-minute window" });

        if (fs.FanAnomalyDetected)
            issues.Add(new() { Title = "Fan Underperformance at Elevated Temperature", Severity = IssueSeverity.High, Confidence = 0.82 - pen, Subsystem = "Thermal", Evidence = "Low fan RPM detected while heatsink temperature is elevated" });

        return issues;
    }

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }

    private record RawIssue(string? Title, string? Description, string? Severity, double Confidence,
        string? Subsystem, List<string>? SupportingFeatures, string? Evidence);
}
