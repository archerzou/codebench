---
name: add-entity
description: Add a new domain entity to the Vertical Slice Architecture template — entity class, error catalog, domain events, EF Core configuration, DbContext wiring, and migration. Use when the user asks to add an entity, aggregate, domain model, or table.
argument-hint: <entity description, e.g. "Project with a name, owner, and list of todos">
---

# Add a Domain Entity

Create a new entity and wire it through the single `Web.Api` project, following the `TodoItem` pattern. The entity and its domain types live with their feature; only the EF configuration and the `DbSet` live in `Database/`.

## Files to create/modify

1. **Entity** — `src/Web.Api/Features/{Entity}/{Entity}.cs`

```csharp
using Web.Api.Common;

namespace Web.Api.Features.Projects;

public sealed class Project : Entity
{
    private Project()                      // for EF
    {
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ProjectStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Project Create(Guid ownerId, string name, DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var project = new Project { Id = Guid.CreateVersion7(), OwnerId = ownerId, Name = name, CreatedAt = createdAt };
        project.Raise(new ProjectCreatedDomainEvent(project.Id));
        return project;
    }

    public Result Archive()                // guarded + idempotent transition (DM-02)
    {
        if (Status == ProjectStatus.Archived) return Result.Success();
        Status = ProjectStatus.Archived;
        return Result.Success();
    }
}
```

`sealed class`, inherits `Entity` (from `Web.Api.Common` — gives it `DomainEvents` + `Raise(...)`), private parameterless constructor for EF, `private set` everywhere, a static factory that raises the `Created` event, and state changes as methods returning `Result` (DM-01, DM-02). Time is passed in (from `IDateTimeProvider`, DM-04). More than ~7 inputs → take a `{Entity}Content` record (see `ProblemContent`). Invariant violations (blank required strings) throw `ArgumentException` — they are bugs; expected business failures return `Result.Failure`. Enums: one per file (NM-01), stored as text (`HasConversion<string>()`). Collections: `IReadOnlyList<T> … = [];` and copy on assignment (`[.. source]`). Real examples: `Features/Problems/Problem.cs`, `TestCase.cs`, `Features/Users/User.cs`.

2. **Error catalog** — `src/Web.Api/Features/{Entity}/{Entity}Errors.cs`

```csharp
using Web.Api.Common;

namespace Web.Api.Features.Projects;

public static class ProjectErrors
{
    public static Error NotFound(Guid projectId) => Error.NotFound(
        "Projects.NotFound",
        $"The project with the Id = '{projectId}' was not found");
}
```

Codes are `"{FeaturePlural}.{Reason}"`. Pick the factory by semantics: `Error.NotFound` (404), `Error.Conflict` (409), `Error.Problem` (400), `Error.Failure` (500). `Error`/`ErrorType` are in `Web.Api.Common`.

3. **Domain events** — one record per file, `src/Web.Api/Features/{Entity}/{Entity}{PastTenseVerb}DomainEvent.cs`

```csharp
using Web.Api.Common;

namespace Web.Api.Features.Projects;

public sealed record ProjectCreatedDomainEvent(Guid ProjectId) : IDomainEvent;
```

Create at minimum the `Created` event; add others as commands need them. Events carry ids, not entities. `IDomainEvent` is in `Web.Api.Common`.

4. **EF configuration** — `src/Web.Api/Database/Configurations/{Entity}Configuration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Api.Features.Projects;
using Web.Api.Features.Users;

namespace Web.Api.Database.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId);
    }
}
```

Relationships are configured shadow-style (`HasOne<User>().WithMany()`) — entities hold foreign-key ids, not navigation properties. Configurations are picked up automatically by `ApplyConfigurationsFromAssembly` in `ApplicationDbContext`. Referencing another feature's entity here (e.g. `User`) is fine — it's a domain-type reference, not a cross-slice handler dependency.

5. **DbContext wiring** — add `DbSet<{Entity}> {Plural}` to `src/Web.Api/Database/ApplicationDbContext.cs`. This is the **only** place a `DbSet` is declared — there is no `IApplicationDbContext` in this template. Handlers and unit tests use the concrete `ApplicationDbContext`, so the new set is available everywhere automatically.

6. **Migration** — from the repo root (single project, so it's both `--project` and `--startup-project`):

```
dotnet ef migrations add Phase{N}_{Words} --project src/Web.Api --startup-project src/Web.Api --output-dir Database/Migrations
```

Migration names are `Phase{N}[A]_{Words_With_Underscores}` (NM-09, checked by `check-rules.ps1`), e.g. `Phase1_Problems_Users`. Review `Up()` before applying, then add an integration test for anything EF InMemory can't see (jsonb, arrays, check constraints, `xmin`, cascades) — see `tests/IntegrationTests/Database/EntityPersistenceTests.cs`.

## Rules

- The entity is a plain domain type in its feature folder — no EF attributes, no persistence concerns on it. Keys, conversions, and relationships live exclusively in the `Database/Configurations/` configuration.
- Entity, errors, and domain events go in `Features/{Entity}/` (namespace `Web.Api.Features.{Entity}`); the EF configuration goes in `Database/Configurations/`; the `DbSet` on `ApplicationDbContext`.
- Domain types are shareable across features (a slice in another feature may reference this entity), but never reference another feature's `Command`/`Handler`.
- Run `dotnet build` and `dotnet test` when done.
- If the user also wants use cases for the entity, continue with the `add-feature` skill.
