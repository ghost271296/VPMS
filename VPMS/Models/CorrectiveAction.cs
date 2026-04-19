namespace VPMS.Models;

public enum ActionPriority { Immediate, Scheduled, Monitor }

public class CorrectiveAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ActionPriority Priority { get; set; }
    public string Subsystem { get; set; } = string.Empty;
    public List<string> Parts { get; set; } = [];
    public string EstimatedTime { get; set; } = string.Empty;
    public string SkillLevel { get; set; } = string.Empty;    // Field / Specialized / R&D
    public List<string> RelatedIssueIds { get; set; } = [];
    public bool IsCompleted { get; set; }
    public string? Notes { get; set; }
}
