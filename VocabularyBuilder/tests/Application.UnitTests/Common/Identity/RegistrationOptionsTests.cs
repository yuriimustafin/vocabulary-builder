using FluentAssertions;
using NUnit.Framework;
using VocabularyBuilder.Infrastructure.Identity;

namespace VocabularyBuilder.Application.UnitTests.Common.Identity;

/// <summary>
/// Reading the registration allowlist out of the single string it is configured as.
/// </summary>
public class RegistrationOptionsTests
{
    [TestCase("a@example.com,b@example.com")]
    [TestCase("a@example.com; b@example.com")]
    [TestCase(" a@example.com\n b@example.com ")]
    public void ShouldAcceptAnyOfTheUsualSeparators(string allowed)
    {
        var options = new RegistrationOptions { AllowedEmails = allowed };

        options.IsAllowed("b@example.com").Should().BeTrue();
    }

    [Test]
    public void ShouldIgnoreCaseAndSurroundingSpace()
    {
        var options = new RegistrationOptions { AllowedEmails = "Learner@Example.com" };

        options.IsAllowed(" learner@example.COM ").Should().BeTrue();
    }

    /// <summary>
    /// Whole addresses only: a listed address must not admit every address containing it.
    /// </summary>
    [Test]
    public void ShouldNotMatchPartOfAnAddress()
    {
        var options = new RegistrationOptions { AllowedEmails = "me@example.com" };

        options.IsAllowed("not-me@example.com").Should().BeFalse();
        options.IsAllowed("example.com").Should().BeFalse();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ShouldCloseRegistrationWhenNothingIsListed(string? allowed)
    {
        var options = new RegistrationOptions { AllowedEmails = allowed };

        options.IsAllowed("anyone@example.com").Should().BeFalse();
    }
}
