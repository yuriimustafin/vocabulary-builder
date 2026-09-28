using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Domain.Helpers;

namespace VocabularyBuilder.Domain.UnitTests.Helpers;

public class WordTagsTests
{
    [Test]
    public void ShouldReadASingleTag()
    {
        WordTags.Parse("preply").Should().Equal("preply");
    }

    /// <summary>
    /// One field, but "preply, travel" is what people write when they mean two.
    /// </summary>
    [Test]
    public void ShouldReadSeveralTagsFromOneField()
    {
        WordTags.Parse("preply, travel, food").Should().Equal("preply", "travel", "food");
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase(",")]
    [TestCase(" , , ")]
    public void ShouldReadNothingFromAnEmptyField(string? field)
    {
        WordTags.Parse(field).Should().BeEmpty();
    }

    [Test]
    public void ShouldTrimAroundTheCommas()
    {
        WordTags.Parse("  preply  ,   travel ").Should().Equal("preply", "travel");
    }

    [Test]
    public void ShouldReadARepeatedTagOnce()
    {
        WordTags.Parse("preply, Preply, PREPLY").Should().Equal("preply");
    }

    /// <summary>
    /// The behaviour tags exist for: a word met again under a new label keeps the old one.
    /// </summary>
    [Test]
    public void ShouldAddToTheTagsAWordAlreadyHas()
    {
        WordTags.Merge(new[] { "preply" }, new[] { "travel" })
            .Should().Equal("preply", "travel");
    }

    [Test]
    public void ShouldNotAddATagTheWordAlreadyHas()
    {
        WordTags.Merge(new[] { "preply", "travel" }, new[] { "travel" })
            .Should().Equal("preply", "travel");
    }

    /// <summary>
    /// Re-importing under "Preply" must not leave the word carrying both spellings.
    /// </summary>
    [Test]
    public void ShouldTreatADifferentlyCasedTagAsTheSameTag()
    {
        WordTags.Merge(new[] { "preply" }, new[] { "Preply" })
            .Should().Equal("preply");
    }

    [Test]
    public void ShouldKeepTheOrderTheTagsWereAddedIn()
    {
        WordTags.Merge(new[] { "b", "a" }, new[] { "d", "c" })
            .Should().Equal("b", "a", "d", "c");
    }

    [Test]
    public void ShouldMergeOntoAWordWithNoTagsYet()
    {
        WordTags.Merge(null, new[] { "preply" }).Should().Equal("preply");
    }

    [Test]
    public void ShouldLeaveTheTagsAloneWhenNothingIsAdded()
    {
        WordTags.Merge(new[] { "preply" }, null).Should().Equal("preply");
    }

    [Test]
    public void ShouldMergeNothingIntoNothing()
    {
        WordTags.Merge(null, null).Should().BeEmpty();
    }
}
