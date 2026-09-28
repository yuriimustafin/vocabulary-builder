using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.Ai;

namespace VocabularyBuilder.Application.UnitTests.ImportWords;

public class GptVocabularyAnalyzerTests
{
    /// <summary>
    /// Answers the prompts with whatever the test sets, and records what it was asked.
    /// </summary>
    private class StubGptClient : IGptClient
    {
        private readonly Queue<string?> _responses;

        public StubGptClient(params string?[] responses)
        {
            _responses = new Queue<string?>(responses);
        }

        public List<string> Prompts { get; } = new();

        public Task<string?> SendMessageAsync(string message)
        {
            Prompts.Add(message);
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : null);
        }
    }

    private static GptVocabularyAnalyzer Analyzer(params string?[] responses) =>
        new(new StubGptClient(responses));

    [Test]
    public async Task ShouldReadTheItemsExtractedFromNotes()
    {
        var analyzer = Analyzer(@"[""un point de vue"", ""migrer"", ""une oeuvre d'art""]");

        var items = await analyzer.ExtractItemsAsync("un point de vue=viewpoint", Language.French);

        items.Should().Equal("un point de vue", "migrer", "une oeuvre d'art");
    }

    /// <summary>
    /// A model wraps its answer in a markdown fence about as often as not.
    /// </summary>
    [Test]
    public async Task ShouldReadAnAnswerWrappedInAMarkdownFence()
    {
        var analyzer = Analyzer("Here you go:\n```json\n[\"un pas\", \"un serpent\"]\n```\nHope that helps.");

        var items = await analyzer.ExtractItemsAsync("un pas=step", Language.French);

        items.Should().Equal("un pas", "un serpent");
    }

    [Test]
    public async Task ShouldDropBlanksAndRepeatsFromTheExtractedItems()
    {
        var analyzer = Analyzer(@"[""un pas"", """", ""  "", ""un pas"", ""Un Pas"", ""un serpent""]");

        var items = await analyzer.ExtractItemsAsync("notes", Language.French);

        items.Should().Equal("un pas", "un serpent");
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task ShouldNotAskAboutEmptyNotes(string notes)
    {
        var client = new StubGptClient();

        var items = await new GptVocabularyAnalyzer(client).ExtractItemsAsync(notes, Language.French);

        items.Should().BeEmpty();
        client.Prompts.Should().BeEmpty();
    }

    [Test]
    public async Task ShouldReadTheLemmasReturnedForTerms()
    {
        var analyzer = Analyzer(
            @"[{""term"":""vous allez"",""lemma"":""aller"",""reason"":null},
               {""term"":""il fait beau"",""lemma"":null,""reason"":""Expression""}]");

        var answers = await analyzer.ResolveLemmasAsync(new[] { "vous allez", "il fait beau" }, Language.French);

        answers.Should().HaveCount(2);
        answers[0].Lemma.Should().Be("aller");
        answers[1].Lemma.Should().BeNull();
        answers[1].SkipReason.Should().Be("Expression");
    }

    /// <summary>
    /// Answers are paired by term, so an entry the model drops must not shift every later
    /// answer onto the wrong word.
    /// </summary>
    [Test]
    public async Task ShouldPairAnswersByTermRatherThanByPosition()
    {
        var analyzer = Analyzer(@"[{""term"":""nous chantons"",""lemma"":""chanter""}]");

        var answers = await analyzer.ResolveLemmasAsync(
            new[] { "vous allez", "nous chantons" },
            Language.French);

        answers.Single(a => a.SourceTerm == "nous chantons").Lemma.Should().Be("chanter");
        answers.Single(a => a.SourceTerm == "vous allez").Lemma.Should().BeNull();
    }

    [Test]
    public async Task ShouldReportEveryTermOfAnUnreadableBatchAsUnresolved()
    {
        var analyzer = Analyzer("I'm sorry, I can't help with that.");

        var answers = await analyzer.ResolveLemmasAsync(new[] { "vous allez", "tu chantes" }, Language.French);

        answers.Should().HaveCount(2);
        answers.Should().OnlyContain(a => a.Lemma == null);
        answers.Should().OnlyContain(a => a.SkipReason != null);
    }

    [Test]
    public async Task ShouldTreatAnEmptyLemmaAsNotAVocabularyItem()
    {
        var analyzer = Analyzer(@"[{""term"":""de rien"",""lemma"":"""",""reason"":null}]");

        var answers = await analyzer.ResolveLemmasAsync(new[] { "de rien" }, Language.French);

        answers[0].Lemma.Should().BeNull();
        answers[0].SkipReason.Should().Be("Not a vocabulary item");
    }

    [Test]
    public async Task ShouldNotAskAboutAnEmptyTermList()
    {
        var client = new StubGptClient();

        var answers = await new GptVocabularyAnalyzer(client)
            .ResolveLemmasAsync(Array.Empty<string>(), Language.French);

        answers.Should().BeEmpty();
        client.Prompts.Should().BeEmpty();
    }

    /// <summary>
    /// A long list is split so the answer never runs out of room, which is what keeps a
    /// few hundred terms down to a handful of requests rather than one per word.
    /// </summary>
    [Test]
    public async Task ShouldSplitALongListIntoBatches()
    {
        var terms = Enumerable.Range(1, 95).Select(i => $"terme{i}").ToList();

        var responses = new[]
        {
            Batch(terms.Take(40)),
            Batch(terms.Skip(40).Take(40)),
            Batch(terms.Skip(80))
        };

        var client = new StubGptClient(responses);

        var answers = await new GptVocabularyAnalyzer(client).ResolveLemmasAsync(terms, Language.French);

        client.Prompts.Should().HaveCount(3);
        answers.Should().HaveCount(95);
        answers.Should().OnlyContain(a => a.Lemma != null);

        static string Batch(IEnumerable<string> batch) =>
            "[" + string.Join(",", batch.Select(t => $@"{{""term"":""{t}"",""lemma"":""{t}""}}")) + "]";
    }
}
