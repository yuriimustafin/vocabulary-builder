using FluentAssertions;
using NUnit.Framework;
using System.Text.RegularExpressions;
using VocabularyBuilder.Domain.Samples.Exceptions;
using VocabularyBuilder.Domain.Samples.ValueObjects;

namespace VocabularyBuilder.Domain.UnitTests.ValueObjects;

public class ColourTests
{
    [Test]
    public void ShouldReturnCorrectColourCode()
    {
        var code = "#FFFFFF";

        var colour = Colour.From(code);

        colour.Code.Should().Be(code);
    }


    [Test]
    public void Test()
    {
        var str = "tes 1 a  aaa";
        var digitsOnly = Regex.Replace(str, @"[^\d]", "");

        var isParsed = int.TryParse(digitsOnly, out var num);

        isParsed.Should().Be(true);
        num.Should().Be(1);
    }


    [Test]
    public void ToStringReturnsCode()
    {
        var colour = Colour.White;

        colour.ToString().Should().Be(colour.Code);
    }

    [Test]
    public void ShouldPerformImplicitConversionToColourCodeString()
    {
        string code = Colour.White;

        code.Should().Be("#FFFFFF");
    }

    [Test]
    public void ShouldPerformExplicitConversionGivenSupportedColourCode()
    {
        var colour = (Colour)"#FFFFFF";

        colour.Should().Be(Colour.White);
    }

    [Test]
    public void ShouldThrowUnsupportedColourExceptionGivenNotSupportedColourCode()
    {
        FluentActions.Invoking(() => Colour.From("##FF33CC"))
            .Should().Throw<UnsupportedColourException>();
    }
}
