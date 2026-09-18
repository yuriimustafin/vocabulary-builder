using System.Data.Common;
using VocabularyBuilder.Application.Common.Interfaces;
using VocabularyBuilder.Application.Ai;
using VocabularyBuilder.Application.Parsers;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.HttpClients;
using VocabularyBuilder.Infrastructure.Parsers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace VocabularyBuilder.Application.FunctionalTests;

using static Testing;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _connection;

    public CustomWebApplicationFactory(DbConnection connection)
    {
        _connection = connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services
                .RemoveAll<IUser>()
                .AddTransient(provider => Mock.Of<IUser>(s => s.Id == GetUserId()));

            ReplaceOutboundClients(services);

            services
                .RemoveAll<DbContextOptions<ApplicationDbContext>>()
                .AddDbContext<ApplicationDbContext>((sp, options) =>
                {
                    options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                    options.UseSqlite(_connection);
                });
        });
    }

    /// <summary>
    /// Swaps the clients that would leave the machine for the recorded ones.
    /// </summary>
    /// <remarks>
    /// Done by replacing services rather than by setting the UseMockMode flags, because the
    /// flags cannot work here: AddInfrastructureServices reads them while Program.cs is
    /// running and picks the implementations there and then, which is before any
    /// configuration this factory adds is merged in. Replacing the registrations afterwards
    /// is the only thing that actually takes effect - TestHostTests holds it to that.
    ///
    /// Only the three that open their own connection are swapped. The other parsers reach
    /// the network through one of these, so mocking these covers them too.
    /// </remarks>
    private static void ReplaceOutboundClients(IServiceCollection services)
    {
        services
            .RemoveAll<IGptClient>()
            .AddScoped<IGptClient>(_ => new MockGptClient());

        services
            .RemoveAll<IWordReferencePageLoader>()
            .AddScoped<IWordReferencePageLoader>(_ => new MockWordReferencePageLoader());

        // One parser among several, so the registration is picked out rather than the
        // interface cleared - clearing it would silently drop the French parsers too
        var oxford = services.SingleOrDefault(d =>
            d.ServiceType == typeof(IWordReferenceParser) &&
            d.ImplementationType == typeof(OxfordParser));

        if (oxford != null)
        {
            services.Remove(oxford);
            services.AddScoped<IWordReferenceParser>(_ => new MockOxfordParser());
        }
    }
}
