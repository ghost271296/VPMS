using VPMS.Models;

namespace VPMS.Services;

public class FeatureEngineService
{
    public FeatureSet Extract(List<TelemetryRow> rows, List<AlarmEvent> alarms)
    {
        var validRows = rows.Where(r => r.IsValid).OrderBy(r => r.Timestamp).ToList();
        if (validRows.Count == 0) return new FeatureSet();

        var fs = new FeatureSet
        {
            LogStart = validRows.First().Timestamp,
            LogEnd = validRows.Last().Timestamp,
            TotalRows = validRows.Count
        };
        fs.LogDuration = fs.LogEnd - fs.LogStart;
        fs.SamplingRateHz = fs.LogDuration.TotalSeconds > 0 ? validRows.Count / fs.LogDuration.TotalSeconds : 0;

        // DC Bus features
        var vdcVals = validRows.Select(r => r.VdcBus).Where(v => v > 0).ToList();
        if (vdcVals.Count > 0)
        {
            fs.VdcMean = Mean(vdcVals);
            fs.VdcStd = StdDev(vdcVals);
            fs.VdcMin = vdcVals.Min();
            fs.VdcMax = vdcVals.Max();
            fs.VdcRipplePeakToPeak = fs.VdcMax - fs.VdcMin;
        }

        // Battery features
        var vbattVals = validRows.Select(r => r.VbattTotal).Where(v => v > 0).ToList();
        var ibattVals = validRows.Select(r => r.Ibatt).ToList();
        var socVals = validRows.Select(r => r.StateOfCharge).Where(v => v >= 0 && v <= 100).ToList();
        var battTempVals = validRows.Select(r => r.BatteryTemp).Where(v => v > -20).ToList();

        if (vbattVals.Count > 0)
        {
            fs.VbattMean = Mean(vbattVals);
            fs.RbattProxy = ComputeRbattProxy(validRows);
            fs.VbattSagDepth = ComputeVoltageSagDepth(validRows);
        }
        if (ibattVals.Count > 0) fs.IbattMean = Mean(ibattVals);
        if (socVals.Count > 0) { fs.SocMean = Mean(socVals); fs.SocMin = socVals.Min(); }
        if (battTempVals.Count > 0) { fs.BattTempMean = Mean(battTempVals); fs.BattTempMax = battTempVals.Max(); }

        // Frequency features
        var freqOutVals = validRows.Select(r => r.FrequencyOutput).Where(v => v > 0).ToList();
        var freqInVals = validRows.Select(r => r.FrequencyInput).Where(v => v > 0).ToList();

        if (freqOutVals.Count > 0)
        {
            fs.FreqOutputMean = Mean(freqOutVals);
            fs.FreqOutputStd = StdDev(freqOutVals);
            fs.FreqOutputMaxDeviation = freqOutVals.Max(v => Math.Abs(v - fs.FreqOutputMean));
        }
        if (freqInVals.Count > 0)
        {
            fs.FreqInputMean = Mean(freqInVals);
            fs.FreqInputStd = StdDev(freqInVals);
        }

        // Load profile
        var loadVals = validRows.Select(r => r.LoadPercent).Where(v => v >= 0).ToList();
        if (loadVals.Count > 0)
        {
            fs.LoadMean = Mean(loadVals);
            fs.LoadMax = loadVals.Max();
            fs.LoadStd = StdDev(loadVals);
            fs.LoadDutyCycleHigh = loadVals.Count(v => v > 80.0) / (double)loadVals.Count * 100;
            fs.LoadHistogram = ComputeLoadHistogram(loadVals);
        }

        // Thermal features
        var ambientVals = validRows.Select(r => r.AmbientTemp).Where(v => v > -20 && v < 80).ToList();
        var heatsinkVals = validRows.Select(r => r.HeatsinkTemp).Where(v => v > -20).ToList();

        if (ambientVals.Count > 0) { fs.AmbientTempMean = Mean(ambientVals); fs.AmbientTempMax = ambientVals.Max(); }
        if (heatsinkVals.Count > 0) { fs.HeatsinkTempMean = Mean(heatsinkVals); fs.HeatsinkTempMax = heatsinkVals.Max(); }
        fs.ThermalHeadroom = 85.0 - fs.HeatsinkTempMax;  // Assume 85°C rated max

        // Fan features
        var fan1Vals = validRows.Select(r => r.Fan1SpeedRpm).Where(v => v >= 0).ToList();
        var fan2Vals = validRows.Select(r => r.Fan2SpeedRpm).Where(v => v >= 0).ToList();
        if (fan1Vals.Count > 0) fs.Fan1SpeedMean = Mean(fan1Vals);
        if (fan2Vals.Count > 0) fs.Fan2SpeedMean = Mean(fan2Vals);
        fs.FanAnomalyDetected = DetectFanAnomaly(validRows);

        // Alarm features
        fs.TotalAlarmCount = alarms.Count;
        fs.FaultCount = alarms.Count(a => a.Severity is AlarmSeverity.Fault or AlarmSeverity.Critical);
        fs.CriticalAlarmCount = alarms.Count(a => a.Severity == AlarmSeverity.Critical);
        fs.AlarmStormIndex = ComputeAlarmStormIndex(alarms);
        fs.AlarmsBySubsystem = alarms.GroupBy(a => a.Subsystem).ToDictionary(g => g.Key, g => g.Count());
        fs.AlarmsByCode = alarms.GroupBy(a => a.Code).ToDictionary(g => g.Key, g => g.Count());

        return fs;
    }

    private static double ComputeRbattProxy(List<TelemetryRow> rows)
    {
        // R ≈ ΔV/ΔI during current transients
        var transients = new List<double>();
        for (int i = 1; i < rows.Count; i++)
        {
            double dI = rows[i].Ibatt - rows[i - 1].Ibatt;
            double dV = rows[i].VbattTotal - rows[i - 1].VbattTotal;
            if (Math.Abs(dI) > 5 && dV != 0 && rows[i].Ibatt != 0)
            {
                double r = -dV / dI;  // Negative because discharge reduces voltage
                if (r > 0 && r < 1.0)  // Realistic range in ohms
                    transients.Add(r);
            }
        }
        return transients.Count > 0 ? Mean(transients) : 0.0;
    }

    private static double ComputeVoltageSagDepth(List<TelemetryRow> rows)
    {
        if (rows.Count == 0) return 0;
        var vbattMean = Mean(rows.Select(r => r.VbattTotal).Where(v => v > 0).ToList());
        var maxSag = rows.Select(r => r.VbattTotal).Where(v => v > 0).Select(v => vbattMean - v).Max();
        return Math.Max(0, maxSag);
    }

    private static bool DetectFanAnomaly(List<TelemetryRow> rows)
    {
        // High temperature but low fan speed = anomaly
        var highTempRows = rows.Where(r => r.HeatsinkTemp > 60).ToList();
        if (!highTempRows.Any()) return false;
        var avgFanSpeed = Mean(highTempRows.Select(r => (r.Fan1SpeedRpm + r.Fan2SpeedRpm) / 2).ToList());
        return avgFanSpeed < 500 && highTempRows.Count > 10;
    }

    private static double ComputeAlarmStormIndex(List<AlarmEvent> alarms)
    {
        if (alarms.Count < 2) return 0;
        var sorted = alarms.OrderBy(a => a.Timestamp).ToList();
        var windowSize = TimeSpan.FromMinutes(5);
        double maxRate = 0;

        for (int i = 0; i < sorted.Count; i++)
        {
            var windowEnd = sorted[i].Timestamp + windowSize;
            int count = sorted.Count(a => a.Timestamp >= sorted[i].Timestamp && a.Timestamp <= windowEnd);
            double rate = count / windowSize.TotalHours;
            if (rate > maxRate) maxRate = rate;
        }
        return maxRate;
    }

    private static Dictionary<string, double> ComputeLoadHistogram(List<double> loadVals)
    {
        var buckets = new Dictionary<string, double>
        {
            ["0-20%"] = 0, ["20-40%"] = 0, ["40-60%"] = 0,
            ["60-80%"] = 0, ["80-100%"] = 0, [">100%"] = 0
        };
        foreach (var v in loadVals)
        {
            if (v < 20) buckets["0-20%"]++;
            else if (v < 40) buckets["20-40%"]++;
            else if (v < 60) buckets["40-60%"]++;
            else if (v < 80) buckets["60-80%"]++;
            else if (v <= 100) buckets["80-100%"]++;
            else buckets[">100%"]++;
        }
        foreach (var key in buckets.Keys.ToList())
            buckets[key] = buckets[key] / loadVals.Count * 100;
        return buckets;
    }

    private static double Mean(List<double> vals) =>
        vals.Count > 0 ? vals.Average() : 0;

    private static double StdDev(List<double> vals)
    {
        if (vals.Count < 2) return 0;
        var mean = vals.Average();
        return Math.Sqrt(vals.Average(v => Math.Pow(v - mean, 2)));
    }
}
