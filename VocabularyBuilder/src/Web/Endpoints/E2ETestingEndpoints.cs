using VocabularyBuilder.Infrastructure.Data;
using VocabularyBuilder.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace VocabularyBuilder.Web.Endpoints;

/// <summary>
/// E2E Testing utilities endpoint - ONLY available in E2ETest environment
/// </summary>
public class E2ETestingEndpoints : EndpointGroupBase
{
    public override void Map(WebApplication app)
    {
        // Only register these endpoints in E2ETest environment
        if (app.Environment.EnvironmentName != "E2ETest")
        {
            return;
        }

        var group = app
            .MapGroup("/api/e2e-testing")
            .WithGroupName("E2ETesting")
            .WithTags("E2ETesting")
            .WithOpenApi();

        group.MapPost("/reset-database", ResetDatabase);
        group.MapGet("/health", HealthCheck);
    }

    /// <summary>
    /// Resets the in-memory database by clearing all data
    /// </summary>
    public async Task<IResult> ResetDatabase(
        [FromServices] IServiceProvider serviceProvider)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Check if using in-memory database
            var connectionString = context.Database.GetConnectionString();
            var isInMemory = connectionString?.Contains(":memory:") == true;

            if (!isInMemory)
            {
                return Results.BadRequest(new 
                { 
                    error = "Database reset is only available for in-memory databases (E2E testing)" 
                });
            }

            // Use raw SQL to delete all data - order matters (child tables first due to FK constraints)
            // Don't use transactions - just execute directly
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    PRAGMA foreign_keys = OFF;
                    
                    DELETE FROM ImportedBookWords;
                    DELETE FROM FrequencyWords;
                    DELETE FROM WordDictionarySources;
                    DELETE FROM WordEncounters;
                    DELETE FROM Words;
                    DELETE FROM TodoItems;
                    DELETE FROM TodoLists;
                    
                    DELETE FROM AspNetUserTokens;
                    DELETE FROM AspNetUserRoles;
                    DELETE FROM AspNetUserLogins;
                    DELETE FROM AspNetUserClaims;
                    DELETE FROM AspNetUsers;
                    DELETE FROM AspNetRoleClaims;
                    DELETE FROM AspNetRoles;
                    
                    PRAGMA foreign_keys = ON;
                ";
                await command.ExecuteNonQueryAsync();
            }
            
            // Clear the change tracker to prevent auto-save on scope dispose
            context.ChangeTracker.Clear();
            
            // Don't re-seed - tests don't need seed data and it causes transaction conflicts

            return Results.Ok(new 
            { 
                message = "Database reset successfully (data cleared, no seed)",
                timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" Inner: {ex.InnerException.Message}";
                if (ex.InnerException.InnerException != null)
                {
                    errorMessage += $" Inner2: {ex.InnerException.InnerException.Message}";
                }
            }
            
            return Results.Problem(
                title: "Database reset failed",
                detail: errorMessage,
                statusCode: 500
            );
        }
    }

    /// <summary>
    /// Health check endpoint for E2E tests
    /// </summary>
    public IResult HealthCheck()
    {
        return Results.Ok(new 
        { 
            status = "healthy",
            environment = "E2ETest",
            timestamp = DateTime.UtcNow
        });
    }
}
