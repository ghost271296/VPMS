using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using VPMS.Models;
using VPMS.Services.AI;

namespace VPMS.ViewModels;

public partial class IssuesViewModel : ObservableObject
{
    private readonly RcaAgent _rcaAgent;
    private DiagnosticSession? _session;

    [ObservableProperty] private ObservableCollection<DiagnosticIssue> _issues = [];
    [ObservableProperty] private DiagnosticIssue? _selectedIssue;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // Custom issue form
    [ObservableProperty] private string _newIssueTitle = string.Empty;
    [ObservableProperty] private string _newIssueDescription = string.Empty;
    [ObservableProperty] private IssueSeverity _newIssueSeverity = IssueSeverity.Medium;
    [ObservableProperty] private string _newIssueSubsystem = string.Empty;
    [ObservableProperty] private bool _showAddIssueForm;

    public event Action<DiagnosticSession>? Gate1Passed;

    public IssuesViewModel(RcaAgent rcaAgent) => _rcaAgent = rcaAgent;

    public void LoadSession(DiagnosticSession session)
    {
        _session = session;
        Issues = new ObservableCollection<DiagnosticIssue>(session.Issues);
        StatusMessage = $"{Issues.Count} issues detected. Review, approve or add custom issues, then proceed.";
    }

    [RelayCommand]
    private void ToggleApproval(DiagnosticIssue? issue)
    {
        if (issue is null) return;
        issue.IsApproved = !issue.IsApproved;
        OnPropertyChanged(nameof(Issues));
    }

    [RelayCommand]
    private void ShowAddForm() => ShowAddIssueForm = true;

    [RelayCommand]
    private void CancelAddForm()
    {
        ShowAddIssueForm = false;
        NewIssueTitle = string.Empty;
        NewIssueDescription = string.Empty;
    }

    [RelayCommand]
    private void AddCustomIssue()
    {
        if (string.IsNullOrWhiteSpace(NewIssueTitle)) return;

        var issue = new DiagnosticIssue
        {
            Title = NewIssueTitle,
            Description = NewIssueDescription,
            Severity = NewIssueSeverity,
            Subsystem = NewIssueSubsystem,
            Confidence = 1.0,
            Source = IssueSource.Engineer,
            IsCustom = true,
            IsApproved = true
        };
        Issues.Add(issue);
        _session?.Issues.Add(issue);

        ShowAddIssueForm = false;
        NewIssueTitle = string.Empty;
        NewIssueDescription = string.Empty;
        StatusMessage = $"Custom issue added. {Issues.Count(i => i.IsApproved)} issues approved.";
    }

    [RelayCommand]
    private void RemoveIssue(DiagnosticIssue? issue)
    {
        if (issue is null) return;
        Issues.Remove(issue);
        _session?.Issues.Remove(issue);
    }

    [RelayCommand]
    private async Task ProceedAsync()
    {
        if (_session is null) return;
        var approved = Issues.Where(i => i.IsApproved).ToList();
        if (approved.Count == 0)
        {
            StatusMessage = "Please approve at least one issue before proceeding.";
            return;
        }

        _session.Issues = Issues.ToList();
        _session.Gate1Passed = true;

        // Start RCA in background
        IsProcessing = true;
        StatusMessage = "Running Root Cause Analysis…";
        try
        {
            _session.RootCauses = await _rcaAgent.AnalyzeAsync(
                _session.Issues, _session.Features!, _session.DataQuality!);
        }
        catch { /* fallback handled in agent */ }
        finally { IsProcessing = false; }

        Gate1Passed?.Invoke(_session);
    }

    public List<IssueSeverity> SeverityOptions { get; } = [.. Enum.GetValues<IssueSeverity>()];
}
