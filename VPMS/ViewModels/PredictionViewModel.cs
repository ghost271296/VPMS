using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using VPMS.Models;

namespace VPMS.ViewModels;

public partial class PredictionViewModel : ObservableObject
{
    private DiagnosticSession? _session;

    [ObservableProperty] private PredictiveRiskReport? _riskReport;
    [ObservableProperty] private ObservableCollection<SubsystemRisk> _subsystemRisks = [];
    [ObservableProperty] private string _overallTrend = string.Empty;
    [ObservableProperty] private string _recommendation = string.Empty;

    public event Action<DiagnosticSession>? ReadyForReport;

    public void LoadSession(DiagnosticSession session)
    {
        _session = session;
        RiskReport = session.PredictiveRisk;
        if (RiskReport != null)
        {
            SubsystemRisks = new ObservableCollection<SubsystemRisk>(RiskReport.Subsystems);
            OverallTrend = RiskReport.OverallTrend;
            Recommendation = RiskReport.Recommendation;
        }
    }

    [RelayCommand]
    private void Proceed()
    {
        if (_session is null) return;
        ReadyForReport?.Invoke(_session);
    }
}
