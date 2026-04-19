using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using VPMS.Models;
using VPMS.Services.AI;

namespace VPMS.ViewModels;

public partial class ActionsViewModel : ObservableObject
{
    private readonly PredictiveAgent _predictiveAgent;
    private DiagnosticSession? _session;

    [ObservableProperty] private ObservableCollection<CorrectiveAction> _immediateActions = [];
    [ObservableProperty] private ObservableCollection<CorrectiveAction> _scheduledActions = [];
    [ObservableProperty] private ObservableCollection<CorrectiveAction> _monitorActions = [];
    [ObservableProperty] private bool _isGeneratingPrediction;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public event Action<DiagnosticSession>? ReadyForPrediction;

    public ActionsViewModel(PredictiveAgent predictiveAgent) => _predictiveAgent = predictiveAgent;

    public void LoadSession(DiagnosticSession session)
    {
        _session = session;
        ImmediateActions = new ObservableCollection<CorrectiveAction>(session.Actions.Where(a => a.Priority == ActionPriority.Immediate));
        ScheduledActions = new ObservableCollection<CorrectiveAction>(session.Actions.Where(a => a.Priority == ActionPriority.Scheduled));
        MonitorActions = new ObservableCollection<CorrectiveAction>(session.Actions.Where(a => a.Priority == ActionPriority.Monitor));
        StatusMessage = $"{ImmediateActions.Count} immediate, {ScheduledActions.Count} scheduled, {MonitorActions.Count} monitor actions.";
    }

    [RelayCommand]
    private void ToggleComplete(CorrectiveAction? action)
    {
        if (action is null) return;
        action.IsCompleted = !action.IsCompleted;
    }

    [RelayCommand]
    private async Task ProceedAsync()
    {
        if (_session is null) return;
        _session.Actions = ImmediateActions.Concat(ScheduledActions).Concat(MonitorActions).ToList();

        IsGeneratingPrediction = true;
        StatusMessage = "Computing 30/60/90-day failure risk predictions…";
        try
        {
            _session.PredictiveRisk = await _predictiveAgent.PredictAsync(
                _session.Features!, _session.Health!, _session.Issues);
        }
        catch { _session.PredictiveRisk = new PredictiveRiskReport(); }
        finally { IsGeneratingPrediction = false; }

        ReadyForPrediction?.Invoke(_session);
    }
}
