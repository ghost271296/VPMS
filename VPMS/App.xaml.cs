using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using VPMS.Services;
using VPMS.Services.AI;
using VPMS.ViewModels;
using VPMS.Views;

namespace VPMS;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton<IConfiguration>(config);

        // Core services
        services.AddSingleton<ExcelParserService>();
        services.AddSingleton<DataQualityService>();
        services.AddSingleton<FeatureEngineService>();
        services.AddSingleton<HealthIndexService>();
        services.AddSingleton<ReportService>();

        // AI services
        services.AddSingleton<OpenAiClientService>();
        services.AddSingleton<IssueDetectionAgent>();
        services.AddSingleton<RcaAgent>();
        services.AddSingleton<QueryAgent>();
        services.AddSingleton<FishboneAgent>();
        services.AddSingleton<CorrectiveActionAgent>();
        services.AddSingleton<PredictiveAgent>();

        // ViewModels
        services.AddSingleton<UploadViewModel>();
        services.AddSingleton<IssuesViewModel>();
        services.AddSingleton<RcaViewModel>();
        services.AddSingleton<QueryViewModel>();
        services.AddSingleton<FishboneViewModel>();
        services.AddSingleton<ActionsViewModel>();
        services.AddSingleton<PredictionViewModel>();
        services.AddSingleton<ReportViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<UploadView>();
        services.AddSingleton<IssuesView>();
        services.AddSingleton<RcaView>();
        services.AddSingleton<QueryView>();
        services.AddSingleton<FishboneView>();
        services.AddSingleton<ActionsView>();
        services.AddSingleton<PredictionView>();
        services.AddSingleton<ReportView>();
        services.AddTransient<SettingsWindow>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
