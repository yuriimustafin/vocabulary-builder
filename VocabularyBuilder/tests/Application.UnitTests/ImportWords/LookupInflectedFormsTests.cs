using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using VocabularyBuilder.Application.ImportWords.Queries;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Enums;
using VocabularyBuilder.Infrastructure.Data;

namespace VocabularyBuilder.Application.UnitTests.ImportWords;

/// <summary>
/// The frequency-data lemma lookup, against a real database because what it has to get
/// right is a query: which rows count as an answer and which are too ambiguous to use.
/// </summary>
public class LookupInflectedFormsTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private LookupInflectedFormsQueryHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _handler = new LookupInflectedFormsQueryHandler(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// A line of the frequency file: the lemma, and the forms recorded against it.
    /// </summary>
    private void GivenLemma(string lemma, params string[] forms)
    {
        var baseForm = new FrequencyWord { Headword = lemma, Language = Language.French, Frequency = 1000 };
        _context.FrequencyWords.Add(baseForm);

        foreach (var form in forms)
        {
            _context.FrequencyWords.Add(new FrequencyWord
            {
                Headword = form,
                Language = Language.French,
                BaseForm = baseForm
            });
        }

        _context.SaveChanges();
    }

    private Task<IReadOnlyDictionary<string, string>> Lookup(params string[] forms) =>
        _handler.Handle(
            new LookupInflectedFormsQuery { Forms = forms, Language = Language.French },
            CancellationToken.None);

    [Test]
    public async Task ShouldReduceAFormToItsLemma()
    {
        GivenLemma("aller", "allez", "allons", "vais", "vont");

        var lemmas = await Lookup("allez", "vont");

        lemmas["allez"].Should().Be("aller");
        lemmas["vont"].Should().Be("aller");
    }

    [Test]
    public async Task ShouldMatchRegardlessOfCase()
    {
        GivenLemma("aller", "allez");

        (await Lookup("Allez"))["allez"].Should().Be("aller");
    }

    /// <summary>
    /// "est" is also east and "neige" is also snow, so the data gives them a lemma row of
    /// their own. Nothing here can tell which was meant, so the term is left for the model,
    /// which at least has the pronoun in front of it.
    /// </summary>
    [Test]
    public async Task ShouldNotAnswerForAFormThatIsAlsoAWordInItsOwnRight()
    {
        GivenLemma("être", "sommes", "sont");
        GivenLemma("est");

        var lemmas = await Lookup("est", "sommes");

        lemmas.Should().NotContainKey("est");
        lemmas["sommes"].Should().Be("être");
    }

    [Test]
    public async Task ShouldNotAnswerForAFormItDoesNotHave()
    {
        GivenLemma("aller", "allez");

        (await Lookup("bricolez")).Should().BeEmpty();
    }

    /// <summary>
    /// Each language has its own frequency data, and a French import must not be answered
    /// from the English table.
    /// </summary>
    [Test]
    public async Task ShouldNotAnswerFromAnotherLanguage()
    {
        var english = new FrequencyWord { Headword = "go", Language = Language.English, Frequency = 1000 };
        _context.FrequencyWords.Add(english);
        _context.FrequencyWords.Add(new FrequencyWord
        {
            Headword = "goes",
            Language = Language.English,
            BaseForm = english
        });
        await _context.SaveChangesAsync();

        (await Lookup("goes")).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldAskNothingOfAnEmptyList()
    {
        (await Lookup()).Should().BeEmpty();
    }

    [Test]
    public async Task ShouldIgnoreBlankForms()
    {
        GivenLemma("aller", "allez");

        var lemmas = await Lookup("", "   ", "allez");

        lemmas.Should().ContainSingle().Which.Key.Should().Be("allez");
    }
}
