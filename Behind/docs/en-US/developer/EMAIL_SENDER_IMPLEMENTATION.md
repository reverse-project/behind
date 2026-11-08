# EmailSender Implementation and Tests Summary

## Overview
This document summarizes the implementation of the EmailSender functionality and its comprehensive test suite for the Behind.Core.WebApi project.

## Implementation Details

### Components

#### 1. Templates Class (`Behind.Common.Templating.Templates`)
A collection-like container for managing email templates with the following features:

**Core Functionality:**
- `Add(Template template)` - Add a Template instance to the collection
- `Add(string name, string content)` - Convenience method to add a SimpleTemplate
- `Remove(string name)` - Remove template(s) by name
- `Remove(Template template)` - Remove specific template instance
- `Get(string name) → Option<Template>` - Retrieve template by name (returns Option)
- `GetOrDefault(string name, Template defaultTemplate) → Template` - Retrieve or fallback
- `IEnumerable<Template>` - Full enumeration support

**Example Usage:**
```csharp
var templates = new Templates();

templates.Add("ConfirmationEmail", "<p>Please confirm {{Email}}</p>");
templates.Add("PasswordReset", "<p>Reset your password {{ResetLink}}</p>");

var template = templates.GetOrDefault("ConfirmationEmail", 
    new SimpleTemplate("default", "No template"));

var isSome = templates.Get("ConfirmationEmail")
    .Match(
        none: () => false,
        some: t => true
    );
```

**Key Design Decisions:**
- Immutable operations: `Remove`, `Get`, `Add` maintain thread-safe semantics
- Option-based retrieval: `Get` returns `Option<Template>` to handle missing templates elegantly
- Enumerable interface: Enables LINQ operations and collections patterns
- Null validation: All public methods validate arguments with `ArgumentNullException`

#### 2. SimpleTemplate Record (`Behind.Common.Templating.Template`)
Template base type with two implementations:
- **Template (abstract record)**: Base type with Name and Content
- **SimpleTemplate (sealed record)**: Full template implementation with parameter rendering

**Features:**
```csharp
var template = new SimpleTemplate("greeting", "Hello {{name}}, welcome {{greeting}}!");

var rendered = template
    .AddParameter(new Parameter("name", "Alice"))
    .AddParameter(new Parameter("greeting", "aboard"))
    .Render();

// Result: "Hello Alice, welcome aboard!"
```

**Parameter Mechanics:**
- Placeholders use `{{PropertyName}}` syntax
- Parameters replaced sequentially via `string.Replace`
- Immutable chain: `AddParameter` returns new SimpleTemplate instance
- Unmatched placeholders left in rendered content

#### 3. EmailSender Base Class (`EmailSender.cs`)
- **Abstract class** implementing `IEmailSender` and `IEmailSender<ApplicationUser>`
- Provides template-based email generation for common scenarios:
  - Email confirmation links
  - Password reset codes
  - Password reset links
- Uses `DefaultEmailTemplates` for out-of-box template content

#### 2. Implementations
- **LoggerEmailSender**: Logs the recipient and subject only, never the body, so sign-in links and codes stay out of the logs
- **SmtpEmailSender**: Sends emails via SMTP with:
  - Configurable authentication (Basic or No auth)
  - Sender address from `SmtpEmailConfiguration.From`, falling back to the Basic authentication username; sending fails when neither is configured
  - SMTP failures propagate to the caller (the magic link mail service logs them)
  - Optional custom Templates override via `SmtpEmailConfiguration.Templates`
  - Fallback to `DefaultEmailTemplates` when no custom templates provided
  - Per-email-type template resolution with `GetOrDefault` fallback

#### 3. Extension Method (`EmailSenderExtensions.cs`)
```csharp
extension(EmailConfiguration configuration)
{
    public EmailSender GetInstance(IServiceProvider serviceProvider) => configuration switch
    {
        LoggerEmailConfiguration _ => new LoggerEmailSender(...),
        SmtpEmailConfiguration _ => new SmtpEmailSender(...),
        _ => throw new NotSupportedException(...)
    };
}
```
- Creates appropriate EmailSender instance based on EmailConfiguration type
- Resolves dependencies from the service provider
- Supports:
  - `LoggerEmailConfiguration` → `LoggerEmailSender`
  - `SmtpEmailConfiguration` → `SmtpEmailSender` with optional Templates support
  - Throws `NotSupportedException` for unknown types

#### 4. DefaultEmailTemplates (`DefaultEmailTemplates.cs`)
Static class providing default email templates:
```csharp
public static class DefaultEmailTemplates
{
    public static readonly SimpleTemplate ConfirmationEmail = 
        new("ConfirmationEmail", "<p>Please confirm your email ({{Email}}) ...</p>");
    
    public static readonly SimpleTemplate PasswordResetCodeEmail = 
        new("PasswordResetCodeEmail", "<p>Your code: {{ResetCode}}</p>");
    
    public static readonly SimpleTemplate PasswordResetLinkEmail = 
        new("PasswordResetLinkEmail", "<p>Reset password: {{ResetLink}}</p>");
}
```

#### 5. SmtpEmailConfiguration Extension
Updated record with optional Templates parameter:
```csharp
public sealed record SmtpEmailConfiguration(
    string SmtpServer, 
    int Port, 
    Ssl Ssl, 
    Option<Authentication> Authentication,
    Option<Templates> Templates,  // Optional custom templates
    Option<string> From = default // Optional sender address
) : EmailConfiguration("Smtp");
```

**Template Resolution Flow (SmtpEmailSender):**
1. Check if `Templates` option has value
2. If present (Some): Use `GetOrDefault` to fetch template or fallback to default
3. If absent (None): Use default template directly
4. Apply parameters and render

#### 6. WebApiExtensions Integration
The `AddEmail()` method in `WebApiExtensions.cs`:
```csharp
WebApplicationBuilder AddEmail()
{
    // Load configuration (email.json)
    builder.Configuration.AddJsonFile("email.json", false, true);
    
    EmailConfiguration emailConfiguration = builder.Configuration
        .GetSection(EmailConfiguration.SectionName)
        .GetOptionAware<EmailConfiguration>() ?? new LoggerEmailConfiguration();
    
    // Register as singleton (immutable & environment-specific)
    builder.Services.AddSingleton(Options.Create(emailConfiguration));
    
    // Register implementations as transient (per-request, thread-safe)
    builder.Services.AddTransient<IEmailSender>(emailConfiguration.GetInstance);
    builder.Services.AddTransient<IEmailSender<ApplicationUser>>(emailConfiguration.GetInstance);
    
    return builder;
}
```

**Scope Analysis:**
- ✅ **EmailConfiguration**: Registered as **Singleton** - Configuration is immutable and environment-specific
- ✅ **IEmailSender/IEmailSender<ApplicationUser>**: Registered as **Transient** - New instance per request to ensure thread safety and proper resource cleanup
- ✅ **LoggerEmailSender**: Created per request (Transient via factory)
- ✅ **SmtpEmailSender**: Created per request (Transient via factory) with SmtpEmailConfiguration options injected

## Test Suite

### Templates Tests (`TemplatesTests.cs`)
Comprehensive test coverage for the Templates collection class (22 tests):

**Add(Template) Tests:**
- `Add_Template_MakesItRetrievableByName` - Verify add-then-get pattern
- `Add_Template_Null_ThrowsArgumentNullException` - Null validation
- `Add_Template_MultipleTemplates_AreAllEnumerable` - LINQ/collection support

**Add(name, content) Tests:**
- `Add_NameAndContent_CreatesSimpleTemplate` - Convenience method works
- `Add_NameAndContent_NullName_ThrowsArgumentNullException` - Name validation
- `Add_NameAndContent_NullContent_ThrowsArgumentNullException` - Content validation

**Remove(name) Tests:**
- `Remove_Name_RemovesMatchingTemplate` - Successful removal
- `Remove_Name_LeavesOtherTemplatesUntouched` - No side effects
- `Remove_Name_NonExistentName_DoesNothing` - Idempotent
- `Remove_Name_Null_ThrowsArgumentNullException` - Null validation

**Remove(Template) Tests:**
- `Remove_Template_RemovesExactInstance` - Reference equality
- `Remove_Template_NotAdded_DoesNothing` - Safe if not present
- `Remove_Template_Null_ThrowsArgumentNullException` - Null validation

**Enumeration Tests:**
- `GetEnumerator_NoTemplates_YieldsNothing` - Empty collection
- `GetEnumerator_Generic_MatchesNonGeneric` - IEnumerable compliance

**Get Tests:**
- `Get_ExistingName_ReturnsSomeWithTemplate` - Happy path (Option)
- `Get_UnknownName_ReturnsNone` - Missing template handling
- `Get_DuplicateNames_ThrowsInvalidOperationException` - Duplicate detection

**GetOrDefault Tests:**
- `GetOrDefault_ExistingName_ReturnsMatchingTemplate` - Happy path
- `GetOrDefault_UnknownName_ReturnsDefaultTemplate` - Fallback works
- `GetOrDefault_NullName_ThrowsArgumentNullException` - Argument validation
- `GetOrDefault_NullDefaultTemplate_ThrowsArgumentNullException` - Argument validation

**Test Results:**
- **Total**: 22 tests
- **Passed**: 22 ✓
- **Failed**: 0

### Email Integration Tests

#### 1. `EmailSenderExtensionsTests.cs`
Tests the extension method factory logic:
- `GetInstance_WithLoggerEmailConfiguration_ReturnsLoggerEmailSender` ✓
- `GetInstance_WithSmtpEmailConfiguration_ReturnsSmtpEmailSender` ✓
- `GetInstance_WithUnsupportedConfiguration_ThrowsNotSupportedException` ✓
- `GetInstance_LoggerEmailConfiguration_DoesNotRequireSmtpOptions` ✓

#### 2. `LoggerEmailSenderTests.cs`
Tests the logger-based email sender:
- `SendEmailAsync_LogsEmailInformation` ✓
- `SendEmailAsync_CompletesSuccessfully` ✓
- `SendConfirmationLinkAsync_UsesCorrectTemplate` ✓
- `SendPasswordResetCodeAsync_UsesCorrectTemplate` ✓
- `SendPasswordResetLinkAsync_UsesCorrectTemplate` ✓

#### 3. `SmtpEmailSenderTests.cs`
Tests the SMTP-based email sender:
- `SendEmailAsync_WithValidSmtpConfiguration_LogsSuccessMessage` ✓
- `SendEmailAsync_WithNoAuthentication_DoesNotSetCredentials` ✓
- `SendConfirmationLinkAsync_CreatesCorrectEmailContent` ✓
- `SendPasswordResetCodeAsync_CreatesCorrectEmailContent` ✓
- `SendPasswordResetLinkAsync_CreatesCorrectEmailContent` ✓

#### 4. `WebApiExtensionsEmailTests.cs`
Integration tests for DI configuration:
- `AddEmail_RegistersEmailSenderAsIEmailSender` ✓
- `AddEmail_WithLoggerEmailConfiguration_ReturnsLoggerEmailSender` ✓
- `AddEmail_WithSmtpEmailConfiguration_ReturnsSmtpEmailSender` ✓
- `AddEmail_EmailSenderRegisteredAsTransient` ✓
- `AddEmail_CanResolveMultipleEmailSenderInstances` ✓

### Test Results
- **Total Tests**: 21
- **Passed**: 21 ✓
- **Failed**: 0
- **Duration**: ~251ms

**Total Email + Templates Tests**: 43 tests, all passing

## Configuration

### EmailConfiguration Options
The email configuration supports two providers with optional template customization:

#### Logger Configuration (Development)
```json
{
  "Email": {
    "Provider": "Logger"
  }
}
```

#### SMTP Configuration (Production) - With Default Templates
```json
{
  "Email": {
    "Provider": "Smtp",
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "From": "noreply@example.com",
    "Ssl": {
      "Enabled": true
    },
    "Authentication": {
      "Type": "Basic",
      "Username": "user@example.com",
      "Password": "secure-password"
    }
  }
}
```

#### SMTP Configuration - With Custom Templates
```json
{
  "Email": {
    "Provider": "Smtp",
    "SmtpServer": "smtp.example.com",
    "Port": 587,
    "Ssl": {
      "Enabled": true
    },
    "Authentication": {
      "Type": "No"
    },
    "Templates": {
      "ConfirmationEmail": "<html><body>Confirm {{Email}} at <a href=\"{{ConfirmationLink}}\">here</a></body></html>",
      "PasswordResetCodeEmail": "<html><body>Your code is {{ResetCode}}</body></html>",
      "PasswordResetLinkEmail": "<html><body>Click <a href=\"{{ResetLink}}\">here</a> to reset</body></html>"
    }
  }
}
```

**Template Override Behavior:**
- If `Templates` is provided in config, SmtpEmailSender uses those templates
- Missing templates in `Templates` fall back to `DefaultEmailTemplates`
- LoggerEmailSender always uses `DefaultEmailTemplates` (no override support)
- Parameter substitution works identically in both default and custom templates

### Parameter Reference

**ConfirmationEmail Parameters:**
- `{{Email}}` - User's email address
- `{{ConfirmationLink}}` - URL with confirmation token

**PasswordResetCodeEmail Parameters:**
- `{{Email}}` - User's email address
- `{{ResetCode}}` - Numeric or alphanumeric reset code

**PasswordResetLinkEmail Parameters:**
- `{{Email}}` - User's email address
- `{{ResetLink}}` - URL with reset token

## Design Patterns Used

### 1. Templates Collection Pattern
The `Templates` class provides a specialized collection for managing immutable email templates:
- **Option-based API**: `Get(string)` returns `Option<Template>` for functional error handling
- **Default-aware retrieval**: `GetOrDefault` enables safe fallback patterns
- **Immutable operations**: Templates are read-only; collection maintains reference integrity
- **IEnumerable compliance**: Full LINQ support while protecting internal list

**Example Pattern:**
```csharp
var customTemplates = new Templates();
customTemplates.Add("WelcomeEmail", "<p>Welcome {{Name}}!</p>");

var template = customTemplates
    .Get("WelcomeEmail")
    .Match(
        none: () => DefaultEmailTemplates.ConfirmationEmail,
        some: t => t
    );
```

### 2. Parameter Substitution via Immutable Chain
The `SimpleTemplate.AddParameter` method returns a new template instance:
```csharp
var rendered = template
    .AddParameter(new Parameter("field1", "value1"))
    .AddParameter(new Parameter("field2", "value2"))
    .Render();
```
- Each `AddParameter` creates new immutable SimpleTemplate
- Original template remains unchanged (functional purity)
- Parameters applied sequentially in call order

### 3. Extension Methods
The `extension(T)` syntax provides a clean DSL for adding configuration to types:
```csharp
extension(EmailConfiguration configuration)
{
    public EmailSender GetInstance(IServiceProvider serviceProvider) => ...
}
```

### 4. Factory Pattern
The `GetInstance` extension method acts as a factory, creating appropriate EmailSender instances based on configuration type with dependency resolution.

### 5. Dependency Injection
- Configuration-driven instantiation
- Service provider injection for resolving dependencies
- Transient lifetime for stateless services
- Optional Templates override at configuration level

### 6. Template Method Pattern
The abstract `EmailSender` class provides template methods for common email scenarios while allowing subclasses to implement actual sending logic and customize template resolution.

### 7. Strategy Pattern (Configuration-Based)
Template resolution strategies via `Option<Templates>`:
- **Default strategy**: Use `DefaultEmailTemplates` directly
- **Override strategy**: Use custom Templates with fallback to defaults

## Usage Examples

### Example 1: Using Default Templates with Logger
```csharp
// Configuration (development/appsettings.Development.json)
// No custom templates - uses DefaultEmailTemplates

// In your code
var emailSender = serviceProvider.GetRequiredService<IEmailSender>();
var user = new ApplicationUser { DisplayName = "Alice" };

await emailSender.SendConfirmationLinkAsync(
    user, 
    "alice@example.com", 
    "https://app.example.com/confirm?token=abc123"
);
// Output: Logger writes confirmation email to console
```

### Example 2: Custom Templates via Configuration
```csharp
// Configuration (email.json)
{
  "Email": {
    "Provider": "Smtp",
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "Ssl": { "Enabled": true },
    "Authentication": { "Type": "Basic", "Username": "...", "Password": "..." },
    "Templates": {
      "ConfirmationEmail": "<h1>Welcome!</h1><p>Confirm <a href='{{ConfirmationLink}}'>here</a></p>"
    }
  }
}

// SmtpEmailSender automatically uses custom template if present,
// falls back to DefaultEmailTemplates for missing templates
```

### Example 3: Programmatic Template Management
```csharp
var templates = new Templates();

templates.Add("WelcomeEmail", "<p>Welcome {{Name}}!</p>");
templates.Add("OrderConfirmation", "<p>Order #{{OrderId}} confirmed</p>");

var template = templates.GetOrDefault(
    "WelcomeEmail",
    new SimpleTemplate("fallback", "Default message")
);

var html = template
    .AddParameter(new Parameter("Name", "Bob"))
    .Render();
```

### Example 4: Safe Template Retrieval with Option
```csharp
var templates = new Templates();
templates.Add("greeting", "Hello {{name}}!");

var greeting = templates
    .Get("greeting")
    .Match(
        none: () => new SimpleTemplate("default", "No greeting available"),
        some: t => t
    )
    .AddParameter(new Parameter("name", "World"))
    .Render();

// Result: "Hello World!"
```

The implementation is designed for easy extension:

### Adding a New Email Provider
1. Create a new `EmailConfiguration` record:
   ```csharp
   public sealed record SendGridEmailConfiguration(...) : EmailConfiguration("SendGrid");
   ```

2. Create a new `EmailSender` implementation:
   ```csharp
   public sealed class SendGridEmailSender : EmailSender { ... }
   ```

3. Update the `GetInstance` extension method:
   ```csharp
   SendGridEmailConfiguration _ => new SendGridEmailSender(...),
   ```

4. Add corresponding tests in the test suite

### Adding New Email Templates
1. Add new methods to the abstract `EmailSender` class
2. Implement in subclasses as needed
3. Add tests for each implementation

## Best Practices

✅ **Immutable Configuration**: EmailConfiguration and Templates are sealed records/classes (immutable)
✅ **Functional Templates API**: Option-based retrieval with Match for safe error handling
✅ **Dependency Injection**: All dependencies resolved through DI container
✅ **Template Pattern**: Common functionality in base class, specifics in subclasses
✅ **Error Handling**: SmtpEmailSender lets SMTP failures propagate; ArgumentNullException for invalid inputs
✅ **Thread Safety**: Transient lifetime ensures no shared state between requests; immutable templates
✅ **Testing**: Comprehensive test coverage (43 tests) with NSubstitute mocking and xUnit
✅ **Configuration Flexibility**: Multiple providers with optional per-provider template overrides
✅ **Functional Purity**: `AddParameter` returns new template instance; original unchanged

## Verification Checklist

- [x] Templates class tests all pass (22/22)
- [x] Email sender tests all pass (21/21)
- [x] All email-related tests compile and run successfully (43/43 total)
- [x] Extension method implementation is correct
- [x] WebApiExtensions DI registration is properly scoped
- [x] Both LoggerEmailSender and SmtpEmailSender work correctly
- [x] Configuration loading from email.json works
- [x] Default fallback to DefaultEmailTemplates works
- [x] Custom template overrides work via SmtpEmailConfiguration.Templates
- [x] Email templates are generated correctly with parameter substitution
- [x] Service provider resolution works for transient services
- [x] Option-based template retrieval is safe and functional
- [x] Application builds and runs successfully with no warnings
