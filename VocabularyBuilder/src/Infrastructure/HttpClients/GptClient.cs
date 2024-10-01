using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using VocabularyBuilder.Application.Ai;


namespace VocabularyBuilder.Infrastructure.HttpClients;
public class GptClient: IGptClient
{
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
        var requestData = new
        {
            model = "gpt-4o", 
            messages = new[] { new { role = "user", content = prompt } }
        };

        var jsonContent = JsonConvert.SerializeObject(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(apiURL, content);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error: {response.StatusCode}");
            return null;
        }

        var responseString = await response.Content.ReadAsStringAsync();
        return responseString;
    }
}
