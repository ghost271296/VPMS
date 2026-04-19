using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using VPMS.Models;
using VPMS.Services.AI;

namespace VPMS.ViewModels;

public partial class RcaViewModel : ObservableObject
{
    private readonly QueryAgent _queryAgent;
    private DiagnosticSession? _session;

    [ObservableProperty] private ObservableCollection<RootCause> _rootCauses = [];
    [ObservableProperty] private RootCause? _selectedCause;
    [ObservableProperty] private bool _isGeneratingQueries;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public event Action<DiagnosticSession>? ReadyForQuery;

    public RcaViewModel(QueryAgent queryAgent) => _queryAgent = queryAgent;

    public void LoadSession(DiagnosticSession session)
    {
        _session = session;
        RootCauses = new ObservableCollection<RootCause>(session.RootCauses);
        StatusMessage = $"{RootCauses.Count} root causes identified. Review then proceed to field questions.";
    }

    [RelayCommand]
    private async Task ProceedAsync()
    {
        if (_session is null) return;

        IsGeneratingQueries = true;
        StatusMessage = "Generating targeted field questions…";
        try
        {
            _session.Queries = await _queryAgent.GenerateAsync(
                _session.RootCauses, _session.Features!);
        }
        catch { _session.Queries = []; }
        finally { IsGeneratingQueries = false; }

        ReadyForQuery?.Invoke(_session);
    }
}
