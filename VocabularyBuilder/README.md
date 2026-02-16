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

The application uses SQLite with Entity Framework Core migrations. Create the database:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Web
```

**Note:** If you need to start fresh, simply delete the `app.db` file in `src/Web/` and run the command again.

### 4. Build the Solution

```bash
dotnet build -tl
```

### 5. Run the Application

Set the environment to Development (uses Test database):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

Then run the application:

```bash
cd src/Web
dotnet watch run
```

The application will start at:
- **HTTPS**: https://localhost:5001
- **HTTP**: http://localhost:5000

The React frontend will be automatically compiled and served. The application will automatically reload if you change any source files.

**Note:** The application uses different databases based on the environment:
- **Development**: `VocabularyBuilder.Test.db`
- **Production**: `VocabularyBuilder.Prod.db`

## Features

- **Word Management**: Add, edit, and delete vocabulary words
- **Bulk Import**: Import multiple words from Oxford Learners Dictionary
- **Encounter Tracking**: Track each time you encounter a word (with idempotency)
- **Dictionary Caching**: Stores HTML from dictionary sources to avoid re-fetching
- **Multiple Sources**: Support for Kindle highlights, manual entries, and dictionary imports

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