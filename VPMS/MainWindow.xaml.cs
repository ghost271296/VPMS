using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using VPMS.Models;
using VPMS.ViewModels;
using VPMS.Views;

namespace VPMS;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly UploadView _uploadView;
    private readonly IssuesView _issuesView;
    private readonly RcaView _rcaView;
    private readonly QueryView _queryView;
    private readonly FishboneView _fishboneView;
    private readonly ActionsView _actionsView;
    private readonly PredictionView _predictionView;
    private readonly ReportView _reportView;

    public MainWindow(MainViewModel vm, UploadView uploadView, IssuesView issuesView,
        RcaView rcaView, QueryView queryView, FishboneView fishboneView,
        ActionsView actionsView, PredictionView predictionView, ReportView reportView)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        _uploadView = uploadView;
        _issuesView = issuesView;
        _rcaView = rcaView;
        _queryView = queryView;
        _fishboneView = fishboneView;
        _actionsView = actionsView;
        _predictionView = predictionView;
        _reportView = reportView;

        // Bind view models to views
        _uploadView.DataContext = vm.UploadVm;
        _issuesView.DataContext = vm.IssuesVm;
        _rcaView.DataContext = vm.RcaVm;
        _queryView.DataContext = vm.QueryVm;
        _fishboneView.DataContext = vm.FishboneVm;
        _actionsView.DataContext = vm.ActionsVm;
        _predictionView.DataContext = vm.PredictionVm;
        _reportView.DataContext = vm.ReportVm;

        // Wire inter-stage navigation
        vm.IssuesVm.Gate1Passed += OnGate1Passed;
        vm.RcaVm.ReadyForQuery += OnReadyForQuery;
        vm.QueryVm.ReadyForFishbone += OnReadyForFishbone;
        vm.FishboneVm.Gate2Passed += OnGate2Passed;
        vm.ActionsVm.ReadyForPrediction += OnReadyForPrediction;
        vm.PredictionVm.ReadyForReport += OnReadyForReport;

        // Listen for stage changes
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentStage))
                UpdateView();
        };

        // Sync nav list
        NavList.SelectionChanged += (_, _) => { };

        UpdateView();
        NavList.SelectedIndex = 0;
    }

    private void UpdateView()
    {
        MainContent.Content = _vm.CurrentStage switch
        {
            SessionStage.Upload => _uploadView,
            SessionStage.Issues => _issuesView,
            SessionStage.Rca => _rcaView,
            SessionStage.Query => _queryView,
            SessionStage.Fishbone => _fishboneView,
            SessionStage.Actions => _actionsView,
            SessionStage.Prediction => _predictionView,
            SessionStage.Report => _reportView,
            _ => _uploadView
        };

        NavList.SelectedIndex = (int)_vm.CurrentStage;
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var win = App.Services.GetRequiredService<SettingsWindow>();
        win.Owner = this;
        win.ShowDialog();
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedIndex >= 0)
        {
            var target = (SessionStage)NavList.SelectedIndex;
            // Only allow backward navigation or to already-reached stages
            if (target <= _vm.CurrentStage)
                _vm.NavigateTo(target);
            else
                NavList.SelectedIndex = (int)_vm.CurrentStage;
        }
    }

    private void OnGate1Passed(DiagnosticSession session)
    {
        _rcaView.DataContext = _vm.RcaVm;
        _vm.RcaVm.LoadSession(session);
        _vm.AdvanceStage();
    }

    private void OnReadyForQuery(DiagnosticSession session)
    {
        _vm.QueryVm.LoadSession(session);
        _vm.AdvanceStage();
    }

    private void OnReadyForFishbone(DiagnosticSession session)
    {
        _vm.FishboneVm.LoadSession(session);
        _vm.AdvanceStage();
    }

    private void OnGate2Passed(DiagnosticSession session)
    {
        _vm.ActionsVm.LoadSession(session);
        _vm.AdvanceStage();
    }

    private void OnReadyForPrediction(DiagnosticSession session)
    {
        _vm.PredictionVm.LoadSession(session);
        _vm.AdvanceStage();
    }

    private void OnReadyForReport(DiagnosticSession session)
    {
        _vm.ReportVm.LoadSession(session);
        _vm.AdvanceStage();
    }
}
