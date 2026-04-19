using Newtonsoft.Json;
using VPMS.Models;

namespace VPMS.Services.AI;

public class FishboneAgent(OpenAiClientService ai)
{
    private const string SystemPrompt = """
        You are a UPS reliability engineer creating an Ishikawa (fishbone) cause-and-effect diagram.
        Given the root causes and engineer answers, build a structured fishbone diagram.
        The "effect" is the primary problem. Each branch represents a causal category.
        Return a JSON object with:
          effect (string — the main problem), branches (array of nodes).
        Each node has: label, detail (optional), branch (Thermal/Electrical/Component/Environmental/Operational/Human),
          isRootCause (bool), confidence (0.0-1.0), children (array of child nodes, same structure).
        Keep it structured but concise — max 3 levels deep, max 4 nodes per branch.
        Return ONLY valid JSON, no markdown.
        """;

    public async Task<FishboneDiagram> GenerateAsync(List<RootCause> rootCauses,
        List<DiagnosticIssue> issues, List<QueryItem> queries, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(rootCauses, issues, queries);
        var json = await ai.CompleteWithRetryAsync(SystemPrompt, prompt, ct: ct);

        try
        {
            json = CleanJson(json);
            var raw = JsonConvert.DeserializeObject<RawFishbone>(json);
            if (raw is null) return FallbackFishbone(rootCauses, issues);

            var diagram = new FishboneDiagram { Effect = raw.Effect ?? "UPS Malfunction" };
            diagram.Branches = raw.Branches?.Select(MapNode).ToList() ?? [];
            return diagram;
        }
        catch
        {
            return FallbackFishbone(rootCauses, issues);
        }
    }

    private static string BuildPrompt(List<RootCause> rootCauses, List<DiagnosticIssue> issues, List<QueryItem> queries)
    {
        var rcaText = string.Join("\n", rootCauses.Select(rc => $"- {rc.Title}: {rc.Explanation} (p={rc.Probability:P0})"));
        var issueText = string.Join("\n", issues.Where(i => i.IsApproved).Select(i => $"- [{i.Severity}] {i.Title}"));
        var qaText = string.Join("\n", queries.Where(q => q.IsAnswered).Select(q => $"Q: {q.Question}\nA: {q.Answer}"));

        return $"""
            PRIMARY ISSUES:
            {issueText}

            ROOT CAUSES:
            {rcaText}

            ENGINEER FIELD OBSERVATIONS:
            {qaText}

            Build an Ishikawa fishbone diagram with the main effect and causal branches.
            """;
    }

    private static FishboneNode MapNode(RawFishboneNode raw)
    {
        var node = new FishboneNode
        {
            Label = raw.Label ?? "Unknown",
            Detail = raw.Detail,
            Branch = Enum.TryParse<FishboneBranch>(raw.Branch, true, out var b) ? b : FishboneBranch.Electrical,
            IsRootCause = raw.IsRootCause,
            Confidence = raw.Confidence
        };
        if (raw.Children is { Count: > 0 })
            node.Children = raw.Children.Select(MapNode).ToList();
        return node;
    }

    private static FishboneDiagram FallbackFishbone(List<RootCause> rootCauses, List<DiagnosticIssue> issues)
    {
        var diagram = new FishboneDiagram
        {
            Effect = issues.FirstOrDefault(i => i.Severity == IssueSeverity.Critical)?.Title
                     ?? issues.FirstOrDefault()?.Title ?? "UPS Fault Condition"
        };

        foreach (var rc in rootCauses.Take(5))
        {
            var branch = Enum.TryParse<FishboneBranch>(rc.Category, true, out var b) ? b : FishboneBranch.Electrical;
            diagram.Branches.Add(new FishboneNode
            {
                Label = rc.Title,
                Detail = rc.Explanation,
                Branch = branch,
                IsRootCause = rc.Rank == 1,
                Confidence = rc.Confidence
            });
        }

        return diagram;
    }

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }

    private record RawFishbone(string? Effect, List<RawFishboneNode>? Branches);
    private record RawFishboneNode(string? Label, string? Detail, string? Branch,
        bool IsRootCause, double Confidence, List<RawFishboneNode>? Children);
}
