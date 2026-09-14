using System.Reflection;
using VocabularyBuilder.Application.Common.Behaviours;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Application.Study;
using VocabularyBuilder.Application.Study.Exercises;
using VocabularyBuilder.Application.Study.Scheduling;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        });

        // Study loop. StudyOptions is registered by the host, which is where configuration
        // is read; everything below is a pure function of it.
        services.AddSingleton<IReviewScheduler>(sp => new Sm2Scheduler(sp.GetRequiredService<StudyOptions>()));
        services.AddSingleton<IExerciseLadder>(sp => new ConfiguredExerciseLadder(sp.GetRequiredService<StudyOptions>()));
        services.AddSingleton<ICardDifficultyCalculator>(sp => new CardDifficultyCalculator(sp.GetRequiredService<StudyOptions>()));
        services.AddSingleton<IGradeResolver>(sp => new GradeResolver(sp.GetRequiredService<StudyOptions>()));
        services.AddSingleton<IScaffoldSequencer>(sp => new ScaffoldSequencer(
            sp.GetRequiredService<StudyOptions>(),
            sp.GetRequiredService<IExerciseLadder>()));

        return services;
    }
}
