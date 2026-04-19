namespace VPMS.Models;

public class RootCause
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public double Probability { get; set; }          // 0.0–1.0
    public double Confidence { get; set; }           // 0.0–1.0
    public int Rank { get; set; }
    public string Category { get; set; } = string.Empty;   // Thermal, Electrical, Component, etc.
    public List<string> SupportingEvidence { get; set; } = [];
    public List<string> ConflictingEvidence { get; set; } = [];
    public List<string> RelatedIssueIds { get; set; } = [];
}
