using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace VPMS.Services.AI;

public class OpenAiClientService
{
    private readonly ChatClient _client;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly float _temperature;

    public OpenAiClientService(IConfiguration config)
    {
        var apiKey = config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        _model = config["OpenAI:Model"] ?? "gpt-4o";
        _maxTokens = int.Parse(config["OpenAI:MaxTokens"] ?? "4096");
        _temperature = float.Parse(config["OpenAI:Temperature"] ?? "0.2");

        var openAiClient = new OpenAIClient(apiKey);
        _client = openAiClient.GetChatClient(_model);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty);

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
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
            catch (Exception) when (attempt < maxRetries)
            {
                await Task.Delay(delay, ct);
                delay *= 2;
            }
        }
        return await CompleteAsync(systemPrompt, userPrompt, ct);
    }
}
