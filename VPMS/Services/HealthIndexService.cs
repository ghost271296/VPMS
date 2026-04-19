using VPMS.Models;

namespace VPMS.Services;

public class HealthIndexService
{
    public HealthIndex Compute(FeatureSet fs, DataQualityReport dq)
    {
        var hi = new HealthIndex();
        double dqPenalty = dq.ConfidencePenalty;

        hi.Battery = ScoreBattery(fs, dqPenalty);
        hi.DcLinkCapacitor = ScoreDcLink(fs, dqPenalty);
        hi.PowerStage = ScorePowerStage(fs, dqPenalty);
        hi.Thermal = ScoreThermal(fs, dqPenalty);
        hi.ComputedAt = DateTime.UtcNow;

        return hi;
    }

    private static SubsystemHealth ScoreBattery(FeatureSet fs, double penalty)
    {
        double score = 100;
        var obs = new List<string>();
        var metrics = new List<string>();

        // Internal resistance proxy
        if (fs.RbattProxy > 0)
        {
            metrics.Add($"Rbatt proxy: {fs.RbattProxy * 1000:F1} mΩ");
            if (fs.RbattProxy > 0.20) { score -= 30; obs.Add("Very high internal resistance — end-of-life likely."); }
            else if (fs.RbattProxy > 0.12) { score -= 15; obs.Add("Elevated internal resistance — battery aging."); }
            else if (fs.RbattProxy > 0.07) { score -= 5; obs.Add("Slightly elevated internal resistance."); }
        }

        // Voltage sag
        if (fs.VbattSagDepth > 0)
        {
            metrics.Add($"Voltage sag: {fs.VbattSagDepth:F2} V");
            if (fs.VbattSagDepth > 10) { score -= 20; obs.Add("Large voltage sag under load — reduced capacity."); }
            else if (fs.VbattSagDepth > 5) { score -= 8; obs.Add("Moderate voltage sag detected."); }
        }

        // SoC
        metrics.Add($"SoC min: {fs.SocMin:F1}%");
        if (fs.SocMin < 60 && fs.SocMin > 0) { score -= 15; obs.Add($"SoC dropped to {fs.SocMin:F1}% — abnormal discharge."); }
        else if (fs.SocMin < 80 && fs.SocMin > 0) { score -= 5; obs.Add($"SoC reached {fs.SocMin:F1}% during log."); }

        // Temperature
        if (fs.BattTempMax > 0)
        {
            metrics.Add($"Batt temp max: {fs.BattTempMax:F1}°C");
            if (fs.BattTempMax > 40) { score -= 15; obs.Add($"Battery overtemperature ({fs.BattTempMax:F1}°C > 40°C limit)."); }
            else if (fs.BattTempMax > 35) { score -= 5; obs.Add("Battery temperature approaching upper limit."); }
        }

        if (obs.Count == 0) obs.Add("Battery parameters within normal range.");
        score = Math.Max(0, score * (1 - penalty * 0.5));

        return new SubsystemHealth { Name = "Battery", Score = score, Observations = obs, KeyMetrics = metrics };
    }

    private static SubsystemHealth ScoreDcLink(FeatureSet fs, double penalty)
    {
        double score = 100;
        var obs = new List<string>();
        var metrics = new List<string>();

        metrics.Add($"Vdc ripple (std): {fs.VdcStd:F2} V");
        metrics.Add($"Vdc P-P: {fs.VdcRipplePeakToPeak:F2} V");

        if (fs.VdcStd > 8) { score -= 35; obs.Add("Severe DC bus ripple — capacitor failure likely."); }
        else if (fs.VdcStd > 4) { score -= 18; obs.Add("Elevated DC bus ripple — capacitor degradation."); }
        else if (fs.VdcStd > 2) { score -= 7; obs.Add("Slightly elevated DC ripple."); }

        if (fs.VdcRipplePeakToPeak > 20) { score -= 15; obs.Add($"Peak-to-peak Vdc variation {fs.VdcRipplePeakToPeak:F1} V is excessive."); }

        if (obs.Count == 0) obs.Add("DC link ripple within acceptable limits.");
        score = Math.Max(0, score * (1 - penalty * 0.5));

        return new SubsystemHealth { Name = "DC Link Capacitor", Score = score, Observations = obs, KeyMetrics = metrics };
    }

    private static SubsystemHealth ScorePowerStage(FeatureSet fs, double penalty)
    {
        double score = 100;
        var obs = new List<string>();
        var metrics = new List<string>();

        metrics.Add($"Freq output std: {fs.FreqOutputStd:F4} Hz");
        metrics.Add($"Freq max deviation: {fs.FreqOutputMaxDeviation:F4} Hz");
        metrics.Add($"Load max: {fs.LoadMax:F1}%");

        // Frequency jitter
        if (fs.FreqOutputStd > 0.5) { score -= 25; obs.Add("Severe output frequency jitter — inverter/PLL anomaly."); }
        else if (fs.FreqOutputStd > 0.2) { score -= 12; obs.Add("Elevated frequency jitter detected."); }
        else if (fs.FreqOutputStd > 0.05) { score -= 4; obs.Add("Slight output frequency instability."); }

        // Overload
        if (fs.LoadMax > 100) { score -= 20; obs.Add($"Load exceeded 100% ({fs.LoadMax:F1}%) — thermal stress on power stage."); }
        else if (fs.LoadMax > 85) { score -= 8; obs.Add($"High peak load ({fs.LoadMax:F1}%) detected."); }

        // Fan anomaly affecting power stage thermal
        if (fs.FanAnomalyDetected) { score -= 15; obs.Add("Fan anomaly detected at elevated temperature."); }

        if (obs.Count == 0) obs.Add("Power stage metrics within normal limits.");
        score = Math.Max(0, score * (1 - penalty * 0.5));

        return new SubsystemHealth { Name = "Power Stage / IGBT", Score = score, Observations = obs, KeyMetrics = metrics };
    }

    private static SubsystemHealth ScoreThermal(FeatureSet fs, double penalty)
    {
        double score = 100;
        var obs = new List<string>();
        var metrics = new List<string>();

        metrics.Add($"Heatsink max: {fs.HeatsinkTempMax:F1}°C");
        metrics.Add($"Ambient max: {fs.AmbientTempMax:F1}°C");
        metrics.Add($"Thermal headroom: {fs.ThermalHeadroom:F1}°C");

        if (fs.HeatsinkTempMax > 75) { score -= 30; obs.Add($"Critical heatsink temperature {fs.HeatsinkTempMax:F1}°C — immediate investigation required."); }
        else if (fs.HeatsinkTempMax > 65) { score -= 15; obs.Add($"Elevated heatsink temperature {fs.HeatsinkTempMax:F1}°C."); }
        else if (fs.HeatsinkTempMax > 55) { score -= 5; obs.Add("Heatsink temperature approaching threshold."); }

        if (fs.AmbientTempMax > 40) { score -= 15; obs.Add($"Ambient temperature {fs.AmbientTempMax:F1}°C exceeds recommended 40°C limit."); }

        if (fs.ThermalHeadroom < 10) { score -= 20; obs.Add($"Thermal headroom only {fs.ThermalHeadroom:F1}°C — thermal margin is critically low."); }

        if (fs.FanAnomalyDetected) { score -= 15; obs.Add("Fan underperformance at elevated heatsink temperatures."); }

        if (obs.Count == 0) obs.Add("Thermal profile within acceptable limits.");
        score = Math.Max(0, score * (1 - penalty * 0.5));

        return new SubsystemHealth { Name = "Thermal", Score = score, Observations = obs, KeyMetrics = metrics };
    }
}
