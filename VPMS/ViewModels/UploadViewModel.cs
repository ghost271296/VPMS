using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPMS.Models;
using VPMS.Services;
using VPMS.Services.AI;

namespace VPMS.ViewModels;

public partial class UploadViewModel : ObservableObject
{
    private readonly ExcelParserService _parser;
    private readonly DataQualityService _dqService;
    private readonly FeatureEngineService _featureEngine;
    private readonly HealthIndexService _healthIndex;
    private readonly IssueDetectionAgent _issueAgent;

    [ObservableProperty] private string _dataLogPath = string.Empty;
    [ObservableProperty] private string _alarmLogPath = string.Empty;
    [ObservableProperty] private string _engineerName = string.Empty;
    [ObservableProperty] private string _siteId = string.Empty;
    [ObservableProperty] private string _upsModel = string.Empty;
    [ObservableProperty] private string _upsSerial = string.Empty;

    [ObservableProperty] private bool _isAnalyzing;
    [ObservableProperty] private string _analysisStatus = string.Empty;
    [ObservableProperty] private int _analysisProgress;

    [ObservableProperty] private bool _hasDataLog;
    [ObservableProperty] private bool _hasAlarmLog;
    [ObservableProperty] private bool _analysisReady;
    [ObservableProperty] private bool _showQualityReport;

    [ObservableProperty] private DataQualityReport? _qualityReport;
    [ObservableProperty] private List<string> _parseErrors = [];

    public event Action<DiagnosticSession>? AnalysisCompleted;

    public UploadViewModel(ExcelParserService parser, DataQualityService dqService,
        FeatureEngineService featureEngine, HealthIndexService healthIndex,
        IssueDetectionAgent issueAgent)
    {
        _parser = parser;
        _dqService = dqService;
        _featureEngine = featureEngine;
        _healthIndex = healthIndex;
        _issueAgent = issueAgent;
    }

    [RelayCommand]
    private void BrowseDataLog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select DataLog File",
            Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            DataLogPath = dlg.FileName;
            HasDataLog = true;
            UpdateAnalysisReady();
        }
    }

    [RelayCommand]
    private void BrowseAlarmLog()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select AlarmLog File",
            Filter = "Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            AlarmLogPath = dlg.FileName;
            HasAlarmLog = true;
            UpdateAnalysisReady();
        }
    }

    private void UpdateAnalysisReady() =>
        AnalysisReady = HasDataLog && !IsAnalyzing;

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (!HasDataLog) return;

        IsAnalyzing = true;
        AnalysisReady = false;
        ParseErrors = [];
        ShowQualityReport = false;
        var errors = new List<string>();

        var session = new DiagnosticSession
        {
            EngineerName = EngineerName,
            SiteId = SiteId,
            UpsModel = UpsModel,
            UpsSerial = UpsSerial
        };

        try
        {
            // Step 1: Parse DataLog
            AnalysisStatus = "Parsing DataLog…";
            AnalysisProgress = 10;
            var (rows, dataErrors) = await Task.Run(() => _parser.ParseDataLog(DataLogPath));
            errors.AddRange(dataErrors);
            session.TelemetryRows = rows;
            session.DataLogPath = DataLogPath;

            // Step 2: Parse AlarmLog (optional)
            if (HasAlarmLog)
            {
                AnalysisStatus = "Parsing AlarmLog…";
                AnalysisProgress = 20;
                var (alarms, alarmErrors) = await Task.Run(() => _parser.ParseAlarmLog(AlarmLogPath));
                errors.AddRange(alarmErrors);
                session.AlarmEvents = alarms;
                session.AlarmLogPath = AlarmLogPath;
            }

            // Step 3: Data quality
            AnalysisStatus = "Checking data quality…";
            AnalysisProgress = 35;
            session.DataQuality = await Task.Run(() => _dqService.Analyze(session.TelemetryRows, session.AlarmEvents));
            QualityReport = session.DataQuality;
            ShowQualityReport = true;

            if (!session.DataQuality.IsAdequate)
            {
                AnalysisStatus = $"Data quality score: {session.DataQuality.Score}/100 — {session.DataQuality.Grade}";
                ParseErrors = errors.Concat(session.DataQuality.Errors).ToList();
                IsAnalyzing = false;
                AnalysisReady = true;
                return;
            }

            // Step 4: Feature extraction
            AnalysisStatus = "Extracting engineering features…";
            AnalysisProgress = 55;
            session.Features = await Task.Run(() => _featureEngine.Extract(session.TelemetryRows, session.AlarmEvents));

            // Step 5: Health index
            AnalysisStatus = "Computing health index…";
            AnalysisProgress = 70;
            session.Health = await Task.Run(() => _healthIndex.Compute(session.Features, session.DataQuality));

            // Step 6: Issue detection (AI)
            AnalysisStatus = "Running issue detection (AI)…";
            AnalysisProgress = 88;
            try
            {
                session.Issues = await _issueAgent.DetectAsync(session.Features, session.DataQuality);
            }
            catch
            {
                AnalysisStatus = "AI unavailable — using rule-based detection.";
            }

            AnalysisProgress = 100;
            AnalysisStatus = $"Analysis complete — {session.Issues.Count} issues detected.";
            ParseErrors = errors;

            AnalysisCompleted?.Invoke(session);
        }
        catch (Exception ex)
        {
            ParseErrors = [$"Fatal error: {ex.Message}"];
            AnalysisStatus = "Analysis failed.";
        }
        finally
        {
            IsAnalyzing = false;
            UpdateAnalysisReady();
        }
    }

    public void Reset()
    {
        DataLogPath = string.Empty;
        AlarmLogPath = string.Empty;
        HasDataLog = false;
        HasAlarmLog = false;
        AnalysisReady = false;
        ShowQualityReport = false;
        QualityReport = null;
        ParseErrors = [];
        AnalysisStatus = string.Empty;
        AnalysisProgress = 0;
    }
}
