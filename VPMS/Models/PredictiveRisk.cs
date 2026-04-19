namespace VPMS.Models;

public class SubsystemRisk
{
    public string Subsystem { get; set; } = string.Empty;
    public double Risk30Day { get; set; }      // 0.0–1.0 probability of failure
    public double Risk60Day { get; set; }
    public double Risk90Day { get; set; }
    public string RemainingLifeEstimate { get; set; } = string.Empty;
    public string TrendDescription { get; set; } = string.Empty;
    public List<string> KeyDrivers { get; set; } = [];
    public string RiskLevel => Risk30Day >= 0.7 ? "Critical" : Risk30Day >= 0.4 ? "High" : Risk30Day >= 0.2 ? "Medium" : "Low";
}

public class PredictiveRiskReport
{
    public List<SubsystemRisk> Subsystems { get; set; } = [];
    public string OverallTrend { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
