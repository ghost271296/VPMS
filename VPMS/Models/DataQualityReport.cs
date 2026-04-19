namespace VPMS.Models;

public class DataQualityReport
{
    public int Score { get; set; }                      // 0–100
    public string Grade => Score >= 90 ? "Excellent" : Score >= 75 ? "Good" : Score >= 50 ? "Fair" : "Poor";
    public bool IsAdequate => Score >= 50;

    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int MissingValueRows { get; set; }
    public int DuplicateRows { get; set; }
    public int OutlierRows { get; set; }
    public int TimestampGaps { get; set; }
    public TimeSpan LogDuration { get; set; }
    public bool IsLogDurationSufficient => LogDuration.TotalMinutes >= 5;

    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];

    public double MissingValuePct => TotalRows > 0 ? (double)MissingValueRows / TotalRows * 100 : 0;
    public double OutlierPct => TotalRows > 0 ? (double)OutlierRows / TotalRows * 100 : 0;

    public double ConfidencePenalty => (100 - Score) / 100.0 * 0.3;  // Up to 30% penalty
}
