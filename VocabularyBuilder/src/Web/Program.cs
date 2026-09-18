using VocabularyBuilder.Application.Study;
using Microsoft.AspNetCore.HttpOverrides;
using VocabularyBuilder.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddKeyVaultIfConfigured(builder.Configuration);

builder.Services.AddSingleton(
    builder.Configuration.GetSection(StudyOptions.SectionName).Get<StudyOptions>() ?? new StudyOptions());
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration); 
builder.Services.AddWebServices();

// End-to-end tests need to move time: spaced repetition is measured in days, and a suite
// that can only wait in real time could never reach the behaviour worth testing.
if (builder.Environment.EnvironmentName == "E2ETest")
{
    builder.Services.AddSingleton<TimeProvider, VocabularyBuilder.Web.Services.TestTimeProvider>();
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins("http://localhost:*")
                       .SetIsOriginAllowedToAllowWildcardSubdomains()
                       .AllowAnyHeader()
                       .AllowAnyMethod();
        });
});

var app = builder.Build();

// A container starts with an empty volume and no database file at all, so something has to
// create one. Off by default, because a process that migrates on startup is the wrong thing
// for a developer machine where migrations are applied deliberately.
if (app.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
{
    await app.InitialiseDatabaseSchemaOnlyAsync();
}

// True when something else terminates TLS in front of the app - a reverse proxy on the same
// host. It changes three things, all of which are wrong to do twice.
var behindReverseProxy = app.Configuration.GetValue<bool>("BehindReverseProxy");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "E2ETest")
{
    // E2ETest should behave like Development for SPA proxy purposes
    app.UseDeveloperExceptionPage();
    
    if (app.Environment.EnvironmentName == "E2ETest")
    {
        // Initialize in-memory database for E2E tests (without seeding to avoid transaction conflicts)
        await app.InitialiseDatabaseSchemaOnlyAsync();
    }
}
else if (!behindReverseProxy)
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    // The proxy sends this header itself when it is the one holding the certificate.
    app.UseHsts();
}

if (behindReverseProxy)
{
    // Without this the app sees every request as plain HTTP from the proxy's own address,
    // so links it generates come out as http:// and any check on the scheme is wrong
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
    });
}

app.UseCors();
app.UseHealthChecks("/health");

// The proxy is already redirecting to HTTPS, and this app is not the one listening on it -
// redirecting again sends the browser to a port nothing is serving
if (!behindReverseProxy)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseSwaggerUi(settings =>
{
    settings.Path = "/api";
    settings.DocumentPath = "/api/specification.json";
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapRazorPages();

app.MapFallbackToFile("index.html");

app.UseExceptionHandler(options => { });


app.MapEndpoints();

app.Run();

public partial class Program { }
