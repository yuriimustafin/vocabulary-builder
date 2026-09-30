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
    public void ConfiguredLevelsReplaceTheDefaults()
    {
        // Regression: configured rungs landed on top of the defaults, leaving a ladder that
        // ran through every level twice.
        var options = Bind(new Dictionary<string, string?>
        {
            ["Study:Ladder:0:Exercises:0:Type"] = nameof(ExerciseType.WordToMeaningReveal),
            ["Study:Ladder:1:PromoteAfter"] = "2",
            ["Study:Ladder:1:Exercises:0:Type"] = nameof(ExerciseType.MeaningToWordChoice),
            ["Study:Ladder:1:Exercises:1:Type"] = nameof(ExerciseType.WordToMeaningChoice),
            ["Study:Ladder:2:Exercises:0:Type"] = nameof(ExerciseType.MeaningToWordRecall)
        });

        options.EffectiveLadder.Should().HaveCount(3);
        options.EffectiveLadder[1].PromoteAfter.Should().Be(2);
        options.EffectiveLadder[1].Exercises.Select(e => e.Type).Should().Equal(
            ExerciseType.MeaningToWordChoice, ExerciseType.WordToMeaningChoice);
    }

    [Test]
    public void TheBuiltInDefaultsApplyWhenNothingIsConfigured()
    {
        var options = Bind(new Dictionary<string, string?> { ["Study:NewCardsPerDay"] = "5" });

        options.NewCardsPerDay.Should().Be(5);
        options.EffectiveLearningSteps.Should().Equal(1, 3, 5, 8);
        options.EffectiveLadder.Should().HaveCount(4);
    }

    [Test]
    public void ScalarSettingsBindNormally()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Study:NewCardsPerDay"] = "7",
            ["Study:LearningExitSuccesses"] = "3",
            ["Study:FollowUpsByTier:Difficult"] = "3",
            ["Study:DifficultyTiers:DifficultMinLapses"] = "4"
        });

        options.NewCardsPerDay.Should().Be(7);
        options.LearningExitSuccesses.Should().Be(3);
        options.FollowUpsByTier.Difficult.Should().Be(3);
        options.DifficultyTiers.DifficultMinLapses.Should().Be(4);
    }

    [Test]
    public void TheShippedConfigurationMatchesTheBuiltInDefaults()
    {
        // Read from the real appsettings.json, so a change there that doubles a collection -
        // or drifts from what the code assumes when nothing is configured - fails here.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(ShippedSettingsPath(), optional: false)
            .Build();

        var shipped = configuration.GetSection(StudyOptions.SectionName).Get<StudyOptions>()!;
        var defaults = new StudyOptions();

        shipped.EffectiveLearningSteps.Should().Equal(defaults.EffectiveLearningSteps);
        shipped.EffectiveLadder.Select(Describe).Should().Equal(defaults.EffectiveLadder.Select(Describe));
        shipped.LongGapProbeType.Should().Be(defaults.LongGapProbeType);
        shipped.FailureRungDrop.Should().Be(defaults.FailureRungDrop);
        shipped.LearningExitSuccesses.Should().Be(defaults.LearningExitSuccesses);
    }

    private static string Describe(LadderRungOptions level) =>
        $"{level.PromoteAfter}: {string.Join(", ", level.Exercises.Select(e => e.Tolerant ? $"{e.Type} (tolerant)" : e.Type.ToString()))}";

    private static string ShippedSettingsPath()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Web", "appsettings.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find src/Web/appsettings.json above the test directory.");
    }
}
