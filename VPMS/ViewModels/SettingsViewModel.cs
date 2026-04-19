using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPMS.Services;
using VPMS.Services.AI;

namespace VPMS.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly OpenAiClientService _ai;

    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isConfigured;
    [ObservableProperty] private bool _isTesting;

    public SettingsViewModel(OpenAiClientService ai)
    {
        _ai = ai;
        ApiKey = SettingsService.LoadApiKey() ?? string.Empty;
        IsConfigured = ai.IsConfigured;
        StatusMessage = ai.IsConfigured
            ? "API key loaded. AI pipeline is active."
            : "No API key configured. Enter your OpenAI API key below.";
    }

    [RelayCommand]
    private async Task SaveAndTestAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "API key cannot be empty.";
            return;
        }

        IsTesting = true;
        StatusMessage = "Saving and testing connection…";
        try
        {
            _ai.SetApiKey(ApiKey);

            // Quick connectivity test
            var response = await _ai.CompleteAsync(
                "You are a test assistant.",
                "Reply with the single word: OK",
                CancellationToken.None);

            IsConfigured = true;
            StatusMessage = response.Trim().StartsWith("OK", StringComparison.OrdinalIgnoreCase)
                ? "Connected successfully. AI pipeline is active."
                : "Key saved. Response received — connection OK.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            IsConfigured = false;
        }
        finally { IsTesting = false; }
    }

    [RelayCommand]
    private void ClearKey()
    {
        ApiKey = string.Empty;
        StatusMessage = "API key cleared. AI pipeline will use rule-based fallback.";
        IsConfigured = false;
    }
}
