namespace VPMS.Models;

public enum SessionStage
{
    Upload = 0,
    Issues = 1,
    Rca = 2,
    Query = 3,
    Fishbone = 4,
    Actions = 5,
    Prediction = 6,
    Report = 7
}

public class DiagnosticSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public SessionStage CurrentStage { get; set; } = SessionStage.Upload;
    public string EngineerName { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string UpsModel { get; set; } = string.Empty;
    public string UpsSerial { get; set; } = string.Empty;

    // Data
    public string? DataLogPath { get; set; }
    public string? AlarmLogPath { get; set; }
    public List<TelemetryRow> TelemetryRows { get; set; } = [];
    public List<AlarmEvent> AlarmEvents { get; set; } = [];

    // Pipeline artifacts
    public DataQualityReport? DataQuality { get; set; }
    public FeatureSet? Features { get; set; }
    public HealthIndex? Health { get; set; }
    public List<DiagnosticIssue> Issues { get; set; } = [];
    public List<RootCause> RootCauses { get; set; } = [];
    public List<QueryItem> Queries { get; set; } = [];
    public FishboneDiagram? Fishbone { get; set; }
    public List<CorrectiveAction> Actions { get; set; } = [];
    public PredictiveRiskReport? PredictiveRisk { get; set; }

    // Gate flags
    public bool Gate1Passed { get; set; }
    public bool Gate2Passed { get; set; }

    // Report metadata
    public string? ReportNotes { get; set; }
    public bool IsComplete => CurrentStage == SessionStage.Report && Gate1Passed && Gate2Passed;
}
