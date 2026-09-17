using Microsoft.Extensions.Options;

namespace VocabularyBuilder.Infrastructure.Parsers;

/// <summary>
/// Fetches WordReference pages. Abstracted so the parser can be exercised
/// against recorded pages without touching the network.
/// </summary>
public interface IWordReferencePageLoader
{
    Task<string?> GetPageAsync(string url);
}

public class HttpWordReferencePageLoader : IWordReferencePageLoader
{
    private readonly HttpClient _httpClient;

    public HttpWordReferencePageLoader(IOptions<WordReferenceOptions> options)
    {
        _httpClient = new HttpClient();
        // WordReference serves an empty page to clients without a browser UA
        _httpClient.DefaultRequestHeaders.Add("User-Agent", options.Value.UserAgent);
    }

    public async Task<string?> GetPageAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"WordReference returned {(int)response.StatusCode} for {url}");
            return null;
        }

        return await response.Content.ReadAsStringAsync();
    }
}

/// <summary>
/// Serves pages recorded under MockData/wordreference, keyed by the word in the
/// URL, so the E2E tests never reach the network.
/// </summary>
public class MockWordReferencePageLoader : IWordReferencePageLoader
{
    private readonly string _mockDataPath;

    public MockWordReferencePageLoader(string? mockDataPath = null)
    {
        _mockDataPath = mockDataPath
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MockData", "wordreference");
    }

    public Task<string?> GetPageAsync(string url)
    {
        var fileName = GetFileName(url);
        var path = Path.Combine(_mockDataPath, fileName);

        if (!File.Exists(path))
        {
            Console.WriteLine($"No recorded WordReference page at {path}");
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(File.ReadAllText(path));
    }

    /// <summary>
    /// "/fren/prendre" becomes "prendre.html", and a conjugation URL
    /// "/conj/frverbs.aspx?v=prendre" becomes "prendre.conj.html".
    /// </summary>
    private static string GetFileName(string url)
    {
        var uri = new Uri(url);

        if (uri.AbsolutePath.Contains("frverbs.aspx", StringComparison.OrdinalIgnoreCase))
        {
            var verb = System.Web.HttpUtility.ParseQueryString(uri.Query)["v"] ?? "unknown";
            return $"{verb}.conj.html";
        }

        var word = Uri.UnescapeDataString(uri.Segments.LastOrDefault() ?? "unknown");
        return $"{word}.html";
    }
}
