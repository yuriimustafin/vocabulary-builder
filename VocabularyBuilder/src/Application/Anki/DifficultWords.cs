using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Exercises.Commands;
using VocabularyBuilder.Domain.Entities.Anki;

namespace VocabularyBuilder.Application.Anki;
//public record GetDifficultWordsCommand(int number) : IRequest<IEnumerable<string>>;
//public class DifficultWords : IRequestHandler<GetDifficultWordsCommand, IEnumerable<string>>
//{
//    private readonly IAnkiDbContext _context;

//    public DifficultWords(IAnkiDbContext context)
//    {
//        _context = context;
//    }

//    public async Task<IEnumerable<string>> Handle(GetDifficultWordsCommand request, CancellationToken cancellationToken)
//    {

//        var sql = "SELECT flds FROM notes WHERE id IN (SELECT nid FROM cards WHERE did = 1727889542420)";

//        var noteResults = await _context
//            .Set<AnkiCard>()
//            .FromSqlRaw(sql)
//            .ToListAsync(cancellationToken);

//        // Return just the string fields
//        return noteResults.Select(n => n.Flds);
//    }
//}
