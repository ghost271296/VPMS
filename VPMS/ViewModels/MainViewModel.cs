using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPMS.Models;

namespace VPMS.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private SessionStage _currentStage = SessionStage.Upload;
    [ObservableProperty] private DiagnosticSession _session = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyMessage = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // Child view models
    public UploadViewModel UploadVm { get; }
    public IssuesViewModel IssuesVm { get; }
    public RcaViewModel RcaVm { get; }
    public QueryViewModel QueryVm { get; }
    public FishboneViewModel FishboneVm { get; }
    public ActionsViewModel ActionsVm { get; }
    public PredictionViewModel PredictionVm { get; }
    public ReportViewModel ReportVm { get; }

    public bool CanGoBack => CurrentStage > SessionStage.Upload && !IsBusy;
    public bool IsUploadStage => CurrentStage == SessionStage.Upload;
    public bool IsIssuesStage => CurrentStage == SessionStage.Issues;
    public bool IsRcaStage => CurrentStage == SessionStage.Rca;
    public bool IsQueryStage => CurrentStage == SessionStage.Query;
    public bool IsFishboneStage => CurrentStage == SessionStage.Fishbone;
    public bool IsActionsStage => CurrentStage == SessionStage.Actions;
    public bool IsPredictionStage => CurrentStage == SessionStage.Prediction;
    public bool IsReportStage => CurrentStage == SessionStage.Report;

    public int StageProgress => (int)CurrentStage;
    public int TotalStages => Enum.GetValues<SessionStage>().Length - 1;
    public double ProgressPct => (double)StageProgress / TotalStages * 100;

    // Stage nav labels
    public static IReadOnlyList<string> StageLabels { get; } =
        ["Upload", "Issues", "RCA", "Query", "Fishbone", "Actions", "Prediction", "Report"];

    public MainViewModel(UploadViewModel uploadVm, IssuesViewModel issuesVm, RcaViewModel rcaVm,
        QueryViewModel queryVm, FishboneViewModel fishboneVm, ActionsViewModel actionsVm,
        PredictionViewModel predictionVm, ReportViewModel reportVm)
    {
        UploadVm = uploadVm;
        IssuesVm = issuesVm;
        RcaVm = rcaVm;
        QueryVm = queryVm;
        FishboneVm = fishboneVm;
        ActionsVm = actionsVm;
        PredictionVm = predictionVm;
        ReportVm = reportVm;

        // Wire upload completion
        UploadVm.AnalysisCompleted += OnUploadCompleted;
    }

    private void OnUploadCompleted(DiagnosticSession session)
    {
        Session = session;
        IssuesVm.LoadSession(session);
        NavigateTo(SessionStage.Issues);
    }

    [RelayCommand]
    private void NavigateBack()
    {
        if (CurrentStage > SessionStage.Upload)
            NavigateTo(CurrentStage - 1);
    }

    public void NavigateTo(SessionStage stage)
    {
        CurrentStage = stage;
        Session.CurrentStage = stage;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsUploadStage));
        OnPropertyChanged(nameof(IsIssuesStage));
        OnPropertyChanged(nameof(IsRcaStage));
        OnPropertyChanged(nameof(IsQueryStage));
        OnPropertyChanged(nameof(IsFishboneStage));
        OnPropertyChanged(nameof(IsActionsStage));
        OnPropertyChanged(nameof(IsPredictionStage));
        OnPropertyChanged(nameof(IsReportStage));
        OnPropertyChanged(nameof(StageProgress));
        OnPropertyChanged(nameof(ProgressPct));
    }

    public void AdvanceStage()
    {
        if (CurrentStage < SessionStage.Report)
            NavigateTo(CurrentStage + 1);
    }

    public void SetBusy(string message)
    {
        IsBusy = true;
        BusyMessage = message;
        HasError = false;
    }

    public void ClearBusy(string status = "")
    {
        IsBusy = false;
        BusyMessage = string.Empty;
        StatusMessage = status;
    }

    public void ShowError(string message)
    {
        HasError = true;
        ErrorMessage = message;
        IsBusy = false;
    }

    [RelayCommand]
    private void DismissError() => HasError = false;

    [RelayCommand]
    private void NewSession()
    {
        Session = new DiagnosticSession();
        NavigateTo(SessionStage.Upload);
        UploadVm.Reset();
        StatusMessage = "New session started.";
    }

    partial void OnCurrentStageChanged(SessionStage value)
    {
        OnPropertyChanged(nameof(CanGoBack));
    }
}
