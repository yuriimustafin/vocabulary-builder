using VocabularyBuilder.Application.Ai;

namespace VocabularyBuilder.Application.Exercises.Commands;
public record CreateTextForAudioCommand(IEnumerable<string> Words) : IRequest<string>;
public class CreateTextForAudioCommandHandler : IRequestHandler<CreateTextForAudioCommand, string>
{
    const string Prompt = "For the word \"{0}\" do the following:\n\n1. Write the word and a full definition for it, but in plain English.\n\n2. Provide 2-4 collocations most often used with this word (if it is a collocation with prepositions - include the most often meaningful word that comes with it as well). Very concisely explain the meaning and when to use each of them, and give 2-3 samples. Start EACH example by writing the collocation used in the example (each example sentence will be introduced/preceded by the collocation itself).\n\nIf the word has more than 1 definition and they are different dramatically, repeat steps 1-2 for each definition.\n\nAfter each step write 1 line that consists only of the word followed by an ellipsis.\n\n";

    private readonly IGptClient _gptClient;

    public CreateTextForAudioCommandHandler(IGptClient gptClient)
    {
        _gptClient = gptClient;
    }

    public async Task<string> Handle(CreateTextForAudioCommand request, CancellationToken cancellationToken)
    {
        foreach (var word in request.Words)
        {
            var message = String.Format(Prompt, word);
            var response = await _gptClient.SendMessageAsync(message);
            Console.WriteLine(response);
        }
        return await Task.FromResult("null");
    }
}

