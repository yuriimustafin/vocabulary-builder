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
        var useMockMode = configuration.GetValue<bool>("OpenAI:UseMockMode");
        var useOxfordMock = configuration.GetValue<bool>("Oxford:UseMockMode");
        var useInMemoryDb = configuration.GetValue<bool>("UseInMemoryDatabase");

        Guard.Against.Null(connectionString, message: "Connection string 'DefaultConnection' not found.");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        // Configure database context based on environment
        if (useInMemoryDb)
        {
            // For E2E tests: Use in-memory SQLite database
            // Keep a singleton connection open to prevent the database from being destroyed
            services.AddSingleton<Microsoft.Data.Sqlite.SqliteConnection>(sp =>
            {
                var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                connection.Open();
                return connection;
            });

            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<Microsoft.Data.Sqlite.SqliteConnection>();
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                options.UseSqlite(connection);
            });
        }
        else
        {
            // For Development/Production: Use file-based SQLite database
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                options.UseSqlite(connectionString,
                    x => x.MigrationsAssembly("VocabularyBuilder.Infrastructure"));
            });
        }

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

        // Register individual parsers (mock or real based on configuration)
        if (useOxfordMock)
        {
            services.AddScoped<IWordReferenceParser, MockOxfordParser>();
        }
        else
        {
            services.AddScoped<OxfordParser>();
            services.AddScoped<IWordReferenceParser, OxfordParser>();
        }
        
        services.AddScoped<GptFrenchParser>();
        
        // Register parser factory for language-based routing
        services.AddScoped<IWordParserFactory, WordParserFactory>();
        
        services.AddScoped<IWordsExporter, AnkiClozeCsvExporter>();
        services.AddScoped<IBookImportParser, BookImportParser>();

        // Register GPT client (mock or real based on configuration)
        if (useMockMode)
        {
            services.AddScoped<IGptClient, MockGptClient>();
        }
        else
        {
            services.AddScoped<IGptClient>(x =>
                ActivatorUtilities.CreateInstance<GptClient>(x, aiApiKey));
        }

        return services;
    }
}
