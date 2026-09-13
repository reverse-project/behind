# Getting Started

Quick start guide for setting up and developing in the Behind project.

## Prerequisites

### System Requirements
- **OS**: Windows 10+, Ubuntu 20.04+, macOS 11+ (development only)
- **RAM**: 8GB minimum, 16GB recommended
- **.NET SDK**: 10.0 (install via dev container or manually)
- **Git**: 2.25+
- **Docker**: 20.10+ (optional, for containerized development)

### Development Environment

#### Option 1: Dev Container (Recommended)

Uses predefined development environment:

```bash
# Clone repository
git clone https://github.com/your-org/behind.git
cd behind

# VS Code will prompt to reopen in container
# Or manually: Ctrl+Shift+P > Dev Containers: Reopen in Container
```

**Dev Container Includes**:
- .NET 10 SDK pre-installed
- Node.js and npm
- ESLint
- Git, curl, wget, gpg
- All required tooling

#### Option 2: Local Installation

```bash
# Install .NET 10 SDK
# https://dotnet.microsoft.com/download/dotnet/10.0

# Verify installation
dotnet --version  # Should show 10.0.x

# Restore dependencies
cd Behind
dotnet restore

# Verify build
dotnet build Behind.slnx
```

## Project Structure

```
Behind/
├── src/
│   ├── Aspire/              # Service orchestration
│   ├── Common/              # Shared libraries
│   │   ├── Behind.Common
│   │   ├── Behind.Common.Database
│   │   └── Behind.Common.WebApi
│   ├── Core/                # Domain & business logic
│   │   ├── Behind.Core.Common
│   │   ├── Behind.Core.Domain
│   │   ├── Behind.Core.Database
│   │   ├── Behind.Core.Database.Migrator
│   │   ├── Behind.Core.WebApi
│   │   └── Behind.Core.Tools
│   └── Web/                 # Web application
└── tests/
    └── Behind.Tests/        # Unit tests
```

## Build and Compilation

### Build Solution

```bash
cd Behind

# Restore packages and build
dotnet build Behind.slnx

# Build specific project
dotnet build src/Core/Behind.Core.WebApi/Behind.Core.WebApi.csproj

# Release build (optimized)
dotnet build Behind.slnx -c Release

# Verbose output
dotnet build Behind.slnx --verbosity detailed
```

### Check Errors

```bash
# Run static analysis
dotnet build Behind.slnx /t:Analyzers

# List all compiler warnings
dotnet build Behind.slnx /p:TreatWarningsAsErrors=false
```

## Running Tests

### Run All Tests

```bash
cd Behind

# Execute all tests
dotnet test tests/Behind.Tests/Behind.Tests.csproj

# Verbose output
dotnet test tests/Behind.Tests/Behind.Tests.csproj --verbosity normal

# Run single test class
dotnet test tests/Behind.Tests/Behind.Tests.csproj \
    --filter "FullyQualifiedName~UserRepositoryTests"

# Run with coverage
dotnet test tests/Behind.Tests/Behind.Tests.csproj \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=opencover
```

### Watch Mode (Continuous Testing)

```bash
dotnet watch --project tests/Behind.Tests/Behind.Tests.csproj test
```

## Database Setup

### Create Database

```bash
cd Behind

# Using SQLite (development)
# Database automatically created on first run

# If needed, manually initialize
dotnet ef database update \
    --project src/Core/Behind.Core.Database \
    --startup-project src/Core/Behind.Core.Database.Migrator
```

### Create Migrations

```bash
cd Behind

# Create new migration
dotnet ef migrations add "AddUserTable" \
    --project src/Core/Behind.Core.Database \
    --output-dir Migrations

# Apply migrations
dotnet ef database update

# Remove last migration (before applied)
dotnet ef migrations remove
```

### Connection Strings

**Development** (appsettings.Development.json):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

**Production** (appsettings.json):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.example.com;Port=5432;Database=behind;User=postgres;Password=***"
  }
}
```

## Running the Application

### Option 1: Direct Execution

```bash
cd Behind

# Run the API service
dotnet run \
    --project src/Core/Behind.Core.WebApi/Behind.Core.WebApi.csproj

# Application runs at https://localhost:5001
```

### Option 2: Aspire Orchestration (Recommended)

```bash
cd Behind/src/Aspire

# Run Aspire dashboard and services
dotnet run --project Behind.AppHost/Behind.AppHost.csproj

# Services coordinatedAutomatically via Aspire
# Dashboard at http://localhost:18888
```

### Option 3: Docker

```bash
# Build Docker image
docker build -t behind:latest .

# Run container
docker run -p 5001:5001 \
    -e ConnectionStrings__DefaultConnection="Data Source=/data/app.db" \
    -v behind-data:/data \
    behind:latest
```

## API Access

### OpenAPI Documentation

```
https://localhost:5001/openapi/v1.json  # OpenAPI specification
https://localhost:5001/scalar/v1         # Scalar UI
https://localhost:5001/swagger/v1        # Swagger UI (if enabled)
```

### Example API Calls

```bash
# Get user (requires auth)
curl -X GET https://localhost:5001/api/users/123 \
    -H "Authorization: Bearer <token>"

# Create user (public)
curl -X POST https://localhost:5001/api/users \
    -H "Content-Type: application/json" \
    -d '{
      "email": "user@example.com",
      "displayName": "John Doe",
      "password": "Secure@Pass123"
    }'

# Health check
curl https://localhost:5001/health
```

## Logging and Debugging

### View Logs

```bash
# Real-time log output
dotnet run --project src/Core/Behind.Core.WebApi/Behind.Core.WebApi.csproj

# Structured logging
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

### Debug in VS Code

**Launch Configuration** (.vscode/launch.json):
```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Launch (web)",
            "type": "coreclr",
            "request": "launch",
            "program": "${workspaceFolder}/Behind/bin/Debug/net10.0/Behind.Core.WebApi.dll",
            "args": [],
            "cwd": "${workspaceFolder}/Behind",
            "stopAtEntry": false,
            "serverReadyAction": {
                "pattern": "\\bNow listening on:\\s+(https?://\\S+)",
                "uriFormat": "{0}",
                "action": "openExternally"
            }
        }
    ]
}
```

## Development Workflow

### 1. Create Feature Branch

```bash
git checkout -b feature/user-authentication
```

### 2. Make Changes

```bash
# Edit files following CODING_STANDARDS.md
# Create tests in tests/Behind.Tests/
# Update documentation if needed
```

### 3. Run Tests

```bash
dotnet test tests/Behind.Tests/Behind.Tests.csproj
```

### 4. Build Verification

```bash
dotnet build Behind.slnx
dotnet build Behind.slnx -c Release
```

### 5. Commit and Push

```bash
git add .
git commit -m "feat: add user authentication"
git push origin feature/user-authentication
```

### 6. Create Pull Request

- Reference relevant issues
- Describe changes and rationale
- Ensure CI/CD passes
- Request review from team

## Code Quality

### Static Analysis

```bash
# Run code analyzers (built-in)
dotnet build Behind.slnx /t:Analyzers

# Format code
dotnet format Behind.slnx

# Check formatting only
dotnet format Behind.slnx --verify-no-changes
```

### Test Coverage

```bash
dotnet test tests/Behind.Tests/Behind.Tests.csproj \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=lcov
```

### Code Review Checklist

- [ ] Follows CODING_STANDARDS.md
- [ ] No comments (code is self-documenting)
- [ ] Uses extension methods (C# 12 syntax)
- [ ] Immutable records/objects
- [ ] Async/await for I/O
- [ ] Functional patterns (Option, Try, Validation)
- [ ] Tests for public methods
- [ ] No code duplication
- [ ] Single responsibility
- [ ] SOLID principles applied

## Common Tasks

### Add New Endpoint

1. **Create DTO** (in Behind.Core.WebApi):
```csharp
public record CreateUserDto(string Email, string DisplayName);
```

2. **Create Domain Entity** (in Behind.Core.Domain):
```csharp
public sealed record HumanUser : User { }
```

3. **Create Repository** (in Behind.Core.Database):
```csharp
public class UserRepository : IRepository<User> { }
```

4. **Create Validator** (in Behind.Core.Database):
```csharp
public static class UserValidation
{
    public static Validation<HumanUser> ValidateNew(...) { }
}
```

5. **Create Controller Action** (in Behind.Core.WebApi):
```csharp
[HttpPost]
public async Task<IActionResult> CreateAsync(CreateUserDto dto) { }
```

6. **Add Tests**:
```csharp
public class UserControllerTests { }
```

### Add Database Migration

```bash
cd Behind

# Create migration
dotnet ef migrations add "AddUserTable" \
    --project src/Core/Behind.Core.Database

# Update database
dotnet ef database update
```

### Add Service Dependency

**Register in Startup**:
```csharp
extension(IServiceCollection)
{
    public IServiceCollection AddMyService(this IServiceCollection services)
    {
        services.AddScoped<IMyService, MyService>();
        return services;
    }
}
```

**Inject in Consumer**:
```csharp
public class MyController
{
    private readonly IMyService service;
    
    public MyController(IMyService service)
    {
        this.service = service;
    }
}
```

## Troubleshooting

### Build Fails

```bash
# Clean build
dotnet clean Behind.slnx
dotnet restore Behind.slnx
dotnet build Behind.slnx

# Check SDK version
dotnet --version

# Verify no corrupted packages
rm -r ~/.nuget/packages
dotnet restore
```

### Tests Fail

```bash
# Run with verbose output
dotnet test tests/Behind.Tests/Behind.Tests.csproj --verbosity normal

# Run single test
dotnet test tests/Behind.Tests/Behind.Tests.csproj \
    --filter "TestClassName.TestMethodName"

# Check test database
# SQLite in-memory used for tests
```

### Database Issues

```bash
# View current migrations
dotnet ef migrations list --project src/Core/Behind.Core.Database

# Revert to previous migration
dotnet ef database update "LastGoodMigration"

# Start from scratch
rm Behind/app.db  # Delete SQLite file
dotnet ef database update
```

### Port Already in Use

```bash
# Specify different port
ASPNETCORE_URLS="https://localhost:5002" dotnet run

# Or in launchSettings.json
{
  "applicationUrl": "https://localhost:5002"
}
```

## Performance Tips

### Optimize Build Times
```bash
# Use incremental builds
dotnet build --incremental

# Parallel build
dotnet build -m -j 4

# Skip unnecessary projects
dotnet build src/Core/Behind.Core.WebApi
```

### Optimize Test Execution
```bash
# Run tests in parallel
dotnet test --parallel

# Filter to specific category
dotnet test --filter "Category=UnitTest"

# Run only tests in specific file
dotnet test tests/Behind.Tests/UserRepositoryTests.cs
```

## Environment Variables

**Development**:
```bash
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="https://localhost:5001"
```

**Production**:
```bash
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="https://0.0.0.0:80"
export ConnectionStrings__DefaultConnection="Host=db;Database=behind"
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build and Test

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build Behind.slnx
      - run: dotnet test tests/Behind.Tests/Behind.Tests.csproj
```

## Documentation

- [PROJECT_OVERVIEW.md](./PROJECT_OVERVIEW.md) - Architecture and stack
- [CODING_STANDARDS.md](./CODING_STANDARDS.md) - Mandatory standards
- [DESIGN_PATTERNS.md](./DESIGN_PATTERNS.md) - Architectural patterns
- [FUNCTIONAL_PROGRAMMING.md](./FUNCTIONAL_PROGRAMMING.md) - Monad patterns
- [DATABASE_ARCHITECTURE.md](./DATABASE_ARCHITECTURE.md) - Data layer
- [WEBAPI_CONVENTIONS.md](./WEBAPI_CONVENTIONS.md) - API design
- [DOMAIN_MODEL.md](./DOMAIN_MODEL.md) - Entity design
- [LIMITATIONS_AND_ISSUES.md](./LIMITATIONS_AND_ISSUES.md) - Known issues

## Getting Help

1. **Check Documentation** - Start with relevant guide above
2. **Search Issues** - Look for similar problems
3. **Ask Questions** - Open discussion issue
4. **Debug Locally** - Use VS Code debugger
5. **Check Logs** - Review detailed error messages

## Quick Reference

```bash
# Build and Test
dotnet build Behind.slnx
dotnet test tests/Behind.Tests/Behind.Tests.csproj

# Run Application
dotnet run --project src/Core/Behind.Core.WebApi/Behind.Core.WebApi.csproj

# Database
dotnet ef migrations add "MigrationName" --project src/Core/Behind.Core.Database
dotnet ef database update

# Format Code
dotnet format Behind.slnx

# View API Docs
# Open https://localhost:5001/scalar/v1
```

---

**Next Steps**:
1. Read CODING_STANDARDS.md for project conventions
2. Review FUNCTIONAL_PROGRAMMING.md to understand monads
3. Explore codebase structure
4. Create feature branch for first contribution
5. Submit pull request following guidelines

Happy coding! 🚀
