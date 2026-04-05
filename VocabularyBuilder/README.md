# VocabularyBuilder

A vocabulary learning application with word tracking, encounter history, and multi-dictionary support. Built with ASP.NET Core and React.

The project was generated using the [Clean.Architecture.Solution.Template](https://github.com/jasontaylordev/VocabularyBuilder) version 8.0.0.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js](https://nodejs.org/) (for React frontend)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/[your-username]/vocabulary-builder.git
cd vocabulary-builder/VocabularyBuilder
```

### 2. Trust the Development Certificate

To avoid SSL certificate warnings in your browser, trust the .NET development certificate:

```bash
dotnet dev-certs https --trust
```

Click "Yes" when prompted. This is a one-time setup step.

### 3. Create the Database

The application uses SQLite with Entity Framework Core migrations.

#### Apply Migrations to Development/Test Database

Uses the default connection string from `appsettings.Development.json`:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Web
```

**Note:** If you need to start fresh, simply delete the `VocabularyBuilder.Test.db` file and run the command again.

#### Apply Migrations to Production Database

**Option 1: Using explicit connection string** (Recommended)

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Web --connection "Data Source=VocabularyBuilder.Prod.db"
```

**Option 2: Using environment variable**

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef database update --project src/Infrastructure --startup-project src/Web
```

**When to use each approach:**
- **`--connection` parameter**: Best when you want to target a specific database without changing environment settings. More explicit and safer for production.
- **Environment variable**: Useful when running multiple EF commands against the same environment, or when your connection string is complex and defined in appsettings.
- **No parameters**: Always targets the Development environment by default.

### 4. Build the Solution

```bash
dotnet build -tl
```

### 5. Run the Application

There are multiple ways to run the application depending on your needs:

#### Option 1: Using Launch Profiles (Recommended)

**Development mode** (uses `VocabularyBuilder.Test.db`):
```bash
dotnet run --project src/Web/Web.csproj
```

**Production mode** (uses `VocabularyBuilder.Prod.db`):
```bash
dotnet run --project src/Web/Web.csproj --launch-profile Production
```

### 6. Run the Tests

#### Unit Tests

```bash
dotnet test
```

#### E2E Tests

End-to-end tests use Playwright and run against a test database with mocked external services.

```bash
# Install dependencies (first time only)
npm install
npx playwright install chromium

# Run E2E tests (database is automatically created in-memory)
npm run test:e2e

# Run tests with UI mode (interactive)
npm run test:e2e:ui
```

See [tests/e2e/README.md](tests/e2e/README.md) for detailed E2E testing documentation.

#### Option 2: Using Environment Variables

Set the environment variable explicitly, then run:

```powershell
# For Development
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/Web/Web.csproj

# For Production
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --project src/Web/Web.csproj
```

**When to use this approach:**
- When you need to override the default environment for testing
- When launch profiles don't work in your environment
- When you want to set the environment globally for multiple commands

#### Option 3: Watch Mode (Development Only)

For automatic reloading during development:

```bash
cd src/Web
dotnet watch run
```

This monitors file changes and automatically restarts the application. Always uses Development environment unless overridden with `$env:ASPNETCORE_ENVIRONMENT`.

---

The application will start at:
- **HTTPS**: https://localhost:5001
- **HTTP**: http://localhost:5000

**Environment Configuration Summary:**
- **Development** profile/env: `appsettings.Development.json` → `VocabularyBuilder.Test.db`
- **Production** profile/env: `appsettings.Production.json` → `VocabularyBuilder.Prod.db`

## Features

- **Word Management**: Add, edit, and delete vocabulary words
- **Multi-Language Support**: English (Oxford Dictionary) and French (GPT-powered AI lookup)
- **Bulk Import**: Import multiple words from Oxford Learners Dictionary or GPT
- **Encounter Tracking**: Track each time you encounter a word (with idempotency)
- **Dictionary Caching**: Stores HTML/JSON from dictionary sources to avoid re-fetching
- **Multiple Sources**: Support for Kindle highlights, manual entries, and dictionary imports
- **Smart Lemma Grouping**: Words are combined based on their base form (lemma)

## Database Migrations

If you make changes to entities, create a new migration:

```bash
dotnet ef migrations add YourMigrationName --project src/Infrastructure --startup-project src/Web
dotnet ef database update --project src/Infrastructure --startup-project src/Web
```

## Code Styles & Formatting

The template includes [EditorConfig](https://editorconfig.org/) support to help maintain consistent coding styles for multiple developers working on the same project across various editors and IDEs. The **.editorconfig** file defines the coding styles applicable to this solution.

## Code Scaffolding

The template includes support to scaffold new commands and queries.

Start in the `.\src\Application\` folder.

Create a new command:

```
dotnet new ca-usecase --name CreateTodoList --feature-name TodoLists --usecase-type command --return-type int
```

Create a new query:

```
dotnet new ca-usecase -n GetTodos -fn TodoLists -ut query -rt TodosVm
```

If you encounter the error *"No templates or subcommands found matching: 'ca-usecase'."*, install the template and try again:

```bash
dotnet new install Clean.Architecture.Solution.Template::8.0.0
```

## Test

The solution contains unit, integration, functional, and acceptance tests.

To run the unit, integration, and functional tests (excluding acceptance tests):
```bash
dotnet test --filter "FullyQualifiedName!~AcceptanceTests"
```

To run the acceptance tests, first start the application:

```bash
cd .\src\Web\
dotnet run
```

Then, in a new console, run the tests:
```bash
cd .\src\Web\
dotnet test
```

## Help
To learn more about the template go to the [project website](https://github.com/JasonTaylorDev/VocabularyBuilder). Here you can find additional guidance, request new features, report a bug, and discuss the template with other users.