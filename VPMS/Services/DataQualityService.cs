using VPMS.Models;

namespace VPMS.Services;

public class DataQualityService
{
    public DataQualityReport Analyze(List<TelemetryRow> rows, List<AlarmEvent> alarms)
    {
        var report = new DataQualityReport { TotalRows = rows.Count };

        if (rows.Count == 0)
        {
            report.Score = 0;
            report.Errors.Add("DataLog contains no data rows.");
            return report;
        }

        var sorted = rows.OrderBy(r => r.Timestamp).ToList();
        report.LogDuration = sorted.Last().Timestamp - sorted.First().Timestamp;

        int score = 100;

        // 1. Check timestamp monotonicity and gaps
        int gapCount = 0;
        TimeSpan expectedInterval = EstimateExpectedInterval(sorted);
        for (int i = 1; i < sorted.Count; i++)
        {
            var gap = sorted[i].Timestamp - sorted[i - 1].Timestamp;
            if (gap < TimeSpan.Zero)
            {
                report.Warnings.Add($"Non-monotonic timestamp at row {i + 1}.");
                score -= 5;
            }
            else if (gap > expectedInterval * 5 && gap.TotalSeconds > 60)
            {
                gapCount++;
            }
        }
        report.TimestampGaps = gapCount;
        if (gapCount > 0)
        {
            report.Warnings.Add($"{gapCount} timestamp gap(s) detected (> 5× expected interval).");
            score -= Math.Min(gapCount * 2, 15);
        }

        // 2. Check for missing values (zero on critical fields treated as missing)
        int missingRows = 0;
        int outlierRows = 0;
        int duplicates = 0;

        var seen = new HashSet<DateTime>();
        foreach (var row in sorted)
        {
            if (!seen.Add(row.Timestamp))
                duplicates++;

            bool hasMissing = row.VdcBus == 0 && row.VbattTotal == 0 && row.LoadPercent == 0;
            if (hasMissing) missingRows++;

            bool hasOutlier = row.VdcBus > 1000 || row.VdcBus < 0 ||
                              row.LoadPercent > 120 || row.LoadPercent < 0 ||
                              row.AmbientTemp > 80 || row.AmbientTemp < -20 ||
                              row.FrequencyOutput is > 70 or (< 40 and > 0);
            if (hasOutlier) outlierRows++;
        }

        report.MissingValueRows = missingRows;
        report.DuplicateRows = duplicates;
        report.OutlierRows = outlierRows;
        report.ValidRows = rows.Count - missingRows;

        if (missingRows > 0)
        {
            score -= (int)Math.Min(report.MissingValuePct * 0.5, 20);
            report.Warnings.Add($"{missingRows} rows ({report.MissingValuePct:F1}%) have missing values on critical channels.");
        }
        if (duplicates > 0)
        {
            score -= Math.Min(duplicates, 5);
            report.Warnings.Add($"{duplicates} duplicate timestamp(s) detected and removed in analysis.");
        }
        if (outlierRows > 0)
        {
            score -= (int)Math.Min(report.OutlierPct * 0.3, 10);
            report.Warnings.Add($"{outlierRows} rows contain outlier values — preserved but flagged.");
        }

        // 3. Log duration check
        if (!report.IsLogDurationSufficient)
        {
            score -= 20;
            report.Warnings.Add($"Log duration is only {report.LogDuration.TotalMinutes:F1} min (< 5 min minimum). Confidence will be capped.");
            report.Recommendations.Add("Re-upload logs covering at least 5 minutes of operation.");
        }
        else if (report.LogDuration.TotalMinutes < 15)
        {
            score -= 5;
            report.Warnings.Add($"Log duration is {report.LogDuration.TotalMinutes:F1} min. 15+ minutes recommended for full feature extraction.");
        }

        // 4. Alarm log checks
        if (alarms.Count == 0)
        {
            report.Warnings.Add("No alarm events found. Alarm-based features will not be computed.");
            score -= 5;
        }

        // 5. Recommendations
        if (report.MissingValuePct > 20)
            report.Recommendations.Add("High missing-value rate — consider re-collecting logs or excluding the affected window.");
        if (gapCount > 5)
            report.Recommendations.Add("Multiple large timestamp gaps — ensure the UPS data logger was not interrupted.");

        report.Score = Math.Max(score, 0);

        if (report.Score >= 90)
            report.Recommendations.Add("Data quality is excellent. Analysis can proceed at full confidence.");
        else if (report.Score >= 75)
            report.Recommendations.Add("Data quality is good. Minor issues noted; proceed with confidence.");
        else if (report.Score >= 50)
            report.Recommendations.Add("Data quality is fair. Results will carry reduced confidence scores.");
        else
            report.Errors.Add("Data quality is poor. Consider re-uploading cleaner logs before analysis.");

        return report;
    }

    private static TimeSpan EstimateExpectedInterval(List<TelemetryRow> sorted)
    {
        if (sorted.Count < 2) return TimeSpan.FromSeconds(1);
        var gaps = sorted.Skip(1).Zip(sorted, (a, b) => (a.Timestamp - b.Timestamp).TotalSeconds)
                         .Where(g => g > 0).ToList();
        if (!gaps.Any()) return TimeSpan.FromSeconds(1);
        var median = gaps.OrderBy(x => x).ElementAt(gaps.Count / 2);
        return TimeSpan.FromSeconds(median);
    }
}
