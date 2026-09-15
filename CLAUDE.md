# APP1S8 — SONDAGEAPI

School project for a **Secure Programming** course (APP1, S8). Team of 2:
**Max** (Maxime Lescadre, git author `ParciIllusion`) and **F-O** (git author `Fox003`).

The deliverable is a **secure .NET 10 survey API** (*sondage*) plus supporting analysis, covering
**11 livrables** (numbered "Requis 1" … "Requis 11") from the *Guide de l'étudiant*.

The survey domain matters for grading: livrable 4 asks for authentication guaranteeing the
**uniqueness of a participant's participation** — one response per person per survey. That
constraint should drive the data model.

## Working agreement — read this first

**Max and F-O write the solution code themselves.** Claude's role here is *coach and helper*, not
implementer. That means:

- **Do** explain concepts, review code they wrote, point out security flaws, suggest approaches,
  explain error messages, look up package/API docs, and answer "why does X work this way".
- **Do** help with scaffolding that is not the learning objective (build config, tooling, CI, docs)
  when asked.
- **Don't** write feature code, controllers, auth logic, or tests unprompted. If a question could be
  answered *or* solved by writing the code, answer it and let them write it.
- When they are stuck, prefer a hint or a worked *analogous* example over the actual solution.
- Ask before producing more than a few lines of production code.

## The 11 livrables

From the *Guide de l'étudiant* (APP1 – S8, Module Sécurité informatique, p. 7). The numbering
matches the "Requis N" labels the team uses.

**This table is the single reference for what the project must deliver.** Track dependency and
tooling decisions in the Status column here; there is no separate dependency document.

| # | Livrable | Kind | Status |
| --- | --- | --- | --- |
| 1 | API secured by **HTTPS**, authorization via an **access key** | code | **Done (2026-09-15)** — `UseHttpsRedirection()` + `Security/ApiKeyMiddleware` checking `X-API-Key`. See `.claude/Max/2026-09-15-livrable1-api-key.md` |
| 2 | **OpenAPI** documentation integrated via **Swagger** | code | Done — single document from `AddOpenApi()` at `/openapi/v1.json`, **pinned to OpenAPI 3.0**; Swashbuckle kept for the UI shell only. Dev-only |
| 3 | **Postman collection** demonstrating the API, linked to the OpenAPI schema | artifact | Not started. Import `/openapi/v1.json` (3.0 pin is there partly for Postman); `SONDAGEAPI.http` covers both endpoints meanwhile |
| 4 | **Participant authentication** guaranteeing uniqueness of participation | code | Not started; `Microsoft.AspNetCore.Authentication.JwtBearer` was dropped in the rebuild. This one belongs in the real authentication stack, **not** in the livrable-1 middleware |
| 5 | **xUnit test battery, in the same solution as the API**, proving full coverage *and* the mitigations from the attack-surface analysis | code | **Blocked** — see known issues 1–4 |
| 6 | **Security impact analysis**, including attack vectors | document | Not started. Feeds the mitigations #5 must test. Material to reuse: section 2 and 3 of `.claude/Max/2026-09-15-livrable1-api-key.md` |
| 7 | **In-code security mechanisms** — stack execution prevention, VS hardening options | build config | Not started. NX/DEP, ASLR, CFG — csproj/linker properties, not library code |
| 8 | **Code protection by obfuscation** + the configuration used | code + doc | `Obfuscar.GlobalTool` 2.2.50 installed globally (`obfuscar.console`); no `obfuscar.xml` yet |
| 9 | **Operational recommendations** (system architecture) | document | Not started |
| 10 | **CycloneDX SBOM** report | artifact | `dotnet-CycloneDX` **not installed** |
| 11 | **Security bug identification and publication process** | document | Not started; conventionally a `SECURITY.md` |

Roughly half the grade is documents (6, 9, 11, plus the written half of 8), so budget time for them
rather than splitting the work purely by endpoint.

Two ordering dependencies: **#6 → #5** (the attack-vector analysis defines the mitigations the tests
must prove, so writing tests first forces rework) and **#2 → #3** (generate the Postman collection
by importing the OpenAPI schema rather than hand-building requests).

Livrables 1 and 4 are **two different auth mechanisms** — an access key authorizing calls to the
API, and participant authentication proving uniqueness. Don't collapse them into one scheme.

## Repo layout

```
SONDAGEAPI/          the API — net10.0, Microsoft.NET.Sdk.Web
  Program.cs         minimal API, all endpoints inline
  Security/          API-key middleware, options, exemption marker, DI extensions
  OpenApi/           document transformer (securitySchemes) + DI extensions
  Data/              ApplicationDbContext (EF Core)
  Models/            Product.cs  (template cruft — not the survey domain)
  Migrations/        InitialCreate — Products table only
  app.db             SQLite database (tracked in git — see known issues)
  global.json        SDK pin: 10.0.0, rollForward latestMajor
Tests/               net8.0, xunit.v3.mtp-v2 on Microsoft.Testing.Platform
global.json          repo root — test runner config only, no SDK pin
```

There is **no `.sln`**.

## Commands

```bash
dotnet build SONDAGEAPI/SONDAGEAPI.csproj    # stop the running app first, or the exe copy fails
dotnet run   --project SONDAGEAPI            # https://localhost:7016  |  http://localhost:5263
dotnet test  Tests/Tests.csproj              # currently fails — see known issue 1
dotnet ef migrations add <Name> --project SONDAGEAPI

dotnet user-secrets set "Sondage:ApiKey" "<key>" --project SONDAGEAPI   # required, or startup fails
dotnet user-secrets list --project SONDAGEAPI
```

**The API will not start without `Sondage:ApiKey`.** `ValidateOnStart()` rejects a missing or
under-32-character key. The secret store is per-developer, so each of Max and F-O sets their own;
outside Development the value comes from the `Sondage__ApiKey` environment variable (double
underscore). Never commit the value.

Swagger UI is at `/swagger`, registered **only when `ASPNETCORE_ENVIRONMENT=Development`**.

Existing endpoints: `GET /ping` (exempt from the key), `GET /api/products/{id:int}` (key required).
A browser address bar cannot send `X-API-Key` — test through Swagger, `SONDAGEAPI.http`, Postman or
curl.

## Current state (2026-09-15)

F-O rebuilt the project from scratch on 2026-09-12 (commit `02cbff6`, *"Nuke but remake lol"*),
replacing the original console app with `SONDAGEAPI`. Since then he has added Swagger, a ping route,
a test project, and EF Core + SQLite with a `Product` entity.

On 2026-09-15 Max finished **livrable 1**: the API-key middleware, its OpenAPI security scheme, and
the supporting DI extensions. Verified against a running instance — no key and wrong key both give
an identical 401, a valid key gives 200, `/ping` stays open. The write-up, including the reasoning
to reuse in livrables 6 and 9, is in `.claude/Max/2026-09-15-livrable1-api-key.md`.

The API compiles clean and runs. The survey domain still does not exist — `Product { Id, Name,
Price }` is scaffolding from the EF tutorial. It is currently load-bearing: `/api/products/{id:int}`
is the only key-protected endpoint, so it is what demonstrates the middleware until the survey
entities replace it.

## Known issues

Ordered by how much they block. 1–4 all stand between the team and livrable 5.

1. **The test suite cannot execute.** `Tests.csproj` targets `net8.0`, but only the .NET 10 SDK is
   installed, so `dotnet test` exits with *"Zéro tests exécutés"* and asks for the
   `Microsoft.NETCore.App 8.0.0` runtime. The project *builds* fine, which makes this easy to miss.
   Retarget to `net10.0`.
2. **`Tests` has no `ProjectReference` to `SONDAGEAPI`** — the test project still cannot see the
   code under test, so no real test can be written.
3. **No solution file.** Livrable 5 explicitly requires the tests to be *"inclus dans la même
   solution que le projet d'API"*, so this is a graded gap, not a preference.
4. **The rebuild dropped the coverage and mocking stack.** `coverlet.collector` and `Moq` are gone,
   and the test stack moved from xUnit 2 + `Microsoft.NET.Test.Sdk` to `xunit.v3.mtp-v2` on
   Microsoft.Testing.Platform. MTP does not take `--collect:"XPlat Code Coverage"` the way VSTest
   did, so the coverage proof livrable 5 demands needs a deliberate choice —
   `Microsoft.Testing.Extensions.CodeCoverage`, or revert to the VSTest runner.
5. **The SQLite database is still tracked.** `.gitignore` now carries `*.db`, `*.db-shm`, and
   `*.db-wal` rules, but ignore rules do not apply to files already in the index, so
   `SONDAGEAPI/app.db`, `app.db-shm`, and `app.db-wal` remain tracked. A binary DB written by both
   developers conflicts constantly, and `-shm`/`-wal` are transient SQLite internals. The migration
   is the source of truth. Needs `git rm --cached SONDAGEAPI/app.db SONDAGEAPI/app.db-shm
   SONDAGEAPI/app.db-wal` — but F-O pushed the DB deliberately (commit `75bce66`), so agree with him
   first and make sure any seed data he wants lives in a migration or a seeding routine.
6. **Packages required by later livrables are missing:**
   `Microsoft.AspNetCore.Authentication.JwtBearer` (livrable 4) and the `CycloneDX` global tool
   (livrable 10). Livrable 1 needed neither — the access key is plain middleware.
7. **Template cruft — partly cleared.** `/weatherforecast`, the `summaries` array and the
   `WeatherForecast` record are gone (2026-09-15). `Models/Product.cs`, `DbSet<Product>` and the
   `InitialCreate` migration remain EF tutorial leftovers, kept only because they carry the one
   endpoint that proves the API-key middleware. Drop them **together with** the survey entities, in
   a single migration, rather than piecemeal.
8. ~~**Two OpenAPI stacks registered at once.**~~ Resolved 2026-09-15: `AddSwaggerGen()` and
   `AddEndpointsApiExplorer()` were removed. `AddOpenApi()` produces the one document, and
   `UseSwaggerUI()` is kept purely as a UI shell pointed at `/openapi/v1.json`.
9. **The API key is a single static shared secret with no rotation or expiry.** Adequate for the
   livrable, but it is the weak point of the scheme — name it in #6 and #9 rather than letting a
   corrector find it. The current dev key also leaked into an assistant session and should be
   rotated.

## Conventions

- Course documents and graded deliverables are written in **French**; commit messages are mixed
  French/English. Code and identifiers are English, except user-facing API strings
  (`"Aucun produit avec l'id {id}"`), which are French.
- Since this is a *secure* programming course: when reviewing their code, call out injection,
  authn/authz gaps, secret handling, input validation, and error-message leakage explicitly — that
  critique is the point of the course, not a nitpick.
