using Newtonsoft.Json;
using VPMS.Models;

namespace VPMS.Services.AI;

public class QueryAgent(OpenAiClientService ai)
{
    private const string SystemPrompt = """
        You are a UPS diagnostic assistant. Based on the current root cause hypotheses,
        generate 3 to 5 targeted questions to ask the field engineer that will most help
        narrow down the diagnosis. Only ask high-information questions — avoid generic or
        low-value questions.
        Return a JSON array of questions. Each must have:
          question (string), rationale (1 sentence why this matters),
          inputType (Boolean/Text/Numeric/Date/MultiChoice),
          choices (array of strings, only for MultiChoice, else empty array),
          unit (string, e.g. "°C", "years", or "" if not applicable),
          informationGain (0.0-1.0, estimated diagnostic value).
        Return ONLY valid JSON, no markdown.
        """;

    public async Task<List<QueryItem>> GenerateAsync(List<RootCause> rootCauses, FeatureSet fs,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(rootCauses, fs);
        var json = await ai.CompleteWithRetryAsync(SystemPrompt, prompt, ct: ct);

        try
        {
            json = CleanJson(json);
            var raw = JsonConvert.DeserializeObject<List<RawQuery>>(json) ?? [];
            return raw.OrderByDescending(q => q.InformationGain).Take(5).Select(r => new QueryItem
            {
                Question = r.Question ?? "Please describe any observations.",
                Rationale = r.Rationale ?? string.Empty,
                InputType = Enum.TryParse<QueryInputType>(r.InputType, true, out var t) ? t : QueryInputType.Text,
                Choices = r.Choices ?? [],
                Unit = r.Unit ?? string.Empty,
                InformationGain = r.InformationGain
            }).ToList();
        }
        catch
        {
            return DefaultQueries();
        }
    }

    private static string BuildPrompt(List<RootCause> rootCauses, FeatureSet fs)
    {
        var causeList = string.Join("\n", rootCauses.Take(3).Select(rc =>
            $"- [{rc.Rank}] {rc.Title} ({rc.Probability * 100:F0}% probability, category: {rc.Category})"));

        return $"""
            TOP ROOT CAUSE HYPOTHESES:
            {causeList}

            KEY METRICS:
            - Battery temp max: {fs.BattTempMax:F1}°C
            - Fan anomaly: {fs.FanAnomalyDetected}
            - Heatsink max: {fs.HeatsinkTempMax:F1}°C
            - Ambient max: {fs.AmbientTempMax:F1}°C
            - Load max: {fs.LoadMax:F1}%
            - Rbatt proxy: {fs.RbattProxy * 1000:F1} mΩ

            Generate 3-5 targeted questions for the site engineer that would most help confirm or
            rule out the top hypotheses. Questions must be answerable at the site.
            """;
    }

    private static List<QueryItem> DefaultQueries() =>
    [
        new() { Question = "Are the cooling fans operational?", Rationale = "Fan failure is a common cause of thermal issues.", InputType = QueryInputType.Boolean, InformationGain = 0.9 },
        new() { Question = "What is the ambient temperature at the site?", Rationale = "High ambient reduces thermal headroom.", InputType = QueryInputType.Numeric, Unit = "°C", InformationGain = 0.8 },
        new() { Question = "When was the battery last replaced?", Rationale = "Battery age correlates with resistance increase.", InputType = QueryInputType.Date, InformationGain = 0.85 },
        new() { Question = "Has the UPS been operating in bypass mode recently?", Rationale = "Bypass events indicate inverter faults.", InputType = QueryInputType.Boolean, InformationGain = 0.75 },
        new() { Question = "What is the site's typical load profile?", Rationale = "Consistent overloading stresses the power stage.", InputType = QueryInputType.MultiChoice, Choices = ["Light (<50%)", "Medium (50-80%)", "Heavy (>80%)", "Variable"], InformationGain = 0.7 }
    ];

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }

    private record RawQuery(string? Question, string? Rationale, string? InputType,
        List<string>? Choices, string? Unit, double InformationGain);
}
