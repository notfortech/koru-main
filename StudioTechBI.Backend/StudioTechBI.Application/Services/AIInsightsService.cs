using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class AIInsightsService
{
    private readonly IConfiguration _config;

    public AIInsightsService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<string> GenerateInsights(string period)
    {
        var apiKey = _config["OpenAI:ApiKey"];
        var model = _config["OpenAI:Model"];

        var data = period.ToLower() switch
        {
            "weekly" => "Revenue: 12000, Expenses: 8000, Profit: 4000",
            "monthly" => "Revenue: 50000, Expenses: 42000, Profit: 8000",
            _ => throw new Exception("Invalid period")
        };

        using var client = new HttpClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        var request = new
        {
            model = model,
            messages = new[]
            {
                new {
                    role = "user",
                    content = $@"
You are a financial analyst.

Analyse:

{data}

Return:

Summary
Risks
Opportunities
Recommendations"
                }
            },
            temperature = 0.3
        };

        var response = await client.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
            )
        );

        var json = await response.Content.ReadAsStringAsync();

        var result = JsonDocument.Parse(json);

        return result
            .RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }
}