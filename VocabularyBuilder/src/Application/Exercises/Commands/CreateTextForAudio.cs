using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.Exercises.Commands;
public record CreateTextForAudioCommand(IEnumerable<string> Words, Language Language = Language.English) : IRequest<string>;
public class CreateTextForAudioCommandHandler : IRequestHandler<CreateTextForAudioCommand, string>
{
    // {0} is the word, {1} the language it is written in. Explanations stay in
    // English whatever the word's language - the audio is for an English-speaking
    // learner - so only the word itself changes language.
    const string Prompt = "For the {1} word \"{0}\" do the following:\n\n1. Write the word and a full definition for it, but in plain English.\n\n2. Provide 2-4 collocations most often used with this word (if it is a collocation with prepositions - include the most often meaningful word that comes with it as well). Very concisely explain the meaning and when to use each of them, and give 2-3 samples. Start EACH example by writing the collocation used in the example (each example sentence will be introduced/preceded by the collocation itself).\n\nIf the word has more than 1 definition and they are different dramatically, repeat steps 1-2 for each definition.\n\nAfter each step write 1 line that consists only of the word followed by an ellipsis.\n\n";

    // Examples are written in the word's own language, so say which that is.
    const string NonEnglishExampleInstruction = "Write every example sentence and collocation in {1}, followed by its English translation in parentheses.\n\n";

    private readonly IGptClient _gptClient;

    public CreateTextForAudioCommandHandler(IGptClient gptClient)
    {
        _gptClient = gptClient;
    }

    public async Task<string> Handle(CreateTextForAudioCommand request, CancellationToken cancellationToken)
    {
        var template = request.Language == Language.English
            ? Prompt
            : Prompt + NonEnglishExampleInstruction;

        foreach (var word in request.Words)
        {
            var message = String.Format(template, word, request.Language);
            var response = await _gptClient.SendMessageAsync(message);
            Console.WriteLine(response);
        }
        return await Task.FromResult("null");
    }
}
