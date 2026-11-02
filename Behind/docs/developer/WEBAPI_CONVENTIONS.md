# WebApi Conventions

This document outlines the conventions, patterns, and best practices for building REST APIs in the Behind project.

## Overview

The Behind WebApi layer is built on:
- **ASP.NET Core 10.0.8**
- **OpenAPI 10.0.10** with Scalar UI
- **JWT Bearer Authentication**
- **Structured Error Handling**
- **Railway-Oriented Programming** (monadic returns)

## Startup Configuration

### WebApplicationBuilder Extensions

The `Behind.Common.WebApi.WebApplicationBuilderExtensions` provides fluent configuration:

```csharp
extension(WebApplicationBuilder builder)
{
    public WebApplicationBuilder AddOpenApi()
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        });
        return builder;
    }

    public WebApplicationBuilder AddOpenTelemetry()
    {
        // Configure distributed tracing, metrics, and logging
        // Via configuration file (appsettings.json)
    }
}
```

### Static Frontend

`UseCore()` serves the JavaScript frontend embedded from `Behind.Core.WebApi/Frontend` (RequireJS, jQuery, Knockout, Sammy; controllers/models/viewmodels/views layout). `index.html` is the default file. JS, CSS and HTML are minified in memory (once per file version) and responses are Brotli/Gzip compressed by default, with ETag revalidation.

Configure through the `Frontend:StaticFiles` section (for example in `frontend.json`):

```json
{ "Frontend": { "StaticFiles": { "Minify": true, "Compress": true, "DefaultFile": "index.html", "CacheControl": "no-cache" } } }
```

Files named `*.min.*` are served as-is. Third-party libraries live in `Frontend/libraries`.

Images (png, jpg, gif, webp, avif, ico, svg) are served unchanged with the correct content type and the same cache/security headers; only SVG is compressed. If a `wwwroot` directory exists in the content root, its files are served in addition to the embedded frontend and take precedence over embedded files with the same path.

### Sites Settings

The `Frontend:Sites` section (`SitesConfiguration`, for example in `frontend.json`) configures the sites feature:

```json
{ "Frontend": { "Sites": { "IndexSlug": "home", "MaxImageBytes": 5242880 } } }
```

- `IndexSlug`: when a site with this slug (normalized like any slug) is live, `GET /sites/index` serves it and the frontend shows it at `#/` instead of the built-in welcome page. Empty or missing means no custom index.
- `MaxImageBytes`: the largest accepted site image upload (default 5 MB, zero or negative falls back to the default). Larger uploads answer `413` with `{ "maxBytes": ... }`. The Kestrel request body limit (30 MB by default) still applies on top of it.

`GET /sites/settings` (`Site.Read`) returns both values to the editor.

### News Endpoints

| Endpoint | Access |
|----------|--------|
| `GET /news?page&pageSize&category` | public, the live version of the news that is running now, and the active categories |
| `GET /news/{id}` | public, the live version, only while the news is running |
| `GET /news/categories` | public, active categories |
| `GET /news/{id}/comments?page&pageSize` | public, `canDelete` is set for the author and for `News.Delete` holders, `canEdit` for the author and for `News.Manage` holders, `updatedAt` comes from the audit log |
| `POST /news/{id}/comments` | any signed in user |
| `POST /news/comments/{id}` | edit: the comment author or `News.Manage`, body `{ "content": ... }`, inserts a new comment version, `204` |
| `POST /news/comments/{id}/delete` | the comment author or `News.Delete` |
| `GET /news/manage`, `GET /news/categories/manage` | `News.Read`, with `search`, `letter`, `deleted` (`RawDatabase.Manage` for `deleted`) |
| `GET /news/{id}/versions`, `GET /news/{id}/versions/{version}` | `News.Read`, the version list and one version in full |
| `POST /news`, `POST /news/{id}/versions` | `News.Write`, create / save a new version, `publish: true` also makes it live |
| `POST /news/{id}/versions/{version}/activate` | `News.Write`, make a version live |
| `POST /news/categories`, `POST /news/categories/{id}`, `POST /news/images` | `News.Write`, saving a category inserts a new version |
| `POST /news/{id}/delete`, `POST /news/categories/{id}/delete` | `News.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

Ids in the URLs are the key of the entity, responses report the `version` that was saved or shown. The managed lists share `WithManagedList` (`ManagedListExtensions`), which validates the letter filter and the deleted switch for `Sites` and `News`. Saving answers `409` with `{ "reason": ... }` (`unknown-category`, `invalid-schedule`, `version-conflict`, `name-taken`, `unknown-image`).

### Articles Endpoints

Articles follow the news endpoints under `/articles` (`ArticleRoutesExtensions.MapArticleEndpoints`), without comments, schedule and highlight, and with their own categories.

| Endpoint | Access |
|----------|--------|
| `GET /articles?page&pageSize&category&search` | public, the live version of the published articles and the active article categories; `search` matches the title or the content of the live version, ignoring case |
| `GET /articles/{id}` | public, the live version |
| `GET /articles/categories` | public, active article categories |
| `GET /articles/manage`, `GET /articles/categories/manage` | `Article.Read`, with `search` (title or content for articles), `letter`, `deleted` (`RawDatabase.Manage` for `deleted`); the article status is `draft` or `published` |
| `GET /articles/{id}/versions`, `GET /articles/{id}/versions/{version}` | `Article.Read`, the version list and one version in full |
| `POST /articles`, `POST /articles/{id}/versions` | `Article.Write`, create / save a new version, `publish: true` also makes it live |
| `POST /articles/{id}/versions/{version}/activate` | `Article.Write`, make a version live |
| `POST /articles/categories`, `POST /articles/categories/{id}`, `POST /articles/images` | `Article.Write`, saving a category inserts a new version |
| `POST /articles/{id}/delete`, `POST /articles/categories/{id}/delete` | `Article.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

The responses reuse `NewsCategoryItem`, `NewsSavedResponse` and `NewsConflictResponse` (reasons `unknown-category`, `version-conflict`, `name-taken`, `unknown-image`).

### Startup Example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder
    .AddOpenApi()
    .AddOpenTelemetry()
    .Services
        .AddDatabaseServices(builder.Configuration)
        .AddAuthenticationServices(builder.Configuration)
        .AddApiServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app
    .UseHttpsRedirection()
    .UseAuthentication()
    .UseAuthorization()
    .MapControllers()
    .Run();
```

## Controller Structure

### Base Pattern

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;
    private readonly ILogger<UsersController> logger;

    public UsersController(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UsersController> logger)
    {
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
        this.logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAsync(Guid id)
    {
        var userOption = await unitOfWork.Users.TryFindAsync(id);

        return userOption.Match(
            none: () =>
            {
                logger.LogWarning("User {UserId} not found", id);
                return NotFound(new ApiErrorResponse("USER_NOT_FOUND", "User not found"));
            },
            some: user =>
            {
                var dto = mapper.Map<UserDto>(user);
                return Ok(dto);
            }
        );
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiValidationResponse))]
    public async Task<IActionResult> CreateUserAsync(CreateUserDto dto)
    {
        var validation = ValidateCreateUser(dto);

        return await validation.Match(
            invalid: errors =>
            {
                logger.LogWarning("User creation validation failed: {@Errors}", errors);
                return BadRequest(new ApiValidationResponse(errors));
            },
            valid: async registration =>
            {
                var user = new HumanUser(
                    displayName: registration.DisplayName,
                    emails: new[] { registration.Email },
                    roles: None
                );

                await unitOfWork.Users.AddAsync(user);
                await unitOfWork.CommitAsync();

                var responseDto = mapper.Map<UserDto>(user);
                return CreatedAtAction(nameof(GetUserAsync), new { id = user.Id }, responseDto);
            }
        );
    }
}
```

## Request/Response Models

### DTO Pattern

**Request DTOs** - Input validation constraints:

```csharp
public record CreateUserDto(
    [Required]
    [EmailAddress]
    string Email,

    [Required]
    [StringLength(100, MinimumLength = 1)]
    string DisplayName,

    [Required]
    [StringLength(128, MinimumLength = 8)]
    string Password
);

public record UpdateUserDto(
    [StringLength(100)]
    string? DisplayName,

    [StringLength(128, MinimumLength = 8)]
    string? NewPassword
);
```

**Response DTOs** - Serializable projections:

```csharp
public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    IEnumerable<RoleDto> Roles,
    DateTime CreatedAt
);

public record RoleDto(
    Guid Id,
    string Name,
    IEnumerable<GrantDto> Grants
);

public record GrantDto(
    Guid Id,
    string Name,
    string Description
);
```

### Error Response Models

```csharp
public record ApiErrorResponse(
    string Code,
    string Message,
    string? Details = null
);

public record ApiValidationResponse(
    IEnumerable<ValidationError> Errors,
    string Message = "Validation failed")
{
    public ApiValidationResponse(IEnumerable<Error> errors)
        : this(errors.Select(e => new ValidationError(e.Code, e.Message))) { }
}

public record ValidationError(
    string Code,
    string Message
);

public record PaginatedResponse<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public PaginatedResponse(PagedResult<T> result)
        : this(result.Items, result.Page, result.PageSize, result.TotalCount, result.TotalPages) { }
}
```

## HTTP Conventions

### HTTP Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| GET | Retrieve resource(s) | 200 OK or 404 Not Found |
| POST | Create new resource | 201 Created with Location header |
| PUT | Replace entire resource | 200 OK or 204 No Content |
| PATCH | Partial update | 200 OK or 204 No Content |
| DELETE | Remove resource | 204 No Content or 200 OK |

### Status Codes

| Code | Usage |
|------|-------|
| **2xx Success** | |
| 200 OK | Request succeeded |
| 201 Created | Resource created (POST) |
| 204 No Content | Success with no body |
| **4xx Client Error** | |
| 400 Bad Request | Invalid input/validation failure |
| 401 Unauthorized | Authentication required |
| 403 Forbidden | Authorization denied |
| 404 Not Found | Resource not found |
| 409 Conflict | Business logic conflict |
| **5xx Server Error** | |
| 500 Internal Server Error | Unexpected server error |

### Example Endpoints

**GET - Retrieve**
```csharp
[HttpGet("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetUserAsync(Guid id)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    return userOption.Match(
        () => NotFound(),
        user => Ok(mapper.Map<UserDto>(user))
    );
}
```

**POST - Create**
```csharp
[HttpPost]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateUserAsync(CreateUserDto dto)
{
    var validation = ValidateAndCreate(dto);
    
    return await validation.Match(
        errors => BadRequest(new ApiValidationResponse(errors)),
        async user =>
        {
            await unitOfWork.Users.AddAsync(user);
            await unitOfWork.CommitAsync();
            
            var dto = mapper.Map<UserDto>(user);
            return CreatedAtAction(nameof(GetUserAsync), new { id = user.Id }, dto);
        }
    );
}
```

**PUT - Full Replace**
```csharp
[HttpPut("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> UpdateUserAsync(Guid id, UpdateUserDto dto)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    
    return await userOption.Match(
        async () => NotFound(),
        async user =>
        {
            var updatedUser = user with
            {
                DisplayName = dto.DisplayName ?? user.DisplayName
            };
            
            unitOfWork.Users.Update(updatedUser);
            await unitOfWork.CommitAsync();
            
            return Ok(mapper.Map<UserDto>(updatedUser));
        }
    );
}
```

**DELETE - Remove**
```csharp
[HttpDelete("{id:guid}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DeleteUserAsync(Guid id)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    
    return await userOption.Match(
        async () => NotFound(),
        async user =>
        {
            await unitOfWork.Users.DeleteAsync(id);
            await unitOfWork.CommitAsync();
            return NoContent();
        }
    );
}
```

**GET - List with Filtering & Pagination**
```csharp
[HttpGet]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> ListUsersAsync(
    [FromQuery] string? email,
    [FromQuery] string? role,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    // Build filter
    var filter = BuildUserFilter(email, role);
    
    // Execute paged query
    var pagedResult = await unitOfWork.Users
        .FindPagedAsync(filter, page, pageSize);
    
    var dtos = pagedResult.Items
        .Select(u => mapper.Map<UserDto>(u));
    
    return Ok(new PaginatedResponse<UserDto>(
        Items: dtos,
        Page: pagedResult.Page,
        PageSize: pagedResult.PageSize,
        TotalCount: pagedResult.TotalCount,
        TotalPages: pagedResult.TotalPages
    ));
}
```

## Authentication & Authorization

### JWT Configuration

```csharp
extension(IServiceCollection)
{
    public IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection("Authentication:Jwt")
            .Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings required");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SecretKey)
                    ),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy =>
                policy.RequireRole("Admin"));
            
            options.AddPolicy("ModerateContent", policy =>
                policy.RequireClaim("permissions", "moderate_content"));
        });

        return services;
    }
}
```

### Endpoint Protection

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // Require authentication for entire controller
public class AdminController : ControllerBase
{
    [HttpPost("users/{id}/ban")]
    [Authorize(Policy = "Admin")]  // Require specific authorization
    public async Task<IActionResult> BanUserAsync(Guid id)
    {
        // Only admin can ban users
    }
    
    [HttpGet("settings")]
    [AllowAnonymous]  // Override class-level [Authorize]
    public IActionResult GetPublicSettings()
    {
        // Public endpoint
    }
}
```

### Role & Permission Checks

```csharp
public static class AuthorizationExtensions
{
    extension(User)
    {
        public bool HasPermission(Grant grant) =>
            Roles.Match(
                () => false,
                roles => roles.Any(r => r.Grants.Match(
                    () => false,
                    grants => grants.Contains(grant)
                ))
            );
        
        public bool HasRole(string roleName) =>
            Roles.Match(
                () => false,
                roles => roles.Any(r => r.Name == roleName)
            );
    }
}

// In controller
if (!currentUser.HasPermission(new Grant("moderate_content")))
{
    return Forbid("Insufficient permissions");
}
```

## Error Handling

### Global Exception Handler

```csharp
extension(WebApplication)
{
    public WebApplication UseGlobalExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var logger = context.RequestServices.GetRequiredService<ILogger>();
                
                logger.LogError(exception, "Unhandled exception");
                
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = exception switch
                {
                    ValidationException => StatusCodes.Status400BadRequest,
                    NotFoundException => StatusCodes.Status404NotFound,
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    _ => StatusCodes.Status500InternalServerError
                };
                
                var response = new ApiErrorResponse(
                    Code: GetErrorCode(exception),
                    Message: exception?.Message ?? "Internal server error",
                    Details: app.Environment.IsDevelopment() ? exception?.StackTrace : null
                );
                
                await context.Response.WriteAsJsonAsync(response);
            });
        });
        
        return app;
    }
}
```

### Functional Error Handling

```csharp
public async Task<IActionResult> ProcessAsync(UserCommandDto dto)
{
    var result = await ValidateCommand(dto)
        .Bind(cmd => ExecuteCommand(cmd))
        .Run();  // Execute and catch any exceptions
    
    return result.Match(
        exception => HandleException(exception),
        user => Ok(mapper.Map<UserDto>(user))
    );
}

private Exceptional<ExecutedCommand> ExecuteCommand(ValidatedCommand cmd)
{
    try
    {
        // Business logic
        return new ExecutedCommand(/* ... */);
    }
    catch (Exception ex)
    {
        return ex;  // Implicit conversion Exception -> Exceptional<T>
    }
}
```

## OpenAPI Documentation

### Documenting Endpoints

```csharp
[HttpPost("register")]
[AllowAnonymous]
[Tags("Authentication")]
[Summary("Register a new user")]
[Description("Creates a new user account with the provided details")]
[ProducesResponseType(StatusCodes.Status201Created, Type = typeof(UserDto))]
[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiValidationResponse))]
[ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiErrorResponse))]
public async Task<IActionResult> RegisterAsync(
    [FromBody]
    [Description("User registration data")]
    CreateUserDto dto)
{
    // Implementation
}
```

### Security Scheme

```csharp
public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components = new OpenApiComponents
        {
            SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
            {
                ["bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme"
                }
            }
        };
        
        return Task.CompletedTask;
    }
}
```

### Scalar UI Setup

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Behind API")
            .WithTheme(ScalarTheme.BluePlanet)
            .WithDarkMode(true)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}
```

## Request/Response Flow

### Typical Flow

```
1. HTTP Request arrives
   ↓
2. Route matching (Controller/Action)
   ↓
3. Model binding (DTO deserialization)
   ↓
4. Validation filters (DataAnnotations)
   ↓
5. Authorization checks
   ↓
6. Action execution
   ├─ Business logic (monadic)
   ├─ Data access
   └─ Error handling (Railway-Oriented)
   ↓
7. Result mapping (to response DTO)
   ↓
8. HTTP Response serialization (JSON)
   ↓
9. Response sent to client
```

## Middleware Configuration

### Standard Order

```csharp
var app = builder.Build();

// Error handling (must be first)
app.UseGlobalExceptionHandler();

// HTTPS/Security
app.UseHttpsRedirection();
app.UseHsts();

// CORS
app.UseCors("AllowFrontend");

// Authentication/Authorization
app.UseAuthentication();
app.UseAuthorization();

// Logging
app.UseStructuredLogging();

// API Documentation
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Routing
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

## Content Negotiation

### JSON-Only APIs

```csharp
services.AddControllers(options =>
{
    options.OutputFormatters.RemoveType<XmlDataContractSerializerOutputFormatter>();
});

services.AddMvc(options =>
{
    options.FormatterMappings.SetMediaTypeMappingForFormat(
        "json",
        MediaTypeHeaderValue.Parse("application/json")
    );
});
```

### Typed JSON Handling

```csharp
services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new OptionJsonConverterFactory());
    options.JsonSerializerOptions.Converters.Add(new ExceptionalJsonConverterFactory());
});
```

## Best Practices

### ✓ DO

- Use DTOs for request/response
- Return monadic types from services
- Document endpoints with OpenAPI attributes
- Use consistent error response format
- Implement pagination for list endpoints
- Use ILogger for structured logging
- Handle exceptions at boundary (controller)
- Use CancellationToken throughout

### ✗ DON'T

- Return domain entities directly
- Mix validation errors with HTTP errors
- Use bare exceptions for control flow
- Skip authorization checks
- Expose internal exception details in production
- Use bare `HttpContext.Request` without helpers
- Mix concerns (business logic in controller)

## Common Patterns

### Result-Based Response

```csharp
public async Task<IActionResult> GetAsync(Guid id)
{
    var result = await service.GetAsync(id);
    
    return result.Match<IActionResult>(
        ex => StatusCode(StatusCodes.Status500InternalServerError,
            new ApiErrorResponse("ERROR", ex.Message)),
        user => Ok(mapper.Map<UserDto>(user))
    );
}
```

### Validation-Based Response

```csharp
public async Task<IActionResult> CreateAsync(CreateDto dto)
{
    var result = await service.ValidateAndCreateAsync(dto);
    
    return await result.Match<Task<IActionResult>>(
        errors => Task.FromResult<IActionResult>(
            BadRequest(new ApiValidationResponse(errors))
        ),
        async entity =>
        {
            await service.SaveAsync(entity);
            var dto = mapper.Map<EntityDto>(entity);
            return CreatedAtAction(nameof(GetAsync), new { id = entity.Id }, dto);
        }
    );
}
```

## Summary

| Aspect | Convention |
|--------|-----------|
| Base Route | `/api/[controller]` |
| Method Naming | Verb + ResourceAsync (GetUserAsync, CreateUserAsync) |
| Response DTOs | ProjectedData, not domain models |
| Errors | ApiErrorResponse with Code and Message |
| Validation | Validation<T> with accumulated errors |
| Authentication | JWT Bearer with OpenAPI documentation |
| Pagination | PagedResult with Items, Page, TotalCount |
| Status Codes | 2xx success, 4xx client error, 5xx server error |
| Documentation | OpenAPI with Scalar UI |

---

**Next Steps**:
- Review DOMAIN_MODEL.md for entity design
- Check FUNCTIONAL_PROGRAMMING.md for error handling patterns
- See DATABASE_ARCHITECTURE.md for data access
- Configure Email Services with [EMAIL_SENDER_IMPLEMENTATION.md](./EMAIL_SENDER_IMPLEMENTATION.md) for user notifications and account management
