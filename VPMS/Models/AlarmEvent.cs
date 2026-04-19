namespace VPMS.Models;

public enum AlarmSeverity { Info, Warning, Fault, Critical }

public class AlarmEvent
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime? ClearedAt { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlarmSeverity Severity { get; set; }
    public string Subsystem { get; set; } = string.Empty;
    public TimeSpan? Duration => ClearedAt.HasValue ? ClearedAt.Value - Timestamp : null;
    public bool IsActive => !ClearedAt.HasValue;
}
