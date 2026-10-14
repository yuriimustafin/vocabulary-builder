using VocabularyBuilder.Application.Common.Models;
using VocabularyBuilder.Application.Imports.Queries;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Web.Endpoints;

/// <summary>The record of past imports, and the words each one brought in.</summary>
public class Imports : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        var group = app
            .MapGroup("/api/{lang}/imports")
            .WithGroupName("Imports")
            .WithTags("Imports")
            .WithOpenApi();

        group.MapGet("/", GetImports);
        group.MapGet("/{id}", GetImport);
    }

    public async Task<PaginatedList<ImportDto>> GetImports(
        ISender sender, string lang, int pageNumber = 1, int pageSize = 20)
    {
        return await sender.Send(new GetImportsQuery(ParseLanguage(lang), pageNumber, pageSize));
    }

    public async Task<IResult> GetImport(ISender sender, string lang, int id)
    {
        var import = await sender.Send(new GetImportDetailsQuery(id));
        return import != null ? Results.Ok(import) : Results.NotFound();
    }

    private static Language ParseLanguage(string lang)
    {
        return lang.ToLower() switch
        {
            "en" or "english" => Language.English,
            "fr" or "french" => Language.French,
            _ => Language.English
        };
    }
}
