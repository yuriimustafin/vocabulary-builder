using FluentAssertions;
using MediatR;
using Moq;
using NUnit.Framework;
using VocabularyBuilder.Application.History;
using VocabularyBuilder.Application.Imports.Commands;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.History;

public class ImportRunTests
{
    private Mock<ISender> _sender = null!;

    [SetUp]
    public void SetUp()
    {
        _sender = new Mock<ISender>();
        _sender
            .Setup(s => s.Send(It.IsAny<StartImportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
    }

    [Test]
    public async Task ShouldMarkTheImportFailedAndRethrowWhenTheWorkThrows()
    {
        var run = () => ImportRun.TrackAsync<int>(
            _sender.Object,
            new StartImportCommand { Kind = ImportKind.LingQ },
            _ => throw new InvalidOperationException("model unreachable"),
            CancellationToken.None);

        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("model unreachable");

        _sender.Verify(s => s.Send(
            It.Is<FailImportCommand>(c => c.ImportId == 42 && c.Error == "model unreachable"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A failure to record the failure must not hide the failure itself.</summary>
    [Test]
    public async Task ShouldRethrowTheImportsOwnErrorWhenMarkingItFailedAlsoFails()
    {
        _sender
            .Setup(s => s.Send(It.IsAny<FailImportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database gone"));

        var run = () => ImportRun.TrackAsync<int>(
            _sender.Object,
            new StartImportCommand(),
            _ => throw new FormatException("bad file"),
            CancellationToken.None);

        await run.Should().ThrowAsync<FormatException>();
    }

    [Test]
    public async Task ShouldMakeTheImportTheScopeOfEveryCallInside()
    {
        int? seen = null;

        await ImportRun.TrackAsync(_sender.Object, new StartImportCommand(), id =>
        {
            seen = ExternalCallScope.Current?.ImportId;
            return Task.FromResult(id);
        }, CancellationToken.None);

        seen.Should().Be(42);
        ExternalCallScope.Current.Should().BeNull("the scope ends with the import");
    }
}

public class ExternalCallScopeTests
{
    [Test]
    public void ShouldKeepWhatTheOuterScopeKnowsAndAnInnerOneDoesNotSay()
    {
        using (ExternalCallScope.Begin(importId: 7))
        {
            using (ExternalCallScope.Begin(ExternalCallPurpose.Conjugation, target: "prendre"))
            {
                ExternalCallScope.Current.Should().Be(
                    new ExternalCallScope.Frame(ExternalCallPurpose.Conjugation, "prendre", null, 7));
            }

            ExternalCallScope.Current.Should().Be(new ExternalCallScope.Frame(null, null, null, 7));
        }

        ExternalCallScope.Current.Should().BeNull();
    }

    /// <summary>A batch over existing words - an export - names each call's word by what it was about.</summary>
    [Test]
    public void ShouldMatchACallToItsWordByWhatItWasAbout()
    {
        var words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["gare"] = 4, ["migrer"] = 7 };

        using (ExternalCallScope.Begin(wordIdsByTarget: words))
        {
            ExternalCallScope.Current!.WordIdFor("Migrer").Should().Be(7);
            ExternalCallScope.Current.WordIdFor("maison").Should().BeNull();

            // A scope that knows its one word outranks the batch
            using (ExternalCallScope.Begin(wordId: 9))
            {
                ExternalCallScope.Current.WordIdFor("gare").Should().Be(9);
            }
        }
    }

    [Test]
    public async Task ShouldNotLeakIntoWorkRunningAlongside()
    {
        var release = new TaskCompletionSource();
        ExternalCallScope.Frame? seenElsewhere = new(null, "unset", null, null);

        var elsewhere = Task.Run(async () =>
        {
            await release.Task;
            seenElsewhere = ExternalCallScope.Current;
        });

        using (ExternalCallScope.Begin(target: "maison"))
        {
            release.SetResult();
            await elsewhere;
        }

        seenElsewhere.Should().BeNull();
    }
}
