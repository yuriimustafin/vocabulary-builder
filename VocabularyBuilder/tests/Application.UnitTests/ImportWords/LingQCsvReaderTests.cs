using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Application.ImportWords;

namespace VocabularyBuilder.Application.UnitTests.ImportWords;

public class LingQCsvReaderTests
{
    private const string Header =
        "term,phrase,tag1,tag2,tag3,tag4,meaninglanguage1,meaning1,meaninglanguage2,meaning2";

    [Test]
    public void ShouldReadTheTermAndTheSentenceItWasMetIn()
    {
        var rows = LingQCsvReader.Read(
            Header + "\nune conférence,J'écoute une conférence.,preply,,,,en,conference,,\n");

        rows.Should().ContainSingle();
        rows[0].Term.Should().Be("une conférence");
        rows[0].Phrase.Should().Be("J'écoute une conférence.");
    }

    [Test]
    public void ShouldLeaveThePhraseUnsetWhenTheExportHasNone()
    {
        var rows = LingQCsvReader.Read(Header + "\nla vente,,preply,,,,en,the sale,,\n");

        rows[0].Phrase.Should().BeNull();
    }

    /// <summary>
    /// The one complication the export actually contains: a sentence with a comma in it,
    /// which would otherwise be read as the start of the next column.
    /// </summary>
    [Test]
    public void ShouldKeepAQuotedFieldWhole()
    {
        var rows = LingQCsvReader.Read(
            Header + "\nje vais très bien merci,\"Je vais très bien, merci.\",,,,,en,I'm very well,,\n");

        rows[0].Term.Should().Be("je vais très bien merci");
        rows[0].Phrase.Should().Be("Je vais très bien, merci.");
    }

    [Test]
    public void ShouldReadADoubledQuoteAsOne()
    {
        var rows = LingQCsvReader.Read(Header + "\nun mot,\"il a dit \"\"oui\"\"\",,,,,en,word,,\n");

        rows[0].Phrase.Should().Be("il a dit \"oui\"");
    }

    [Test]
    public void ShouldSkipTheHeader()
    {
        LingQCsvReader.Read(Header + "\nune main,,preply,,,,en,hand,,\n")
            .Should().ContainSingle().Which.Term.Should().Be("une main");
    }

    /// <summary>
    /// A file someone has already trimmed should not quietly lose its first word.
    /// </summary>
    [Test]
    public void ShouldReadAFileThatHasNoHeader()
    {
        var rows = LingQCsvReader.Read("une main,,preply,,,,en,hand,,\nun pied,,preply,,,,en,foot,,\n");

        rows.Should().HaveCount(2);
        rows[0].Term.Should().Be("une main");
    }

    [Test]
    public void ShouldIgnoreBlankAndTermlessLines()
    {
        var rows = LingQCsvReader.Read(Header + "\nune main,,,,,,,,,\n\n,,,,,,,,,\nun pied,,,,,,,,,\n");

        rows.Select(r => r.Term).Should().Equal("une main", "un pied");
    }

    [Test]
    public void ShouldReadTheLastRowWithoutATrailingNewline()
    {
        LingQCsvReader.Read(Header + "\nune main,,preply,,,,en,hand,,")
            .Should().ContainSingle();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ShouldReadAnEmptyFileAsNoRows(string content)
    {
        LingQCsvReader.Read(content).Should().BeEmpty();
    }
}
