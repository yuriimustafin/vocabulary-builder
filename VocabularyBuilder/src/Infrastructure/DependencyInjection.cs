using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Constants;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.Data.Interceptors;
using VocabularyBuilder.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Infrastructure.Parsers;
using VocabularyBuilder.Infrastructure.Exporters;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Infrastructure.HttpClients;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var aiApiKey = configuration["OpenAI:ApiKey"] ?? "";

        Guard.Against.Null(connectionString, message: "Connection string 'DefaultConnection' not found.");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            options.UseSqlServer(connectionString,
                x => x.MigrationsAssembly("VocabularyBuilder.Infrastructure"));
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitialiser>();

        services
            .AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddSingleton(TimeProvider.System);
        services.AddTransient<IIdentityService, IdentityService>();

        services.AddAuthorization(options =>
            options.AddPolicy(Policies.CanPurge, policy => policy.RequireRole(Roles.Administrator)));

        // TODO: Make it configurable
        services.AddScoped<IWordReferenceParser, OxfordParser>();
        services.AddScoped<IWordsExporter, AnkiClozeCsvExporter>();
        services.AddScoped<IBookImportParser, BookImportParser>();

        services.AddScoped<IGptClient>(x =>
            ActivatorUtilities.CreateInstance<GptClient>(x, aiApiKey));

        return services;
    }
}
