using Newtonsoft.Json;
using VPMS.Models;

namespace VPMS.Services.AI;

public class CorrectiveActionAgent(OpenAiClientService ai)
{
    private const string SystemPrompt = """
        You are a Vertiv UPS service engineer generating corrective actions.
        Given the confirmed fishbone diagram and root causes, produce a prioritized action plan.
        Return a JSON array of actions. Each must have:
          title, description, priority (Immediate/Scheduled/Monitor),
          subsystem, parts (list of part names/numbers if applicable, else empty list),
          estimatedTime (e.g. "2 hours", "30 minutes"), skillLevel (Field/Specialized/R&D),
          relatedIssueIds (list of issue IDs, may be empty).
        Order by priority: Immediate first, then Scheduled, then Monitor.
        Be practical and specific. Return ONLY valid JSON, no markdown.
        """;

    public async Task<List<CorrectiveAction>> GenerateAsync(FishboneDiagram fishbone,
        List<RootCause> rootCauses, List<DiagnosticIssue> issues, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(fishbone, rootCauses, issues);
        var json = await ai.CompleteWithRetryAsync(SystemPrompt, prompt, ct: ct);

        try
        {
            json = CleanJson(json);
            var raw = JsonConvert.DeserializeObject<List<RawAction>>(json) ?? [];
            return raw.Select(r => new CorrectiveAction
            {
                Title = r.Title ?? "Action Required",
                Description = r.Description ?? string.Empty,
                Priority = Enum.TryParse<ActionPriority>(r.Priority, true, out var p) ? p : ActionPriority.Scheduled,
                Subsystem = r.Subsystem ?? "General",
                Parts = r.Parts ?? [],
                EstimatedTime = r.EstimatedTime ?? "TBD",
                SkillLevel = r.SkillLevel ?? "Field",
                RelatedIssueIds = r.RelatedIssueIds ?? []
            }).OrderBy(a => a.Priority).ToList();
        }
        catch
        {
            return FallbackActions(rootCauses);
        }
    }

    private static string BuildPrompt(FishboneDiagram fishbone, List<RootCause> rootCauses, List<DiagnosticIssue> issues)
    {
        var rcaText = string.Join("\n", rootCauses.Take(3).Select(rc =>
            $"- {rc.Title} ({rc.Category}, p={rc.Probability:P0}): {rc.Explanation}"));
        var branchText = string.Join("\n", fishbone.Branches.Where(b => b.IsRootCause)
            .Select(b => $"- [{b.Branch}] {b.Label}: {b.Detail}"));
        var issueIds = string.Join(", ", issues.Where(i => i.IsApproved).Select(i => i.Id));

        return $"""
            FISHBONE ROOT CAUSES:
            {branchText}

            RANKED ROOT CAUSES:
            {rcaText}

            ACTIVE ISSUE IDs: {issueIds}

            Generate corrective actions covering Immediate (do now), Scheduled (plan within 30 days),
            and Monitor (watch closely) categories.
            """;
    }

    private static List<CorrectiveAction> FallbackActions(List<RootCause> rootCauses)
    {
        var actions = new List<CorrectiveAction>();
        foreach (var rc in rootCauses.Take(3))
        {
            actions.Add(new CorrectiveAction
            {
                Title = $"Investigate: {rc.Title}",
                Description = rc.Explanation,
                Priority = rc.Rank == 1 ? ActionPriority.Immediate : ActionPriority.Scheduled,
                Subsystem = rc.Category,
                EstimatedTime = "1-2 hours",
                SkillLevel = "Field"
            });
        }
        actions.Add(new CorrectiveAction
        {
            Title = "Monitor all parameters after actions",
            Description = "Log telemetry for 24 hours post-repair to confirm resolution.",
            Priority = ActionPriority.Monitor,
            EstimatedTime = "24 hours",
            SkillLevel = "Field"
        });
        return actions;
    }

    private static string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json")) text = text[7..];
        if (text.StartsWith("```")) text = text[3..];
        if (text.EndsWith("```")) text = text[..^3];
        return text.Trim();
    }

    private record RawAction(string? Title, string? Description, string? Priority, string? Subsystem,
        List<string>? Parts, string? EstimatedTime, string? SkillLevel, List<string>? RelatedIssueIds);
}
