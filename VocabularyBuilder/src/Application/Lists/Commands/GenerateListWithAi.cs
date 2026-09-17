using System.Text.Json;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Lists.Queries;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Lists.Commands;

public enum QuantityRange
{
    Small = 0,  // 3-5 items
    Medium = 1, // 5-7 items
    Large = 2   // 7+ (10 items)
}

public record GenerateListWithAiCommand : IRequest<GeneratedListPreviewDto>
{
    public string Prompt { get; init; } = string.Empty;
    public QuantityRange QuantityRange { get; init; } = QuantityRange.Medium;
    public Language Language { get; init; } = Language.English;
}

public class GeneratedListPreviewDto
{
    public string Title { get; init; } = string.Empty;
    public List<string> Items { get; init; } = new();
}

public class GenerateListWithAiCommandHandler : IRequestHandler<GenerateListWithAiCommand, GeneratedListPreviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IGptClient _gptClient;

    public GenerateListWithAiCommandHandler(IApplicationDbContext context, IGptClient gptClient)
    {
        _context = context;
        _gptClient = gptClient;
    }

    public async Task<GeneratedListPreviewDto> Handle(GenerateListWithAiCommand request, CancellationToken cancellationToken)
    {
        // Determine the number of items based on quantity range
        var (minItems, maxItems) = GetItemRange(request.QuantityRange);
        
        // Build the prompt for GPT
        var gptPrompt = BuildGptPrompt(request.Prompt, minItems, maxItems, request.Language);
        
        // Call GPT to generate items
        var gptResponse = await _gptClient.SendMessageAsync(gptPrompt);
        
        if (string.IsNullOrWhiteSpace(gptResponse))
        {
            throw new InvalidOperationException("GPT did not return a valid response");
        }
        
        // Parse the JSON response
        List<string> items;
        try
        {
            items = JsonSerializer.Deserialize<List<string>>(gptResponse) 
                ?? throw new InvalidOperationException("Failed to parse GPT response");
                
            if (items.Count == 0)
            {
                throw new InvalidOperationException("GPT returned an empty list");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse GPT response as JSON: {ex.Message}. Response: {gptResponse}");
        }
        
        // Generate list title from prompt (capitalize first letter)
        var title = char.ToUpper(request.Prompt[0]) + request.Prompt.Substring(1);
        
        // Return preview without saving to database
        return new GeneratedListPreviewDto
        {
            Title = title,
            Items = items.Select(i => i.Trim()).ToList()
        };
    }
    
    private static (int min, int max) GetItemRange(QuantityRange quantityRange)
    {
        return quantityRange switch
        {
            QuantityRange.Small => (3, 5),
            QuantityRange.Medium => (5, 7),
            QuantityRange.Large => (10, 10),
            _ => (5, 7)
        };
    }
    
    private static string BuildGptPrompt(string userPrompt, int minItems, int maxItems, Language language)
    {
        var itemCount = maxItems == minItems ? maxItems.ToString() : $"{minItems}-{maxItems}";
        var languageName = GetLanguageName(language);
        var exams = GetExams(language);
        
        return $@"Generate {itemCount} items for: ""{userPrompt}"".

Instructions:
1. Return ONLY a valid JSON array of strings (no additional text, explanations, or markdown)
2. Each item should be a single word or phrase
3. Items should be common and actually in use in everyday {languageName}
4. Focus on vocabulary that demonstrates good language skills in tests like {exams}
5. Include practical phrases/words suitable for writing and speaking test sections
6. Items can be somewhat typical for these tests (even slightly cliche), but must be genuinely useful
7. Do not include numbering, bullets, or other formatting in the items themselves
8. Ensure items are diverse and non-repetitive
9. Every item must be written in {languageName}

Example format: [""item1"", ""item2"", ""item3""]

Generate the list now:";
    }

    private static string GetLanguageName(Language language)
    {
        return language switch
        {
            Language.French => "French",
            _ => "English"
        };
    }

    /// <summary>
    /// Proficiency tests the generated vocabulary should suit. Both sets are the
    /// ones that matter for Canadian immigration.
    /// </summary>
    private static string GetExams(Language language)
    {
        return language switch
        {
            Language.French => "TEF Canada/TCF Canada",
            _ => "CELPIP/IELTS"
        };
    }
}
