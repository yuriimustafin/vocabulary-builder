using VocabularyBuilder.Application.ImportWords.Commands;
using VocabularyBuilder.Domain.Entities.Frequency;

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
    public async Task ShouldThrowExceptionForNonExistentFile()
    {
        // Arrange
        var command = new ImportFrequencyWordsCommand("nonexistent.txt");

        // Act & Assert
        await FluentActions.Invoking(() => SendAsync(command))
            .Should().ThrowAsync<FileNotFoundException>();
    }
}
