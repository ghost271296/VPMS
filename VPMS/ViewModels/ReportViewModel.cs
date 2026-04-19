using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VPMS.Models;
using VPMS.Services;

namespace VPMS.ViewModels;

public partial class ReportViewModel : ObservableObject
{
    private readonly ReportService _reportService;
    private DiagnosticSession? _session;

    [ObservableProperty] private string _reportNotes = string.Empty;
    [ObservableProperty] private bool _includeExecutiveSummary = true;
    [ObservableProperty] private bool _includeHealthDashboard = true;
    [ObservableProperty] private bool _includeIssues = true;
    [ObservableProperty] private bool _includeRca = true;
    [ObservableProperty] private bool _includeFishbone = true;
    [ObservableProperty] private bool _includeActions = true;
    [ObservableProperty] private bool _includePrediction = true;
    [ObservableProperty] private bool _includeAppendix = true;

    [ObservableProperty] private bool _isExporting;
    [ObservableProperty] private string _exportStatus = string.Empty;
    [ObservableProperty] private string _lastExportPath = string.Empty;

    public ReportViewModel(ReportService reportService) => _reportService = reportService;

    public void LoadSession(DiagnosticSession session)
    {
        _session = session;
        ExportStatus = "Report ready. Choose format and export.";
    }

    private ReportOptions BuildOptions()
    {
        var sections = new HashSet<ReportSection>();
        if (IncludeExecutiveSummary) sections.Add(ReportSection.ExecutiveSummary);
        if (IncludeHealthDashboard) sections.Add(ReportSection.HealthDashboard);
        if (IncludeIssues) sections.Add(ReportSection.DetectedIssues);
        if (IncludeRca) sections.Add(ReportSection.RootCauseAnalysis);
        if (IncludeFishbone) sections.Add(ReportSection.FishboneDiagram);
        if (IncludeActions) sections.Add(ReportSection.CorrectiveActions);
        if (IncludePrediction) sections.Add(ReportSection.PredictiveRisks);
        if (IncludeAppendix) sections.Add(ReportSection.EngineeringAppendix);
        return new ReportOptions { IncludedSections = sections };
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (_session is null) return;
        var dlg = new SaveFileDialog { Filter = "PDF Files (*.pdf)|*.pdf", FileName = $"VPMS_{_session.SiteId}_{DateTime.Now:yyyyMMdd}.pdf" };
        if (dlg.ShowDialog() != true) return;

        _session.ReportNotes = ReportNotes;
        IsExporting = true;
        ExportStatus = "Generating PDF…";
        try
        {
            LastExportPath = await _reportService.ExportPdfAsync(_session, dlg.FileName, BuildOptions());
            ExportStatus = $"PDF saved: {LastExportPath}";
        }
        catch (Exception ex) { ExportStatus = $"PDF export failed: {ex.Message}"; }
        finally { IsExporting = false; }
    }

    [RelayCommand]
    private async Task ExportDocxAsync()
    {
        if (_session is null) return;
        var dlg = new SaveFileDialog { Filter = "Word Files (*.docx)|*.docx", FileName = $"VPMS_{_session.SiteId}_{DateTime.Now:yyyyMMdd}.docx" };
        if (dlg.ShowDialog() != true) return;

        _session.ReportNotes = ReportNotes;
        IsExporting = true;
        ExportStatus = "Generating DOCX…";
        try
        {
            LastExportPath = await _reportService.ExportDocxAsync(_session, dlg.FileName);
            ExportStatus = $"DOCX saved: {LastExportPath}";
        }
        catch (Exception ex) { ExportStatus = $"DOCX export failed: {ex.Message}"; }
        finally { IsExporting = false; }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (_session is null) return;
        var dlg = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = $"VPMS_{_session.SiteId}_{DateTime.Now:yyyyMMdd}.csv" };
        if (dlg.ShowDialog() != true) return;

        IsExporting = true;
        ExportStatus = "Generating CSV…";
        try
        {
            LastExportPath = await _reportService.ExportCsvAsync(_session, dlg.FileName);
            ExportStatus = $"CSV saved: {LastExportPath}";
        }
        catch (Exception ex) { ExportStatus = $"CSV export failed: {ex.Message}"; }
        finally { IsExporting = false; }
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (_session is null) return;
        var dlg = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", FileName = $"VPMS_{_session.SiteId}_{DateTime.Now:yyyyMMdd}.json" };
        if (dlg.ShowDialog() != true) return;

        IsExporting = true;
        ExportStatus = "Generating JSON…";
        try
        {
            LastExportPath = await _reportService.ExportJsonAsync(_session, dlg.FileName);
            ExportStatus = $"JSON saved: {LastExportPath}";
        }
        catch (Exception ex) { ExportStatus = $"JSON export failed: {ex.Message}"; }
        finally { IsExporting = false; }
    }

    [RelayCommand]
    private void OpenExportFolder()
    {
        if (string.IsNullOrEmpty(LastExportPath)) return;
        var dir = System.IO.Path.GetDirectoryName(LastExportPath);
        if (dir != null && System.IO.Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", dir);
    }
}
