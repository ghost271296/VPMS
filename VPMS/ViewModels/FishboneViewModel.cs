using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using VPMS.Models;
using VPMS.Services.AI;

namespace VPMS.ViewModels;

public partial class FishboneViewModel : ObservableObject
{
    private readonly CorrectiveActionAgent _actionAgent;
    private DiagnosticSession? _session;

    [ObservableProperty] private FishboneDiagram? _fishbone;
    [ObservableProperty] private ObservableCollection<FishboneNode> _branches = [];
    [ObservableProperty] private string _engineerNotes = string.Empty;
    [ObservableProperty] private bool _isGeneratingActions;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isConfirmed;

    public event Action<DiagnosticSession>? Gate2Passed;

    public FishboneViewModel(CorrectiveActionAgent actionAgent) => _actionAgent = actionAgent;

    public void LoadSession(DiagnosticSession session)
    {
        _session = session;
        Fishbone = session.Fishbone;
        if (Fishbone != null)
            Branches = new ObservableCollection<FishboneNode>(Fishbone.Branches);
        StatusMessage = "Review the fishbone diagram. Edit nodes if needed, then confirm.";
    }

    [RelayCommand]
    private void AddNode(FishboneBranch branch)
    {
        var node = new FishboneNode
        {
            Label = "New cause",
            Branch = branch,
            Confidence = 1.0,
            IsRootCause = false
        };
        Branches.Add(node);
        Fishbone?.Branches.Add(node);
    }

    [RelayCommand]
    private void RemoveNode(FishboneNode? node)
    {
        if (node is null) return;
        Branches.Remove(node);
        Fishbone?.Branches.Remove(node);
    }

    [RelayCommand]
    private async Task ConfirmAndProceedAsync()
    {
        if (_session is null || Fishbone is null) return;

        Fishbone.IsConfirmedByEngineer = true;
        Fishbone.EngineerNotes = EngineerNotes;
        _session.Fishbone = Fishbone;
        _session.Gate2Passed = true;
        IsConfirmed = true;

        IsGeneratingActions = true;
        StatusMessage = "Generating corrective action plan…";
        try
        {
            _session.Actions = await _actionAgent.GenerateAsync(
                _session.Fishbone, _session.RootCauses, _session.Issues);
        }
        catch { _session.Actions = []; }
        finally { IsGeneratingActions = false; }

        Gate2Passed?.Invoke(_session);
    }

    public List<FishboneBranch> BranchOptions { get; } = [.. Enum.GetValues<FishboneBranch>()];
}
