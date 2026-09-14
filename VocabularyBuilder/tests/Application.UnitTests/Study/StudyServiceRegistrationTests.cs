using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Scheduling;
using VocabularyBuilder.Domain.Enums;

namespace VocabularyBuilder.Application.UnitTests.Study;

/// <summary>
/// Container checks. Every study service is resolved through real registration, so a
/// constructor that stops matching its registration fails here rather than at startup.
/// </summary>
public class StudyServiceRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // The host supplies these two; everything else comes from AddApplicationServices.
        services.AddSingleton(new StudyOptions());
        services.AddScoped(_ => new Mock<IApplicationDbContext>().Object);
        services.AddApplicationServices();

        // Scope validation catches a singleton capturing a scoped dependency, which is the
        // real hazard here. Whole-container validation is not used: most MediatR handlers
        // depend on Infrastructure registrations that this test deliberately does not load.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Test]
    public void EveryStudyServiceResolves()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<IReviewScheduler>().Should().BeOfType<Sm2Scheduler>();
        services.GetRequiredService<IExerciseLadder>().Should().BeOfType<ConfiguredExerciseLadder>();
        services.GetRequiredService<ICardDifficultyCalculator>().Should().NotBeNull();
        services.GetRequiredService<IGradeResolver>().Should().NotBeNull();
        services.GetRequiredService<IScaffoldSequencer>().Should().NotBeNull();
        services.GetRequiredService<IStudyMaterialResolver>().Should().NotBeNull();
        services.GetRequiredService<IDistractorPicker>().Should().NotBeNull();
        services.GetRequiredService<IDistractorSource>().Should().NotBeNull();
        services.GetRequiredService<IExerciseCatalog>().Should().NotBeNull();
    }

    [Test]
    public void TheCatalogueIsBuiltFromTheRegisteredDefinitionsAndCoversEveryType()
    {
        using var provider = BuildProvider();
        var catalog = provider.GetRequiredService<IExerciseCatalog>();

        foreach (var type in Enum.GetValues<ExerciseType>())
        {
            catalog.Get(type).Type.Should().Be(type);
        }
    }

    [Test]
    public void EveryRungOfTheConfiguredLadderCanBeResolved()
    {
        using var provider = BuildProvider();
        var catalog = provider.GetRequiredService<IExerciseCatalog>();
        var ladder = provider.GetRequiredService<IExerciseLadder>();

        for (var rung = 0; rung < ladder.RungCount; rung++)
        {
            catalog.Get(ladder.TypeAt(rung)).CanBeProbe
                .Should().BeTrue("a rung the ladder can select must be gradable");
        }
    }
}
