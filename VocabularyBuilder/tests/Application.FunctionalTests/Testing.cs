using VocabularyBuilder.Domain.Constants;
using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace VocabularyBuilder.Application.FunctionalTests;

[SetUpFixture]
public partial class Testing
{
    private static ITestDatabase _database;
    private static CustomWebApplicationFactory _factory = null!;
    private static IServiceScopeFactory _scopeFactory = null!;
    private static string? _userId;

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        _database = await TestDatabaseFactory.CreateAsync();

        _factory = new CustomWebApplicationFactory(_database.GetConnection());

        _scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>();
    }

    public static async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = _scopeFactory.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        return await mediator.Send(request);
    }

    public static async Task SendAsync(IBaseRequest request)
    {
        using var scope = _scopeFactory.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        await mediator.Send(request);
    }

    /// <summary>
    /// A client for the test host, holding its own cookies, on HTTPS so that the host has no
    /// redirect of its own to make.
    /// </summary>
    public static HttpClient CreateClient() =>
        _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    public static string? GetUserId()
    {
        return _userId;
    }

    public static async Task<string> RunAsDefaultUserAsync()
    {
        return await RunAsUserAsync("test@local", "Testing1234!", Array.Empty<string>());
    }

    public static async Task<string> RunAsAdministratorAsync()
    {
        return await RunAsUserAsync("administrator@local", "Administrator1234!", new[] { Roles.Administrator });
    }

    /// <summary>
    /// Runs the rest of the test with nobody signed in.
    /// </summary>
    public static void RunAsAnonymous()
    {
        _userId = null;
    }

    /// <summary>
    /// Signs in as the given user, creating it the first time. Switching back to a user the
    /// test has already used returns to the same account, and so to the same data.
    /// </summary>
    public static async Task<string> RunAsUserAsync(string userName, string password, string[] roles)
    {
        using var scope = _scopeFactory.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByNameAsync(userName);

        if (existing is not null)
        {
            _userId = existing.Id;

            return _userId;
        }

        var user = new ApplicationUser { UserName = userName, Email = userName };

        var result = await userManager.CreateAsync(user, password);

        if (roles.Any())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in roles)
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }

            await userManager.AddToRolesAsync(user, roles);
        }

        if (result.Succeeded)
        {
            _userId = user.Id;

            return _userId;
        }

        var errors = string.Join(Environment.NewLine, result.ToApplicationResult().Errors);

        throw new Exception($"Unable to create {userName}.{Environment.NewLine}{errors}");
    }

    /// <summary>
    /// Empties the database between tests.
    /// </summary>
    /// <remarks>
    /// A failure here is not swallowed. It used to be, from when Respawn drove the reset
    /// against SQL Server and could throw for reasons of its own - but a reset that quietly
    /// does nothing leaves every later test reading another test's rows, and they fail
    /// somewhere far away from the cause.
    /// </remarks>
    public static async Task ResetState()
    {
        await _database.ResetAsync();

        _userId = null;
    }

    /// <summary>
    /// Resolves a service the way a handler would, so a test can check what the host
    /// actually wired up.
    /// </summary>
    public static TService GetService<TService>() where TService : notnull
    {
        using var scope = _scopeFactory.CreateScope();

        return scope.ServiceProvider.GetRequiredService<TService>();
    }

    /// <summary>
    /// Runs against a resolved service inside its scope, for anything that must not outlive
    /// the scope it came from - a DbContext above all.
    /// </summary>
    public static TResult WithService<TService, TResult>(Func<TService, TResult> use)
        where TService : notnull
    {
        using var scope = _scopeFactory.CreateScope();

        return use(scope.ServiceProvider.GetRequiredService<TService>());
    }

    /// <summary>
    /// <see cref="WithService{TService, TResult}"/> for work that has to be awaited, which must
    /// finish before the scope it runs in is disposed.
    /// </summary>
    public static async Task<TResult> WithServiceAsync<TService, TResult>(Func<TService, Task<TResult>> use)
        where TService : notnull
    {
        using var scope = _scopeFactory.CreateScope();

        return await use(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public static IEnumerable<TService> GetServices<TService>()
    {
        using var scope = _scopeFactory.CreateScope();

        return scope.ServiceProvider.GetServices<TService>().ToList();
    }

    public static async Task<TEntity?> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.FindAsync<TEntity>(keyValues);
    }

    public static async Task AddAsync<TEntity>(TEntity entity)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Add(entity);

        await context.SaveChangesAsync();
    }

    public static async Task<List<TEntity>> ListAsync<TEntity>() where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Set<TEntity>().ToListAsync();
    }

    public static async Task<int> CountAsync<TEntity>() where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Set<TEntity>().CountAsync();
    }

    [OneTimeTearDown]
    public async Task RunAfterAnyTests()
    {
        await _database.DisposeAsync();
        await _factory.DisposeAsync();
    }
}
