namespace VPMS.Models;

public class SubsystemHealth
{
    public string Name { get; set; } = string.Empty;
    public double Score { get; set; }                  // 0–100
    public string Status => Score >= 80 ? "Healthy" : Score >= 60 ? "Degraded" : Score >= 40 ? "Warning" : "Critical";
    public List<string> Observations { get; set; } = [];
    public List<string> KeyMetrics { get; set; } = [];
}

public class HealthIndex
{
    public SubsystemHealth Battery { get; set; } = new() { Name = "Battery" };
    public SubsystemHealth DcLinkCapacitor { get; set; } = new() { Name = "DC Link Capacitor" };
    public SubsystemHealth PowerStage { get; set; } = new() { Name = "Power Stage / IGBT" };
    public SubsystemHealth Thermal { get; set; } = new() { Name = "Thermal" };

    public double OverallScore => (Battery.Score * 0.35 + DcLinkCapacitor.Score * 0.25 + PowerStage.Score * 0.25 + Thermal.Score * 0.15);
    public string OverallStatus => OverallScore >= 80 ? "Healthy" : OverallScore >= 60 ? "Degraded" : OverallScore >= 40 ? "Warning" : "Critical";
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}
