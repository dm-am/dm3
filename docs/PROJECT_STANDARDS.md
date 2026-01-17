# DM3 Project Standards Reference

**Version:** 1.3
**Last Updated:** 2026-01-17
**Based on:** 12-Factor App Methodology, Course Transcripts Analysis, Codebase Analysis

---

## Table of Contents
1. [12-Factor Compliance](#12-factor-compliance)
2. [Project Structure](#project-structure)
3. [Naming Conventions](#naming-conventions)
4. [Architecture Patterns](#architecture-patterns)
5. [Dependency Injection](#dependency-injection)
6. [Database & Data Access](#database--data-access)
7. [API Design](#api-design)
8. [Error Handling](#error-handling)
9. [Validation](#validation)
10. [Logging & Observability](#logging--observability)
11. [Testing Standards](#testing-standards)
12. [Configuration Management](#configuration-management)
13. [Message Queuing](#message-queuing)
14. [Authentication & Authorization](#authentication--authorization)
15. [Package Management](#package-management)

---

## 12-Factor Compliance

This project follows the [12-Factor App](https://12factor.net/) methodology:

| Factor | Implementation | Status |
|--------|---------------|--------|
| I. Codebase | Single Git repo, multiple deployments | ✅ |
| II. Dependencies | Central package management (Directory.Packages.props) | ✅ |
| III. Config | Environment variables + appsettings.json | ✅ |
| IV. Backing Services | Attached resources via configuration | ✅ |
| V. Build, Release, Run | Docker + CI/CD pipeline | ✅ |
| VI. Processes | Stateless (sessions in DB/Redis) | ✅ |
| VII. Port Binding | Self-contained Kestrel | ✅ |
| VIII. Concurrency | Horizontal scaling via containers | ✅ |
| IX. Disposability | Fast startup, graceful shutdown | ✅ |
| X. Dev/Prod Parity | Docker Compose for local dev | ✅ |
| XI. Logs | Serilog → stdout/OpenSearch | ✅ |
| XII. Admin Processes | EF Core migrations | ✅ |

---

## Project Structure

### Solution Organization
```
DM.sln
├── src/
│   ├── DM.Services.Core/              # Core utilities, logging, caching
│   ├── DM.Services.DataAccess/        # EF Core, MongoDB, entities
│   ├── DM.Services.Common/            # Shared business logic
│   ├── DM.Services.Authentication/    # Auth services
│   ├── DM.Services.Community/         # Community domain
│   ├── DM.Services.Forum/             # Forum domain
│   ├── DM.Services.Gaming/            # Gaming domain
│   ├── DM.Services.Notifications/     # Notifications
│   ├── DM.Services.MessageQueuing/    # RabbitMQ integration
│   ├── DM.Services.Search/            # OpenSearch
│   ├── DM.Web.API/                    # REST API
│   └── DM.Web.Core/                   # Web infrastructure
├── test/
│   ├── DM.Tests.Core/                 # Test utilities
│   └── DM.Services.{Domain}.Tests/    # Domain tests
├── frontend/
│   └── DM.Web.Modern.Temp/            # Vue.js frontend
├── docker/
│   └── docker-compose.yml
├── Directory.Build.props              # Shared build settings
└── Directory.Packages.props           # Central package versions
```

### Domain Module Structure
```
DM.Services.{Domain}/
├── BusinessProcesses/
│   └── {Feature}/
│       ├── Creating/
│       │   ├── I{Feature}CreatingService.cs
│       │   ├── {Feature}CreatingService.cs
│       │   ├── I{Feature}CreatingRepository.cs
│       │   ├── {Feature}CreatingRepository.cs
│       │   ├── I{Feature}Factory.cs
│       │   ├── {Feature}Factory.cs
│       │   └── Create{Feature}.cs         # DTO
│       ├── Reading/
│       ├── Updating/
│       └── Deleting/
├── Authorization/
│   ├── {Feature}Intention.cs              # Enum of intentions
│   └── {Feature}IntentionResolver.cs
├── Dto/
│   ├── Input/
│   │   ├── Create{Feature}.cs
│   │   └── Create{Feature}Validator.cs
│   └── Output/
│       └── {Feature}.cs
└── {Domain}Module.cs                      # Autofac module
```

---

## Naming Conventions

### C# Code

| Element | Convention | Example |
|---------|------------|---------|
| **Private fields** | `_camelCase` | `_repository`, `_identityProvider` |
| **Public properties** | `PascalCase` | `UserId`, `CreatedAt` |
| **Methods** | `PascalCase` | `CreateTopic`, `ValidateUser` |
| **Parameters** | `camelCase` | `userId`, `createTopic` |
| **Classes** | `PascalCase` | `TopicCreatingService` |
| **Interfaces** | `IPascalCase` | `ITopicCreatingService` |
| **Enums** | `PascalCase` | `ForumIntention`, `EventType` |
| **Constants** | `PascalCase` | `DefaultPageSize` |
| **Async methods** | No `Async` suffix | `Create()` not `CreateAsync()` |

### File Naming

| Type | Pattern | Example |
|------|---------|---------|
| **Service interface** | `I{Name}Service.cs` | `ITopicCreatingService.cs` |
| **Service implementation** | `{Name}Service.cs` | `TopicCreatingService.cs` |
| **Repository interface** | `I{Name}Repository.cs` | `ITopicCreatingRepository.cs` |
| **Repository implementation** | `{Name}Repository.cs` | `TopicCreatingRepository.cs` |
| **Factory** | `{Name}Factory.cs` | `TopicFactory.cs` |
| **Validator** | `{Name}Validator.cs` | `CreateTopicValidator.cs` |
| **Entity (DAL)** | `{Name}.cs` | `ForumTopic.cs` |
| **DTO** | `{Name}.cs` or `Create{Name}.cs` | `Topic.cs`, `CreateTopic.cs` |
| **AutoMapper Profile** | `{Name}Profile.cs` | `TopicProfile.cs` |
| **Autofac Module** | `{Domain}Module.cs` | `ForumModule.cs` |
| **Test class** | `{Class}Should.cs` | `TopicCreatingServiceShould.cs` |
| **Migration** | `YYYYMMDDHHMMSS_{Name}.cs` | `20260115000000_AddSessionsTable.cs` |

### Folder Naming

- Use `PascalCase` for all folders
- Group by feature within `BusinessProcesses/`
- CRUD operations: `Creating/`, `Reading/`, `Updating/`, `Deleting/`

---

## Architecture Patterns

### Hexagonal Architecture (Ports & Adapters)

```
┌─────────────────────────────────────────────────────────────┐
│                     Infrastructure Layer                      │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────────────┐  │
│  │  Web API    │  │  Consumers  │  │  Database Adapters   │  │
│  │ Controllers │  │  RabbitMQ   │  │  EF Core, MongoDB    │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬───────────┘  │
│         │                │                     │              │
│  ┌──────▼─────────────────▼─────────────────────▼───────────┐ │
│  │                  Application Services                     │ │
│  │  (I{Feature}Service → {Feature}Service)                  │ │
│  └──────────────────────────┬────────────────────────────────┘ │
│                             │                                  │
│  ┌──────────────────────────▼────────────────────────────────┐ │
│  │                   Domain Layer                             │ │
│  │  - Entities (BusinessObjects)                              │ │
│  │  - Factories                                               │ │
│  │  - Intentions (Authorization)                              │ │
│  │  - Validators                                              │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

### Service Layer Pattern

```csharp
// Interface - Port
public interface ITopicCreatingService
{
    Task<Topic> CreateTopic(CreateTopic createTopic, CancellationToken ct = default);
}

// Implementation - Adapter
internal class TopicCreatingService : ITopicCreatingService
{
    private readonly IValidator<CreateTopic> _validator;
    private readonly IForumReadingService _forumReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly ITopicFactory _topicFactory;
    private readonly ITopicCreatingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    public TopicCreatingService(
        IValidator<CreateTopic> validator,
        IForumReadingService forumReadingService,
        IIntentionManager intentionManager,
        ITopicFactory topicFactory,
        ITopicCreatingRepository repository,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _forumReadingService = forumReadingService;
        _intentionManager = intentionManager;
        _topicFactory = topicFactory;
        _repository = repository;
        _identityProvider = identityProvider;
    }

    public async Task<Topic> CreateTopic(CreateTopic createTopic, CancellationToken ct = default)
    {
        // 1. Validate input
        await _validator.ValidateAndThrowAsync(createTopic, ct);

        // 2. Load dependencies
        var forum = await _forumReadingService.GetForum(createTopic.ForumTitle);

        // 3. Check authorization
        _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, forum);

        // 4. Create entity via factory
        var topic = _topicFactory.Create(forum.Id, _identityProvider.Current.User.UserId, createTopic);

        // 5. Persist
        return await _repository.Create(topic, ct);
    }
}
```

### Repository Pattern

```csharp
public interface ITopicCreatingRepository
{
    Task<Topic> Create(ForumTopic topic, CancellationToken ct = default);
}

internal class TopicCreatingRepository : ITopicCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public async Task<Topic> Create(ForumTopic topic, CancellationToken ct = default)
    {
        _dbContext.ForumTopics.Add(topic);
        await _dbContext.SaveChangesAsync(ct);
        return _mapper.Map<Topic>(topic);
    }
}
```

### Factory Pattern

```csharp
public interface ITopicFactory
{
    ForumTopic Create(Guid forumId, Guid userId, CreateTopic createTopic);
}

internal class TopicFactory : ITopicFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ForumTopic Create(Guid forumId, Guid userId, CreateTopic createTopic)
    {
        return new ForumTopic
        {
            ForumTopicId = _guidFactory.Create(),
            ForumId = forumId,
            UserId = userId,
            Title = createTopic.Title,
            Text = createTopic.Text,
            CreateDate = _dateTimeProvider.Now
        };
    }
}
```

---

## Dependency Injection

### Autofac Modules

Each domain has its own module:

```csharp
public class ForumModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Services - internal implementation, interface exposed
        builder.RegisterType<TopicCreatingService>()
            .As<ITopicCreatingService>()
            .InstancePerLifetimeScope();

        // Repositories
        builder.RegisterType<TopicCreatingRepository>()
            .As<ITopicCreatingRepository>()
            .InstancePerLifetimeScope();

        // Factories
        builder.RegisterType<TopicFactory>()
            .As<ITopicFactory>()
            .SingleInstance();

        // Validators - auto-registered
        builder.RegisterAssemblyTypes(ThisAssembly)
            .AsClosedTypesOf(typeof(IValidator<>))
            .InstancePerLifetimeScope();
    }
}
```

### Lifetimes

| Lifetime | Use Case |
|----------|----------|
| `SingleInstance()` | Stateless services, factories |
| `InstancePerLifetimeScope()` | Request-scoped (services, repositories) |
| `InstancePerDependency()` | New instance each time (rare) |

### Registration in Startup

```csharp
public void ConfigureContainer(ContainerBuilder builder)
{
    builder.RegisterModule<CoreModule>();
    builder.RegisterModule<DataAccessModule>();
    builder.RegisterModule<AuthenticationModule>();
    builder.RegisterModule<CommonModule>();
    builder.RegisterModule<ForumModule>();
    builder.RegisterModule<CommunityModule>();
    builder.RegisterModule<GamingModule>();
}
```

---

## Database & Data Access

### Entity Framework Core

#### DbContext
```csharp
public class DmDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<ForumTopic> ForumTopics { get; set; }
    public DbSet<Comment> Comments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fluent API configuration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DmDbContext).Assembly);
    }
}
```

#### Entity Design
```csharp
public class ForumTopic
{
    public Guid ForumTopicId { get; set; }      // Primary key
    public Guid ForumId { get; set; }            // Foreign key
    public Guid UserId { get; set; }             // Foreign key
    public string Title { get; set; }
    public string Text { get; set; }
    public DateTimeOffset CreateDate { get; set; }
    public bool IsRemoved { get; set; }

    // Navigation properties
    public virtual Forum Forum { get; set; }
    public virtual User Author { get; set; }
}
```

### MongoDB (for specific use cases)

```csharp
public class MongoRepository<TEntity> where TEntity : class
{
    protected readonly IMongoCollection<TEntity> Collection;

    protected MongoRepository(IMongoClient client, string collectionName)
    {
        var database = client.GetDatabase("dm3");
        Collection = database.GetCollection<TEntity>(collectionName);
    }
}
```

### Migrations

```bash
# Create migration
dotnet ef migrations add YYYYMMDDHHMMSS_DescriptiveName -p src/DM.Services.DataAccess

# Apply migrations
dotnet ef database update -p src/DM.Services.DataAccess
```

---

## API Design

### Controller Structure

```csharp
[Route("v1/boards/{boardId}/topics")]
[ApiController]
public class TopicController : ControllerBase
{
    private readonly ITopicApiService _topicApiService;

    public TopicController(ITopicApiService topicApiService)
    {
        _topicApiService = topicApiService;
    }

    /// <summary>
    /// Get list of topics
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListEnvelope<Topic>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopics(
        string boardId,
        [FromQuery] PagingQuery query,
        CancellationToken ct)
    {
        var result = await _topicApiService.GetTopics(boardId, query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Create new topic
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Envelope<Topic>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTopic(
        string boardId,
        [FromBody] CreateTopic createTopic,
        CancellationToken ct)
    {
        var result = await _topicApiService.CreateTopic(boardId, createTopic, ct);
        return CreatedAtAction(nameof(GetTopic), new { topicId = result.Resource.Id }, result);
    }
}
```

### API Response Envelope

```csharp
public class Envelope<T>
{
    public T Resource { get; set; }
}

public class ListEnvelope<T>
{
    public IEnumerable<T> Resources { get; set; }
    public PagingResult Paging { get; set; }
}

public class BadRequestError
{
    public string Message { get; set; }
    public IDictionary<string, string[]> Errors { get; set; }
}
```

### API Service Layer

```csharp
public interface ITopicApiService
{
    Task<Envelope<Topic>> CreateTopic(string boardId, CreateTopic createTopic, CancellationToken ct);
    Task<ListEnvelope<Topic>> GetTopics(string boardId, PagingQuery query, CancellationToken ct);
}

internal class TopicApiService : ITopicApiService
{
    private readonly ITopicCreatingService _creatingService;
    private readonly IMapper _mapper;

    public async Task<Envelope<Topic>> CreateTopic(string boardId, CreateTopic createTopic, CancellationToken ct)
    {
        createTopic.ForumTitle = boardId;
        var topic = await _creatingService.CreateTopic(createTopic, ct);
        return new Envelope<Topic> { Resource = _mapper.Map<Topic>(topic) };
    }
}
```

---

## Error Handling

### Custom Exceptions

```csharp
public class HttpException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public HttpException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
```

### Error Handling Middleware

```csharp
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationException(context, ex);
        }
        catch (HttpException ex)
        {
            await HandleHttpException(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleInternalError(context);
        }
    }
}
```

---

## Validation

### FluentValidation

```csharp
public class CreateTopicValidator : AbstractValidator<CreateTopic>
{
    public CreateTopicValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(10000);
    }
}
```

### Registration

```csharp
// Auto-register all validators
builder.RegisterAssemblyTypes(ThisAssembly)
    .AsClosedTypesOf(typeof(IValidator<>))
    .InstancePerLifetimeScope();
```

---

## Logging & Observability

### Three Pillars

1. **Logs** (Serilog → OpenSearch)
2. **Metrics** (OpenTelemetry → Prometheus)
3. **Traces** (OpenTelemetry → Jaeger)

### Serilog Configuration

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .Enrich.With<ActivityEnricher>()  // Correlation with traces
    .Enrich.WithProperty("Application", applicationName)
    .WriteTo.Console()
    .WriteTo.OpenSearch(new OpenSearchSinkOptions(uri)
    {
        IndexFormat = "dm_logstash-{0:yyyy.MM.dd}",
        InlineFields = true
    })
    .CreateLogger();
```

### OpenTelemetry Setup

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .ConfigureResource(r => r.AddService(applicationName))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource(DmActivitySource.Name)
        .AddOtlpExporter(options => options.Endpoint = new Uri(tracingEndpoint)))
    .WithMetrics(builder => builder
        .ConfigureResource(r => r.AddService(applicationName))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());
```

### Custom Activity Source

```csharp
public static class DmActivitySource
{
    public const string Name = "DM.Services";
    public static readonly ActivitySource Source = new(Name, "1.0.0");
}

// Usage
using var activity = DmActivitySource.Source.StartActivity("CreateTopic");
activity?.SetTag("forum.title", createTopic.ForumTitle);
```

---

## Testing Standards

### Test Naming

```
Pattern: {Behavior}_When_{Condition}
```

### Test Class Structure

```csharp
public class TopicCreatingServiceShould : UnitTestBase
{
    private readonly TopicCreatingService _service;
    private readonly Mock<ITopicCreatingRepository> _repository;
    private readonly Mock<IIntentionManager> _intentionManager;

    public TopicCreatingServiceShould()
    {
        _repository = Mock<ITopicCreatingRepository>();
        _intentionManager = Mock<IIntentionManager>();

        _service = new TopicCreatingService(
            Mock<IValidator<CreateTopic>>().Object,
            Mock<IForumReadingService>().Object,
            _intentionManager.Object,
            Mock<ITopicFactory>().Object,
            _repository.Object,
            Mock<IIdentityProvider>().Object);
    }

    [Fact]
    public async Task CreateTopic_When_Valid_Input()
    {
        // Arrange
        var createTopic = new CreateTopic { Title = "Test" };
        _repository.Setup(r => r.Create(It.IsAny<ForumTopic>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Topic());

        // Act
        var result = await _service.CreateTopic(createTopic);

        // Assert
        result.Should().NotBeNull();
        _repository.Verify(r => r.Create(It.IsAny<ForumTopic>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Test Base Class

```csharp
public abstract class UnitTestBase
{
    protected Mock<T> Mock<T>() where T : class => new Mock<T>();
}
```

### Important: CancellationToken in Mocks

When mocking methods with optional CancellationToken:

```csharp
// ✅ Correct
repository.Setup(r => r.Get(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(entity);

// ❌ Wrong - will cause CS0854 error
repository.Setup(r => r.Get(It.IsAny<Guid>()))
    .ReturnsAsync(entity);
```

---

## Configuration Management

### Configuration Hierarchy

1. `appsettings.json` (defaults)
2. `appsettings.{Environment}.json` (overrides)
3. Environment variables (production secrets)
4. User secrets (development)

### ConnectionStrings Pattern

```csharp
public class ConnectionStrings
{
    public string Rdb { get; set; }           // PostgreSQL
    public string Mongo { get; set; }          // MongoDB
    public string Search { get; set; }         // OpenSearch
    public string MessageQueue { get; set; }   // RabbitMQ
    public string Logs { get; set; }           // OpenSearch logs
    public string TracingEndpoint { get; set; } // Jaeger
}
```

### Environment Variables

```bash
# Production pattern
ConnectionStrings__Rdb=Host=postgres;Database=dm3;...
ConnectionStrings__Mongo=mongodb://mongo:27017
```

---

## Message Queuing

### RabbitMQ Integration

```csharp
// Producer
public interface IInvokedEventProducer
{
    Task Send(EventType eventType, Guid entityId);
}

// Consumer
public class NotificationConsumer : IConsumer
{
    public async Task HandleAsync(InvokedEventMessage message, CancellationToken ct)
    {
        // Process message
    }
}
```

### Outbox Pattern

```csharp
public class OutboxEvent
{
    public long Id { get; set; }
    public Guid AggregateId { get; set; }
    public int EventType { get; set; }
    public string? Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public bool IsProcessed { get; set; }
}
```

---

## Authentication & Authorization

### Intention-Based Authorization

```csharp
// Define intentions
public enum ForumIntention
{
    CreateTopic,
    EditTopic,
    DeleteTopic,
    AdministrateTopics
}

// Resolver
public class ForumIntentionResolver : IIntentionResolver<ForumIntention, Forum>
{
    public bool IsAllowed(AuthenticatedUser user, ForumIntention intention, Forum target)
    {
        return intention switch
        {
            ForumIntention.CreateTopic => user.IsAuthenticated,
            ForumIntention.EditTopic => IsAuthorOrModerator(user, target),
            ForumIntention.AdministrateTopics => user.Role >= UserRole.Moderator,
            _ => false
        };
    }
}

// Usage
_intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, forum);
```

---

## Package Management

### Central Package Management

All package versions in `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Core -->
    <PackageVersion Include="Autofac" Version="8.0.0" />
    <PackageVersion Include="AutoMapper" Version="13.0.1" />
    <PackageVersion Include="FluentValidation" Version="11.9.2" />

    <!-- Data -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="8.0.8" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.4" />
    <PackageVersion Include="MongoDB.Driver" Version="2.28.0" />

    <!-- Observability -->
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.2" />
    <PackageVersion Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.10.0-beta.1" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />

    <!-- Messaging -->
    <PackageVersion Include="Jamq.Client.Rabbit" Version="0.10.0" />
  </ItemGroup>
</Project>
```

### Build Settings

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>$(AssemblyName).Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

---

## Frontend Standards (Vue.js)

### Project Structure

```
frontend/DM.Web.Modern.Temp/
├── src/
│   ├── api/
│   │   ├── index.ts              # Base API client
│   │   ├── models/               # TypeScript types
│   │   │   ├── index.ts          # Served<T>, Id<T>, Post<T>, Patch<T>
│   │   │   ├── common/
│   │   │   ├── community/
│   │   │   ├── forum/
│   │   │   ├── gaming/
│   │   │   └── messaging/
│   │   └── requests/             # API request modules
│   ├── assets/styles/
│   ├── components/               # Reusable components
│   ├── composables/              # Vue composables
│   ├── router/
│   ├── stores/                   # Pinia stores
│   └── views/                    # Page components
```

### Branded Types Pattern

Use `Served<T>` for server-provided fields:

```typescript
// api/models/index.ts
type InternalServed<T> = T & { __type: T };
export type Served<T> = T extends InternalServed<infer U> ? InternalServed<U> : InternalServed<T>;
export type Id<T> = Served<T>;
export type Post<T> = { [P in keyof T as IfEquals<T[P], Served<T[P]>, never, P>]: T[P] };
export type Patch<T> = { [P in keyof T as IfEquals<T[P], Served<T[P]>, never, P>]?: T[P] };
```

### Model Definition Pattern

```typescript
// CORRECT
export type Message = {
  id: Served<MessageId>;           // Server-provided (read-only)
  createdUtc: Served<string>;      // Server-provided
  author: Served<User>;            // Server-provided
  text: string;                    // User-editable
  likes: Served<User[]>;           // Server-provided
};
```

### API Request Module Pattern

```typescript
export default new (class MessagingApi {
  public getConversations(q: PagingQuery) {
    return Api.get<ListEnvelope<Conversation>>("conversations", q);
  }

  public updateMessage(id: MessageId, message: Patch<Message>) {
    return Api.patch<Envelope<Message>>(`messages/${id}`, message);
  }
})();
```

---

## CI/CD Standards

### Pipeline Structure

```yaml
stages:
  - build
  - test
  - publish
  - deploy

build:
  stage: build
  script:
    - dotnet build -c Release

test:
  stage: test
  script:
    - dotnet test --no-build
  artifacts:
    reports:
      junit: TestResults/*.xml

publish:
  stage: publish
  script:
    - docker build -t $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA .
    - docker push $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA
```

### Migration Strategy

- Apply migrations **BEFORE** code deployment
- Test migration, verify, then deploy
- Never modify applied migrations
- One logical change per migration

---

## Infrastructure (Docker)

### Docker Compose Service Naming

All services use `dm-` prefix:

```yaml
services:
  dm-db:         # PostgreSQL
  dm-rabbit:     # RabbitMQ
  dm-search:     # OpenSearch
  dm-api:        # .NET API
  dm-web:        # Frontend
  dm-prometheus: # Metrics
  dm-grafana:    # Dashboards
  dm-loki:       # Log aggregation
  dm-jaeger:     # Distributed tracing
```

### Multi-Stage Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Project.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Project.dll"]
```

---

## API DTO Reference

### Enumerations

#### UserRole (Flags)
```csharp
[Flags]
public enum UserRole
{
    Guest = 0,
    Newbie = 1,
    RegularUser = 1 << 1,      // Registered user
    ExModerator = 1 << 2,      // Former moderator
    MentorModerator = 1 << 3,  // Nanny moderator for newbies
    RegularModerator = 1 << 4, // Forum moderator
    SeniorModerator = 1 << 5,  // Senior moderator
    Administrator = 1 << 6     // Full admin
}
```

#### Gender
```csharp
public enum Gender
{
    Unknown = 0,
    Male = 1,
    Female = 2
}
```

#### ColorSchema
```csharp
public enum ColorSchema
{
    Light = 0,  // Default light theme
    Dark = 1    // Dark theme
}
```

#### PollType
```csharp
public enum PollType
{
    Global = 0,  // Site-wide poll
    Game = 1,    // Game-specific poll
    Topic = 2    // Forum topic poll
}
```

### User DTOs

#### User (Base)
```csharp
public class User
{
    public Guid Id { get; set; }
    public string Login { get; set; }
    public IEnumerable<UserRole> Roles { get; set; }
    public string MediumPictureUrl { get; set; }
    public string SmallPictureUrl { get; set; }
    public Rating Rating { get; set; }
    public DateTimeOffset? OnlineUtc { get; set; }
    public DateTimeOffset? RegistrationDateUtc { get; set; }
}

public class Rating
{
    public bool IsEnabled { get; set; }
    public int TotalRating { get; set; }
    public int TotalPosts { get; set; }
}
```

#### UserDetails (Extended)
```csharp
public class UserDetails : User
{
    public Guid? PictureGuid { get; set; }
    public string OriginalPictureUrl { get; set; }
    public string Status { get; set; }
    public string GivenPostReview { get; set; }
    public string Name { get; set; }
    public Gender Gender { get; set; }
    public DateTimeOffset? BirthdayDateUtc { get; set; }
    public string Location { get; set; }
    public IEnumerable<UserContact> Contacts { get; set; }
    public InfoBbText Info { get; set; }
    public UserSettings Settings { get; set; }
}

public class UserContact
{
    public string Title { get; set; }  // e.g., "Telegram", "Discord"
    public string Value { get; set; }
}
```

#### UserSettings
```csharp
public class UserSettings
{
    public ColorSchema ColorSchema { get; set; }
    public string NannyGreetingsMessage { get; set; }
    public PagingLimits PagingLimits { get; set; }
}

public class PagingLimits
{
    public int PostsPerPage { get; set; }
    public int CommentsPerPage { get; set; }
    public int TopicsPerPage { get; set; }
    public int MessagesPerPage { get; set; }
    public int EntitiesPerPage { get; set; }
}
```

### Poll DTO
```csharp
public class Poll
{
    public Guid Id { get; set; }
    public PollType PollType { get; set; }
    public DateTimeOffset EndsUtc { get; set; }
    public string Title { get; set; }
    public IEnumerable<PollOption> Options { get; set; }
}
```

### Field Naming Conventions

| Old Name | New Name | Reason |
|----------|----------|--------|
| `Online` | `OnlineUtc` | UTC suffix for date fields |
| `Registration` | `RegistrationDateUtc` | UTC suffix |
| `Ends` | `EndsUtc` | UTC suffix |
| `Enabled` | `IsEnabled` | Boolean prefix convention |
| `Quality` | `TotalRating` | More descriptive |
| `Quantity` | `TotalPosts` | More descriptive |
| `Paging` | `PagingLimits` | Clarifies purpose |
| `Player` | `RegularUser` | Clearer meaning |
| `NannyModerator` | `MentorModerator` | Clearer meaning |

---

## Quick Reference Checklist

### Before Creating a New Service:
- [ ] Create CRUD folder structure (Creating/Reading/Updating/Deleting)
- [ ] Define intention enum and resolver
- [ ] Create FluentValidation validators
- [ ] Create AutoMapper profile
- [ ] Register in Autofac module
- [ ] Add event publishing

### Before Creating a New Endpoint:
- [ ] Add XML documentation
- [ ] Add route name (`Name = nameof(...)`)
- [ ] Add `[AuthenticationRequired]` if needed
- [ ] Add ALL `[ProducesResponseType]` attributes
- [ ] Create corresponding API service method

### Before Creating Frontend Types:
- [ ] Use `Id<string>` for ID types
- [ ] Use `Served<T>` for all server-provided fields
- [ ] Keep user-editable fields as plain types
- [ ] Create API request methods using `Post<T>` and `Patch<T>`

### Before Committing:
- [ ] `dotnet build` succeeds with no errors/warnings
- [ ] `npm run type-check` passes
- [ ] All new services publish events
- [ ] All new endpoints have ProducesResponseType
- [ ] No secrets in committed files

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-01-15 | Initial standards document |
| 1.1 | 2026-01-15 | Added Frontend, CI/CD, Infrastructure, Checklist |
| 1.2 | 2026-01-15 | Added API DTO Reference section with updated enums and DTOs |
| 1.3 | 2026-01-17 | Fixed TypeScript Served<T> type issues in stores, added Paging.hasMoreBefore/hasMoreAfter |

---

*This document should be updated when standards change.*
