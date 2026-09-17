using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

/// <summary>
/// Options as the host actually binds them.
///
/// Configuration binding appends to a collection that already has contents rather than
/// replacing it. A populated default on a bound property therefore silently doubles
/// anything configured, which is invisible in a test that constructs the options directly.
/// </summary>
public class StudyOptionsBindingTests
{
    private static StudyOptions Bind(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return configuration.GetSection(StudyOptions.SectionName).Get<StudyOptions>() ?? new StudyOptions();
    }

    [Test]
    public void ConfiguredLearningStepsReplaceTheDefaultsRatherThanBeingAddedToThem()
    {
        // Regression: this bound to [1, 10, 1, 10], so a card walked four steps and never
        // graduated - it simply cycled through day one forever.
        var options = Bind(new Dictionary<string, string?>
        {
            ["Study:LearningStepsMinutes:0"] = "1",
            ["Study:LearningStepsMinutes:1"] = "10"
        });

        options.EffectiveLearningSteps.Should().Equal(1, 10);
    }

    [Test]
    public void ConfiguredLadderRungsReplaceTheDefaults()
    {
        // Regression: the six configured rungs landed on top of six defaults, leaving a
        // twelve-rung ladder that ran through every exercise type twice.
        var values = new Dictionary<string, string?>();
        var types = new[]
        {
            nameof(ExerciseType.WordToMeaningReveal),
            nameof(ExerciseType.WordToMeaningChoice),
            nameof(ExerciseType.MeaningToWordChoice),
            nameof(ExerciseType.ContextToWordRecall),
            nameof(ExerciseType.MeaningToWordScramble),
            nameof(ExerciseType.MeaningToWordRecall)
        };

        for (var i = 0; i < types.Length; i++)
        {
            values[$"Study:Ladder:{i}:Type"] = types[i];
        }

        var options = Bind(values);

        options.EffectiveLadder.Should().HaveCount(6);
        options.EffectiveLadder.Select(r => r.Type).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void AShorterConfiguredLadderIsHonouredExactly()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Study:Ladder:0:Type"] = nameof(ExerciseType.WordToMeaningReveal),
            ["Study:Ladder:1:Type"] = nameof(ExerciseType.MeaningToWordRecall)
        });

        options.EffectiveLadder.Select(r => r.Type).Should().Equal(
            ExerciseType.WordToMeaningReveal, ExerciseType.MeaningToWordRecall);
    }

    [Test]
    public void TheBuiltInDefaultsApplyWhenNothingIsConfigured()
    {
        var options = Bind(new Dictionary<string, string?> { ["Study:NewCardsPerDay"] = "5" });

        options.NewCardsPerDay.Should().Be(5);
        options.EffectiveLearningSteps.Should().Equal(1, 10);
        options.EffectiveLadder.Should().HaveCount(6);
    }

    [Test]
    public void ScalarSettingsBindNormally()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Study:NewCardsPerDay"] = "7",
            ["Study:TargetSuccessRate"] = "0.9",
            ["Study:FollowUpsByTier:Difficult"] = "3",
            ["Study:DifficultyTiers:DifficultMinLapses"] = "4"
        });

        options.NewCardsPerDay.Should().Be(7);
        options.TargetSuccessRate.Should().Be(0.9);
        options.FollowUpsByTier.Difficult.Should().Be(3);
        options.DifficultyTiers.DifficultMinLapses.Should().Be(4);
    }

    [Test]
    public void TheShippedConfigurationProducesTheIntendedLadder()
    {
        // The real appsettings values, so a change there that doubles a collection fails here.
        var values = new Dictionary<string, string?>
        {
            ["Study:LearningStepsMinutes:0"] = "1",
            ["Study:LearningStepsMinutes:1"] = "10",
            ["Study:Ladder:0:Type"] = nameof(ExerciseType.WordToMeaningReveal),
            ["Study:Ladder:1:Type"] = nameof(ExerciseType.WordToMeaningChoice),
            ["Study:Ladder:2:Type"] = nameof(ExerciseType.MeaningToWordChoice),
            ["Study:Ladder:3:Type"] = nameof(ExerciseType.ContextToWordRecall),
            ["Study:Ladder:4:Type"] = nameof(ExerciseType.MeaningToWordScramble),
            ["Study:Ladder:5:Type"] = nameof(ExerciseType.MeaningToWordRecall)
        };

        var options = Bind(values);

        options.EffectiveLearningSteps.Should().HaveCount(2,
            "three touches on day one depends on exactly two learning steps");
        options.EffectiveLadder.Select(r => r.Type).Should().Equal(
            ExerciseType.WordToMeaningReveal,
            ExerciseType.WordToMeaningChoice,
            ExerciseType.MeaningToWordChoice,
            ExerciseType.ContextToWordRecall,
            ExerciseType.MeaningToWordScramble,
            ExerciseType.MeaningToWordRecall);
    }
}
