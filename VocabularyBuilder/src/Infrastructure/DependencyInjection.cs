using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Domain.Constants;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.Data.Interceptors;
using VocabularyBuilder.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Infrastructure.Parsers;
using VocabularyBuilder.Infrastructure.Exporters;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Infrastructure.HttpClients;
using VocabularyBuilder.Infrastructure.Ai;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var aiApiKey = configuration["OpenAI:ApiKey"] ?? "";
        var useMockMode = configuration.GetValue<bool>("OpenAI:UseMockMode");
        var useOxfordMock = configuration.GetValue<bool>("Oxford:UseMockMode");
        var useWordReferenceMock = configuration.GetValue<bool>("WordReference:UseMockMode");
        var useInMemoryDb = configuration.GetValue<bool>("UseInMemoryDatabase");

        Guard.Against.Null(connectionString, message: "Connection string 'DefaultConnection' not found.");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        // Configure database context based on environment
        if (useInMemoryDb)
        {
            // For E2E tests: an in-memory SQLite database, reached by every context through a
            // connection of its own - see InMemoryDatabase for why they must not share one.
            // Checked here, so a connection string that cannot be shared fails at startup
            // rather than as an empty database on the first query.
            InMemoryDatabase.EnsureShareable(connectionString);

            services.AddSingleton(_ => new InMemoryDatabaseKeepAlive(connectionString));

            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                // Opened before the first context connects, and held until the app stops
                sp.GetRequiredService<InMemoryDatabaseKeepAlive>();

                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                // Split the collection includes; see the file-based registration below
                options.UseSqlite(connectionString, x => x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            });
        }
        else
        {
            // For Development/Production: Use file-based SQLite database
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                // A query pulling in two collections at once - the study queue takes a
                // word's senses and its encounters together - multiplies its rows by both
                // unless they are fetched separately. EF warns about it on every such query
                // until the behaviour is stated, and one round trip per collection is the
                // cheaper half of that trade against a local file
                options.UseSqlite(connectionString, x => x
                    .MigrationsAssembly("VocabularyBuilder.Infrastructure")
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            });
        }

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitialiser>();

        AddIdentity(services, configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddTransient<IIdentityService, IdentityService>();

        // Register individual parsers (mock or real based on configuration).
        // WordParserFactory routes by each parser's SourceType, so every parser
        // is registered against IWordReferenceParser and nothing else.
        if (useOxfordMock)
        {
            services.AddScoped<IWordReferenceParser, MockOxfordParser>();
        }
        else
        {
            services.AddScoped<IWordReferenceParser, OxfordParser>();
        }
        
        services.AddScoped<IWordReferenceParser, GptFrenchParser>();

        // WordReference is the French dictionary; GPT stays registered as a
        // fallback for when it has no entry
        services.Configure<WordReferenceOptions>(configuration.GetSection(WordReferenceOptions.SectionName));

        if (useWordReferenceMock)
        {
            services.AddScoped<IWordReferencePageLoader>(_ => new MockWordReferencePageLoader());
        }
        else
        {
            services.AddScoped<IWordReferencePageLoader, HttpWordReferencePageLoader>();
        }

        services.AddScoped<IWordReferenceParser, WordReferenceFrenchParser>();
        services.AddScoped<IConjugationParser, WordReferenceConjugationParser>();
        
        // Register parser factory for language-based routing
        services.AddScoped<IWordParserFactory, WordParserFactory>();
        
        services.Configure<AnkiExportOptions>(configuration.GetSection(AnkiExportOptions.SectionName));
        services.AddScoped<IWordsExporter, AnkiClozeCsvExporter>();
        services.AddScoped<IBookImportParser, BookImportParser>();

        // Reads notes and resolves lemmas for the LingQ and lesson-notes imports
        services.AddScoped<IVocabularyAnalyzer, GptVocabularyAnalyzer>();

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

    /// <summary>
    /// Users, sign-in and who may do what.
    /// </summary>
    /// <remarks>
    /// Identity's own API endpoints do the signing in (mapped under /api/Users by the Web
    /// project). They issue a cookie to the React app and a bearer token to anything else -
    /// curl, a script - and both are accepted everywhere.
    /// </remarks>
    private static void AddIdentity(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentityApiEndpoints<ApplicationUser>(options =>
            {
                // Registration uses the email as the user name, and the administrator is
                // looked up by it, so it has to identify one account
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.ConfigureApplicationCookie(options =>
        {
            // The React app is served from the same origin as the API, so the cookie never
            // has to travel on a request another site started. Strict keeps it that way,
            // which is what stands in for antiforgery tokens on the JSON endpoints.
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        // The keys that sign cookies and tokens. Left in the container they are lost with it
        // on every deploy, signing everyone out, so a deployment points this at its volume.
        var keysPath = configuration["DataProtection:KeysPath"];

        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.Configure<AdministratorOptions>(configuration.GetSection(AdministratorOptions.SectionName));
        services.Configure<RegistrationOptions>(configuration.GetSection(RegistrationOptions.SectionName));

        services.AddAuthorization(options =>
        {
            // Every endpoint needs a signed-in user unless it opts out, so one added later is
            // closed by default rather than open because someone forgot to close it
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(Policies.CanPurge, policy => policy.RequireRole(Roles.Administrator));
        });
    }
}
