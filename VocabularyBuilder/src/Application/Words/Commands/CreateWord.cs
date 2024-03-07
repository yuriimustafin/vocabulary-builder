using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.TodoLists.Commands.CreateTodoList;
using VocabularyBuilder.Domain.Samples.Entities;

namespace VocabularyBuilder.Application.Words.Commands;
public record CreateWordCommand(string Word) : IRequest<string>;
public class CreateWordCommandHandler : IRequestHandler<CreateWordCommand, string>
{
    private readonly IApplicationDbContext _context;

    public CreateWordCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> Handle(CreateWordCommand request, CancellationToken cancellationToken)
    {
        var entity = new Word() { Headword = request.Word };

        _context.Words.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Headword;
    }
}
