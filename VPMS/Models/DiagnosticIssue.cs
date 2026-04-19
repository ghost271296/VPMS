namespace VPMS.Models;

public enum IssueSeverity { Low, Medium, High, Critical }
public enum IssueSource { AI, Engineer }

public class DiagnosticIssue
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IssueSeverity Severity { get; set; }
    public double Confidence { get; set; }            // 0.0–1.0
    public string Subsystem { get; set; } = string.Empty;
    public List<string> SupportingFeatures { get; set; } = [];
    public List<string> SupportingAlarms { get; set; } = [];
    public string Evidence { get; set; } = string.Empty;
    public IssueSource Source { get; set; } = IssueSource.AI;
    public bool IsApproved { get; set; } = true;     // Gate 1 outcome
    public bool IsCustom { get; set; }               // Added by engineer
}
