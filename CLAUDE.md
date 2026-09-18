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
| 4 | **Participant authentication** guaranteeing uniqueness of participation | code | **Done (2026-09-15, branch `max`)** — opaque one-time invitation token, `AuthenticationHandler` + conditional `UPDATE`. See `.claude/Max/2026-09-15-livrable4-jeton-participation.md`. F-O has a parallel **JWT** implementation on `fo`; the two get compared against the grading grid and one is kept. No JwtBearer package needed for the token approach |
| 5 | **xUnit test battery, in the same solution as the API**, proving full coverage *and* the mitigations from the attack-surface analysis | code | **Done (2026-09-17, branch `max`)** — 117 tests, **100 % line and 100 % branch** per ReportGenerator. TOCTOU race test included (10 concurrent submissions → one 201, nine 409). Run the Rider **Couverture** configurations in `.run/`. See `.claude/Max/2026-09-17-livrable5-tests-couverture.md` |
| 6 | **Security impact analysis**, including attack vectors | document | Not started. Feeds the mitigations #5 must test. Material to reuse: section 2 and 3 of `.claude/Max/2026-09-15-livrable1-api-key.md` |
| 7 | **In-code security mechanisms** — stack execution prevention, VS hardening options | build config | Not started. NX/DEP, ASLR, CFG — csproj/linker properties, not library code |
| 8 | **Code protection by obfuscation** + the configuration used | code + doc | `Obfuscar.GlobalTool` 2.2.50 installed globally (`obfuscar.console`); no `obfuscar.xml` yet |
| 9 | **Operational recommendations** (system architecture) | document | Not started |
| 10 | **CycloneDX SBOM** report | artifact | `cyclonedx` **6.2.0 installed** (2026-09-18) as a local tool in the root `.config/dotnet-tools.json`, alongside ReportGenerator. Not yet run against `SONDAGEAPI.slnx`; report itself not generated |
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
    Participants/    livrable 4 — token gen/hash, AuthenticationHandler, claims, DI extensions
  OpenApi/           two document transformers (API key + participant token) & DI extensions
  Contracts/         request/response records
  Data/              ApplicationDbContext (EF Core) — DbSets + OnModelCreating indexes
  Models/            Survey, Invitation, SurveyResponse
  Migrations/        InitialCreate, SurveyDomain (drops Products, adds the survey schema)
  app.db             SQLite database (tracked in git — see known issues)
Tests/               net10.0, xUnit 2 + VSTest + coverlet.msbuild; ProjectReference -> SONDAGEAPI
  Infrastructure/    SondageApiFactory (WebApplicationFactory<Program>), factory variants,
                     TestTimeProvider, ApiScenario helpers
  *Tests.cs          one class per surface: API key, sondages, invitations, participation,
                     uniqueness/TOCTOU, token lifetime, forged claims, OpenAPI, HTTPS, production
SONDAGEAPI.slnx      repo root — solution, both projects
global.json          repo root — SDK pin: 10.0.0, rollForward latestMajor (covers both projects)
.run/                repo root — shared Rider run configurations, folder "Couverture":
                     1 Outils locaux / 2 Tests / 3 Rapport / 4 Ouvrir le rapport, chained
                     by before-launch tasks. Tracked in git; `.idea/` is not.
                     type="RunNativeExe" (Rider persists a type by its runConfigId,
                     not by its class name), and EXE_PATH is the absolute
                     C:\Program Files\dotnet\dotnet.exe — adjust if .NET is elsewhere
.config/             dotnet-tools.json — ReportGenerator as a local tool
```

The solution is **`.slnx`**, the XML format .NET 10's `dotnet new sln` now emits by default (the
teacher asked for it). It needs VS 17.14+ / Rider 2025.1+. Three readable lines instead of the
classic `.sln` GUID soup, so it stops being a merge-conflict source.

## Commands

```bash
dotnet build SONDAGEAPI.slnx                 # stop the running app first, or the exe copy fails
dotnet run   --project SONDAGEAPI            # https://localhost:7016  |  http://localhost:5263
dotnet test  SONDAGEAPI.slnx                 # 117/117 passing, no coverage
dotnet ef migrations add <Name> --project SONDAGEAPI

# What the Rider "Couverture" configurations run, in order. Step 2 fails under 100 %
# line or branch; step 3 needs step 1 because ReportGenerator is a local tool.
dotnet tool restore
dotnet test Tests/Tests.csproj -p:CollectCoverage=true
dotnet reportgenerator "-reports:TestResults/coverage.cobertura.xml" "-targetdir:TestResults/rapport" "-reporttypes:Html;TextSummary;Badges"

dotnet user-secrets set "Sondage:ApiKey" "<key>" --project SONDAGEAPI   # required, or startup fails
dotnet user-secrets list --project SONDAGEAPI
```

**The API will not start without `Sondage:ApiKey`.** `ValidateOnStart()` rejects a missing or
under-32-character key. The secret store is per-developer, so each of Max and F-O sets their own;
outside Development the value comes from the `Sondage__ApiKey` environment variable (double
underscore). Never commit the value.

Swagger UI is at `/swagger`, registered **only when `ASPNETCORE_ENVIRONMENT=Development`**.

Endpoints. `GET /ping` is exempt from the key. Everything under `/api/` needs `X-API-Key`; the two
participation endpoints need `X-Participant-Token` **as well**.

| Endpoint | Auth |
| --- | --- |
| `GET /ping` | none |
| `POST /api/sondages` | API key |
| `GET /api/sondages/{id:guid}` | API key |
| `POST /api/sondages/{id:guid}/invitations` | API key — **returns the plaintext token once** |
| `POST /api/sondages/{id:guid}/reponses` | API key + participant token |
| `GET /api/sondages/{id:guid}/participation` | API key + participant token |

A browser address bar cannot send headers — test through Swagger, `SONDAGEAPI.http`, Postman or
curl.

## Current state (2026-09-17)

F-O rebuilt the project from scratch on 2026-09-12 (commit `02cbff6`, *"Nuke but remake lol"*),
replacing the original console app with `SONDAGEAPI`. Since then he has added Swagger, a ping route,
a test project, and EF Core + SQLite with a `Product` entity.

On 2026-09-15 Max finished **livrable 1**: the API-key middleware, its OpenAPI security scheme, and
the supporting DI extensions. Verified against a running instance — no key and wrong key both give
an identical 401, a valid key gives 200, `/ping` stays open. The write-up, including the reasoning
to reuse in livrables 6 and 9, is in `.claude/Max/2026-09-15-livrable1-api-key.md`.

Also on 2026-09-15 the test plumbing was repaired: `Tests` retargeted to `net10.0`, given a
`ProjectReference` to `SONDAGEAPI`, and both projects put in a new root `SONDAGEAPI.slnx`. The suite
now actually executes (1/1), and `Microsoft.AspNetCore.App` flows transitively into
`Tests.runtimeconfig.json`, so no hand-written `FrameworkReference` is needed.

Still on 2026-09-15, Max built **livrable 4** on branch `max` using the opaque-token approach:
`Survey` / `Invitation` / `SurveyResponse`, the `SurveyDomain` migration (which also drops
`Products`), a `ParticipantTokenAuthenticationHandler`, and the participation endpoints. Verified
against a running instance, including a 10-way concurrent submission that produced exactly one 201
and nine 409s, with a single row in `Responses`.

The survey domain now exists and `Product` is gone. F-O is implementing livrable 4 as JWT on branch
`fo`; both stay until the team picks one against the grading grid.

On 2026-09-17 **livrable 5** was built on branch `max`: 117 xUnit tests reaching 100 % line and
100 % branch coverage, measured by coverlet and reported by ReportGenerator. Most tests run the real
pipeline through `WebApplicationFactory<Program>` over a per-class temporary SQLite file, with only
the clock and the connection string substituted — so the API key middleware, the participant
authentication handler and the conditional `UPDATE` are all genuinely exercised rather than mocked.
Getting there required reverting the test project to the VSTest runner (known issue 4). Two findings
came out of it, both material for livrable 6: in Development a malformed JSON body returns a full
stack trace via the developer exception page, and `UseHttpsRedirection` protects the *next* request,
not the one that already carried the key in clear. Details in
`.claude/Max/2026-09-17-livrable5-tests-couverture.md`.

## Known issues

Ordered by how much they block. All four that stood between the team and livrable 5 are now
resolved; what remains is either shared-repo hygiene or material for the written livrables.

1. ~~**The test suite cannot execute.**~~ Resolved 2026-09-15: `Tests.csproj` retargeted from
   `net8.0` to `net10.0`. It used to *build* fine while `dotnet test` reported *"Zéro tests
   exécutés"* and asked for the `Microsoft.NETCore.App 8.0.0` runtime, which made it easy to miss.
2. ~~**`Tests` has no `ProjectReference` to `SONDAGEAPI`.**~~ Resolved 2026-09-15.
3. ~~**No solution file.**~~ Resolved 2026-09-15: `SONDAGEAPI.slnx` at the repo root holds both
   projects, satisfying livrable 5's *"inclus dans la même solution que le projet d'API"*.
4. ~~**The rebuild dropped the coverage and mocking stack.**~~ Resolved 2026-09-17 by reverting to
   the VSTest runner. xUnit **v3 4.x is MTP-only** (every `xunit.v3*` package drags in
   `Microsoft.Testing.Platform.MSBuild`, which hard-errors on VSTest under the .NET 10 SDK), and
   coverlet has no MTP collector — so `xunit.v3.mtp-v2` and coverlet are mutually exclusive. Since
   the livrable names coverlet explicitly, `Tests` is now xUnit **2.9.3** +
   `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` + `coverlet.msbuild`, and the root
   `global.json` no longer sets `test.runner`. The **msbuild** integration replaced
   `coverlet.collector` on 2026-09-17: the collector silently ignores `<Threshold>` in a
   `.runsettings` file, so only `coverlet.msbuild` can collect *and* fail the run under
   100 %. Moq was never re-added and is not needed: `WebApplicationFactory` + a
   hand-written `TestTimeProvider` cover the substitution needs.
5. **The SQLite database is still tracked** — now more pressing, since `app.db` carries the survey
   schema and both developers are writing migrations. `.gitignore` now carries `*.db`, `*.db-shm`, and
   `*.db-wal` rules, but ignore rules do not apply to files already in the index, so
   `SONDAGEAPI/app.db`, `app.db-shm`, and `app.db-wal` remain tracked. A binary DB written by both
   developers conflicts constantly, and `-shm`/`-wal` are transient SQLite internals. The migration
   is the source of truth. Needs `git rm --cached SONDAGEAPI/app.db SONDAGEAPI/app.db-shm
   SONDAGEAPI/app.db-wal` — but F-O pushed the DB deliberately (commit `75bce66`), so agree with him
   first and make sure any seed data he wants lives in a migration or a seeding routine.
6. ~~**Packages required by later livrables are missing:** the `CycloneDX` global tool (livrable
   10).~~ Resolved 2026-09-18: installed as a local tool (`cyclonedx` 6.2.0) via the root
   `.config/dotnet-tools.json` — see #10 above; the report itself still needs to be generated and
   committed as the artifact. `Microsoft.AspNetCore.Authentication.JwtBearer` is **not** needed by the token implementation of
   livrable 4 — it uses only the framework's built-in `AuthenticationHandler`. F-O's JWT branch
   needs it; whether it ever lands depends on which implementation the team keeps.
7. ~~**Template cruft.**~~ Cleared 2026-09-15: `/weatherforecast` and the `WeatherForecast` record
   went first, then `Models/Product.cs`, `DbSet<Product>` and the `Products` table, dropped by the
   `SurveyDomain` migration alongside the survey entities. `GET /api/sondages/{id:guid}` took over
   as the endpoint demonstrating the API key. The `InitialCreate` migration stays in history.
8. ~~**Two OpenAPI stacks registered at once.**~~ Resolved 2026-09-15: `AddSwaggerGen()` and
   `AddEndpointsApiExplorer()` were removed. `AddOpenApi()` produces the one document, and
   `UseSwaggerUI()` is kept purely as a UI shell pointed at `/openapi/v1.json`.
9. **The API key is a single static shared secret with no rotation or expiry.** Adequate for the
   livrable, but it is the weak point of the scheme — name it in #6 and #9 rather than letting a
   corrector find it. The current dev key also leaked into an assistant session and should be
   rotated.
10. **SQLite serialises writers** — relevant to livrable 9, not a bug. The 10-way race test passed
    with no lock errors at this scale, but one writer at a time is a property of the engine, not of
    the code. Any recommendation about real deployment should name PostgreSQL (or equivalent) and
    note that the conditional-`UPDATE` pattern carries over unchanged.
11. ~~**The SDK pin sits below the solution.**~~ Resolved 2026-09-17: the `sdk` block moved into the
    root `global.json`, which now covers both projects; `SONDAGEAPI/global.json` was deleted.
12. **The API project exposes its internals to `Tests`.** `<InternalsVisibleTo Include="Tests" />`
    in `SONDAGEAPI.csproj`, plus `public partial class Program;` at the end of `Program.cs`, are
    what let the test project use `WebApplicationFactory<Program>` and reach `ApiKeyMiddleware`,
    `InvitationToken` and the OpenAPI transformers. Both are the documented ASP.NET Core
    integration-testing pattern, but they widen the assembly's surface — worth a line in livrable 6,
    and worth checking against the obfuscation configuration of livrable 8, which will see a public
    `Program` and an `InternalsVisibleTo` target it must not rename.

## Conventions

- Course documents and graded deliverables are written in **French**; commit messages are mixed
  French/English. Code and identifiers are English, except user-facing API strings
  (`"Aucun produit avec l'id {id}"`), which are French.
- Since this is a *secure* programming course: when reviewing their code, call out injection,
  authn/authz gaps, secret handling, input validation, and error-message leakage explicitly — that
  critique is the point of the course, not a nitpick.
