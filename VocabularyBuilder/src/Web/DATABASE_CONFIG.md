# Database Configuration

This application supports two separate databases: Test and Production.

## Database Files

- **Test Database**: `VocabularyBuilder.Test.db` - Used in Development environment
- **Production Database**: `VocabularyBuilder.Prod.db` - Used in Production environment

## Switching Between Databases

### Method 1: Using Environment Variable (Recommended)

Set the `ASPNETCORE_ENVIRONMENT` environment variable:

**For Test/Development Database:**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/Web/Web.csproj
```

**For Production Database:**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --project src/Web/Web.csproj
```

### Method 2: Using Launch Settings

Edit `src/Web/Properties/launchSettings.json` and change the `ASPNETCORE_ENVIRONMENT` value for your profile.

### Method 3: Directly Modify Connection String

Temporarily edit `appsettings.Development.json` or `appsettings.json` to change the `DefaultConnection` value:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=VocabularyBuilder.Test.db"  // or VocabularyBuilder.Prod.db
}
```

## Configuration Files

- `appsettings.json` - Base configuration (Production database)
- `appsettings.Development.json` - Development environment (Test database)
- `appsettings.Production.json` - Production environment (Production database)

## Current Setup

- **Development**: Uses `VocabularyBuilder.Test.db`
- **Production**: Uses `VocabularyBuilder.Prod.db`

The application automatically selects the appropriate database based on the environment.

## Migrations

When running migrations, specify the environment to target the correct database:

**For Test Database:**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Web/Web.csproj
```

**For Production Database:**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Web/Web.csproj
```
