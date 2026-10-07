using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using VocabularyBuilder.Application.Ai;


namespace VocabularyBuilder.Infrastructure.HttpClients;
public class GptClient: IGptClient, IDetailedGptClient
{
    private const string Model = "gpt-4o";

    private readonly HttpClient httpClient;
    private readonly string apiKey;
    private readonly string apiURL = "https://api.openai.com/v1/chat/completions";

    public GptClient(string apiKey)
    {
        this.apiKey = apiKey;
        httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<string?> SendMessageAsync(string prompt)
    {
        return (await CompleteAsync(prompt)).Content;
    }

    public async Task<GptCompletion> CompleteAsync(string prompt)
    {
        var requestData = new
        {
            model = Model,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var jsonContent = JsonConvert.SerializeObject(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(apiURL, content);
        var statusCode = (int)response.StatusCode;
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error: {response.StatusCode}");
            return new GptCompletion(null, Model, statusCode, $"{response.StatusCode}: {responseString}");
        }

        // Parse the OpenAI API response to extract the message content
        try
        {
            dynamic responseJson = JsonConvert.DeserializeObject(responseString)!;

            return new GptCompletion(
                responseJson?.choices?[0]?.message?.content?.ToString(),
                (string?)responseJson?.model?.ToString() ?? Model,
                statusCode,
                PromptTokens: (int?)responseJson?.usage?.prompt_tokens,
                CompletionTokens: (int?)responseJson?.usage?.completion_tokens);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing GPT response: {ex.Message}");
            // Return raw response as fallback
            return new GptCompletion(responseString, Model, statusCode, $"Unreadable response: {ex.Message}");
        }
    }
}
