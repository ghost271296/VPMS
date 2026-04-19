using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using VPMS.Models;
using VPMS.Services.AI;

namespace VPMS.ViewModels;

public partial class QueryViewModel : ObservableObject
{
    private readonly FishboneAgent _fishboneAgent;
    private DiagnosticSession? _session;

    [ObservableProperty] private ObservableCollection<QueryItem> _queries = [];
    [ObservableProperty] private bool _isGeneratingFishbone;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public int AnsweredCount => Queries.Count(q => q.IsAnswered);
    public int TotalQueries => Queries.Count;
    public bool CanProceed => Queries.All(q => q.IsAnswered);

    public event Action<DiagnosticSession>? ReadyForFishbone;

    public QueryViewModel(FishboneAgent fishboneAgent) => _fishboneAgent = fishboneAgent;

    public void LoadSession(DiagnosticSession session)
    {
        _session = session;
        Queries = new ObservableCollection<QueryItem>(session.Queries);
        StatusMessage = $"{Queries.Count} targeted questions. Answer all to proceed.";
        OnPropertyChanged(nameof(AnsweredCount));
        OnPropertyChanged(nameof(TotalQueries));
    }

    public void NotifyAnswerChanged()
    {
        _session?.Queries.Clear();
        if (_session != null) _session.Queries.AddRange(Queries);
        OnPropertyChanged(nameof(AnsweredCount));
        OnPropertyChanged(nameof(CanProceed));
    }

    [RelayCommand]
    private async Task ProceedAsync()
    {
        if (_session is null) return;
        _session.Queries = Queries.ToList();

        IsGeneratingFishbone = true;
        StatusMessage = "Generating fishbone diagram…";
        try
        {
            _session.Fishbone = await _fishboneAgent.GenerateAsync(
                _session.RootCauses, _session.Issues, _session.Queries);
        }
        catch { _session.Fishbone = new FishboneDiagram { Effect = "UPS Fault" }; }
        finally { IsGeneratingFishbone = false; }

        ReadyForFishbone?.Invoke(_session);
    }
}
