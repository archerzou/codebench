---
name: add-tests
description: Backfill missing tests for existing use cases in the Vertical Slice Architecture template — handler unit tests, FluentValidation validator tests, and HTTP integration tests. Use when the user asks to add, improve, or backfill test coverage.
argument-hint: <use case or feature to cover, e.g. "CreateTodo" or "the Users feature">
---

# Add Tests for an Existing Use Case

Backfill the three test types this template expects for every slice. Read the target slice file (`Features/{Entity}/{UseCase}.cs`) first — its nested `Command`/`Query`, `Handler`, `Validator`, and `Endpoint` — then mirror the structure of the closest existing test class.

## Workflow

1. **Locate the slice.** Open `src/Web.Api/Features/{Entity}/{UseCase}.cs`. List every distinct outcome from the nested `Handler`: each guard clause (`return Result.Failure(...)`) and the happy path.
2. **Check what already exists** in `tests/Web.Api.UnitTests/{Feature}/` and `tests/IntegrationTests/{Feature}/` — extend existing classes, don't duplicate.
3. **Write handler unit tests** — one test per failure path plus one happy path asserting persisted state and raised domain events.
4. **Write validator tests** (commands only) — one failing test per rule plus one fully-valid command.
5. **Write integration tests** — happy path over real HTTP with state asserted via a follow-up GET; error translation (404/409/400) where the handler has failure paths; 401 without a token if the route family is new.
6. **Run** `dotnet test` (Docker must be running for the integration tests) and fix failures before finishing.

## Conventions

- **Frameworks:** xUnit + Shouldly + NSubstitute; `FluentValidation.TestHelper` for validators. Global usings already cover `Xunit`, `NSubstitute`, `Shouldly`. Add `using Web.Api.Common;` for `Result`/`Error` and `using Web.Api.Features.{Entity};` for the slice, entity, errors, and domain events.
- **Reference slice members qualified:** `CreateTodo.Command`, `new CreateTodo.Handler(...)`, `new CreateTodo.Validator()` — they are nested in the `public static class {UseCase}`.
- **Unit test base:** inherit `BaseHandlerTest`; use `CreateDbContext()` for a fresh in-memory `ApplicationDbContext` (from `Web.Api.Database`) and `CreateCache()` for a real `HybridCache`. There is no `IApplicationDbContext`/`TestDbContext` — the handler takes the concrete `ApplicationDbContext`, so use it directly. Substitute only interfaces (`IUserContext`, `IDateTimeProvider`) — never mock the DbContext. For `IUserContext.TryGetUserId(out …)` use `Returns(call => { call[0] = id; return true; })`.
- **Integration test base:** inherit `BaseIntegrationTest(factory)` with the `IntegrationTestWebAppFactory` collection fixture; use `SignInAsNewUserAsync()` (local user + Clerk-shaped test token) or `CreateUserAsync()` + `Authenticate(TestJwt.Create(sub))` for authenticated calls; `TestJwt.Create(sub, authorizedParty:, issuer:, expiresAt:, signingKey:)` covers token edge cases. Integration tests go through HTTP; deserialize into the slice's public `Response` or a private DTO record.
- **Naming (NM-10, NM-11 in `context/code_rules.md`):** classes `{UseCase}HandlerTests` / `{UseCase}ValidatorTests` (unit), `{UseCase}Tests` (integration), `{TypeName}Tests` for non-slice types; methods `{Subject}_Should_{Outcome}[_When_{Condition}]` — `Handle_Should_…` for handlers, the slice name (`GetMe_Should_…`) for integration tests.
- **Structure:** `// Arrange` / `// Act` / `// Assert` comments in every test.
- **Assertions:** compare exact errors (`result.Error.ShouldBe(TodoItemErrors.NotFound(id))`); assert persisted state by re-reading from the context (unit) or via a GET request (integration); assert domain events with `entity.DomainEvents.ShouldContain(e => e is XDomainEvent)`.

Full annotated templates: [../add-feature/references/tests.md](../add-feature/references/tests.md) (if the `add-feature` skill is installed) or mirror `GetMeHandlerTests` (unit), `AuthorizedPartyValidatorTests` (non-slice unit) and `GetMeTests` / `ClerkTokenValidationTests` (integration) in this repo.
