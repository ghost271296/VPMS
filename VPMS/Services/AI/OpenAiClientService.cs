using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace VPMS.Services.AI;

public class OpenAiClientService
{
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly float _temperature;
    private ChatClient? _client;
    private string _apiKey = string.Empty;

    public OpenAiClientService(IConfiguration config)
    {
        _model = config["OpenAI:Model"] ?? "gpt-4o";
        _maxTokens = int.Parse(config["OpenAI:MaxTokens"] ?? "4096");
        _temperature = float.Parse(config["OpenAI:Temperature"] ?? "0.2");

        // Load key from settings file, config, or environment — don't throw if empty
        var key = SettingsService.LoadApiKey()
                  ?? config["OpenAI:ApiKey"]
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                  ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(key))
            SetApiKey(key);
    }

    public bool IsConfigured => _client != null;

    public void SetApiKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        _apiKey = key;
        _client = new OpenAIClient(key).GetChatClient(_model);
        SettingsService.SaveApiKey(key);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        if (_client is null)
            throw new InvalidOperationException("OpenAI API key is not configured. Go to Settings to enter your key.");

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = _maxTokens,
            Temperature = _temperature
        };

        var response = await _client.CompleteChatAsync(messages, options, ct);
        return response.Value.Content[0].Text ?? string.Empty;
    }

    public async Task<string> CompleteWithRetryAsync(string systemPrompt, string userPrompt,
        int maxRetries = 3, CancellationToken ct = default)
    {
        int delay = 2000;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await CompleteAsync(systemPrompt, userPrompt, ct);
            }
            catch (InvalidOperationException)
            {
                throw;  // Don't retry missing-key errors
            }
            catch (Exception) when (attempt < maxRetries)
            {
                await Task.Delay(delay, ct);
                delay *= 2;
            }
        }
        return await CompleteAsync(systemPrompt, userPrompt, ct);
    }
}
