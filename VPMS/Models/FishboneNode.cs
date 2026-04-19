namespace VPMS.Models;

public enum FishboneBranch { Thermal, Electrical, Component, Environmental, Operational, Human }

public class FishboneNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public FishboneBranch Branch { get; set; }
    public bool IsRootCause { get; set; }
    public double Confidence { get; set; }
    public List<FishboneNode> Children { get; set; } = [];
}

public class FishboneDiagram
{
    public string Effect { get; set; } = string.Empty;    // The problem statement (fish head)
    public List<FishboneNode> Branches { get; set; } = [];
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public bool IsConfirmedByEngineer { get; set; }
    public string? EngineerNotes { get; set; }
}
