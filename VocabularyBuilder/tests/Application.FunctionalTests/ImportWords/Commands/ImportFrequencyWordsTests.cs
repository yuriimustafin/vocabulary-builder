using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Application.Words.Queries;
using VocabularyBuilder.Domain.Entities.Frequency;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.FunctionalTests.ImportWords.Commands;

using static Testing;

public class ImportFrequencyWordsTests : BaseTestFixture
{
    [Test]
    public async Task ShouldImportFrequencyWordsFromFile()
    {
        // Arrange
        var testData = @"have/1315648 -> had,has,'ve,having
say/317317 -> said,says,saying
go/227247 -> going,went,gone";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testData);

        try
        {
            var command = new ImportFrequencyWordsCommand(tempFile);

            // Act
            var result = await SendAsync(command);

            // Assert
            result.Should().Be(3); // 3 lemmas imported

            var count = await CountAsync<FrequencyWord>();
            count.Should().Be(13); // 3 lemmas + 10 derived forms
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ShouldImportLemmaWithoutDerivedForms()
    {
        // Arrange
        var testData = "word/12345";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testData);

        try
        {
            var command = new ImportFrequencyWordsCommand(tempFile);

            // Act
            var result = await SendAsync(command);

            // Assert
            result.Should().Be(1);

            var lemma = (await FindAsync<FrequencyWord>(1))!;
            lemma.Headword.Should().Be("word");
            lemma.Frequency.Should().Be(12345);
            lemma.BaseFormId.Should().BeNull(); // Base forms have no parent
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ShouldHandleEmptyLines()
    {
        // Arrange
        var testData = @"word1/100

word2/200

";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testData);

        try
        {
            var command = new ImportFrequencyWordsCommand(tempFile);

            // Act
            var result = await SendAsync(command);

            // Assert
            result.Should().Be(2);
            var count = await CountAsync<FrequencyWord>();
            count.Should().Be(2);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ShouldImportFrenchWordsWithTheirOwnLanguage()
    {
        // Arrange - the shape produced by scripts/convert-lexique-to-frequency.js
        var testData = @"être/3223650 -> suis,sommes,sont,était
prendre/191383 -> prends,prend,pris";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testData);

        try
        {
            var command = new ImportFrequencyWordsCommand(tempFile, Language.French);

            // Act
            var result = await SendAsync(command);

            // Assert
            result.Should().Be(2);

            var count = await CountAsync<FrequencyWord>();
            count.Should().Be(9); // 2 lemmas + 7 derived forms

            var lemma = (await FindAsync<FrequencyWord>(1))!;
            lemma.Headword.Should().Be("être");
            lemma.Language.Should().Be(Language.French);
            lemma.Frequency.Should().Be(3223650);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ShouldResolveConjugatedFrenchFormToItsLemmaFrequency()
    {
        // Arrange
        var testData = "prendre/191383 -> prends,prend,pris";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testData);

        try
        {
            await SendAsync(new ImportFrequencyWordsCommand(tempFile, Language.French));

            // Act - a conjugated form carries no frequency of its own, so it has
            // to reach the lemma's through BaseForm
            var frequency = await SendAsync(new GetWordFrequencyQuery("prends", Language.French));

            // Assert
            frequency.Should().Be(191383);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ShouldNotResolveFrenchFormAgainstEnglishFrequencies()
    {
        // Arrange - "prend" exists only as French, so an English lookup must miss it
        var testData = "prendre/191383 -> prends,prend,pris";

        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, testData);

        try
        {
            await SendAsync(new ImportFrequencyWordsCommand(tempFile, Language.French));

            // Act
            var frequency = await SendAsync(new GetWordFrequencyQuery("prend", Language.English));

            // Assert
            frequency.Should().BeNull();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ShouldThrowExceptionForNonExistentFile()
    {
        // Arrange
        var command = new ImportFrequencyWordsCommand("nonexistent.txt");

        // Act & Assert
        await FluentActions.Invoking(() => SendAsync(command))
            .Should().ThrowAsync<FileNotFoundException>();
    }
}
