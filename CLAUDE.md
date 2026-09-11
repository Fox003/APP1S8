# APP1S8

School project for a **Secure Programming** course (APP1, S8). Team of 2: **Max** (Maxime Lescadre) and **F-O**.

The deliverable is a **secure .NET 10 Web API** plus supporting analysis, covering **11 livrables**
(numbered "Requis 1" … "Requis 11") from the *Guide de l'étudiant*.

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

From the *Guide de l'étudiant* (APP1 – S8, Module Sécurité informatique, p. 7). The numbering here
matches the "Requis N" labels used in `dependances_projet.md`.

| # | Livrable | Kind | Tooling / notes |
| --- | --- | --- | --- |
| 1 | API secured by **HTTPS**, authorization via an **access key** | code | Distinct from #4 — this is transport security + an API key on the endpoint |
| 2 | **OpenAPI** documentation integrated via **Swagger** | code | `Swashbuckle.AspNetCore` |
| 3 | **Postman collection** demonstrating the API's features, linked to the OpenAPI schema | artifact | No tooling chosen yet; export the collection into the repo |
| 4 | **Participant authentication** guaranteeing uniqueness of participation | code | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| 5 | **xUnit test battery, in the same solution as the API**, proving full code coverage *and* the mitigations from the attack-surface analysis | code | `xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `coverlet.collector`, `Moq`. **Requires a `.sln`** — see known issues. Ties back to #6. |
| 6 | **Security impact analysis**, including an analysis of attack vectors | document | Feeds the mitigations that #5 must test |
| 7 | **In-code security mechanisms** — stack execution prevention, Visual Studio hardening options | build config | NX/DEP, ASLR, Control Flow Guard etc. — csproj/linker properties, not library code |
| 8 | **Code protection by obfuscation** + the configuration used | code + document | `Obfuscar.GlobalTool` (installed globally as `obfuscar.console`); `obfuscar.xml` not written yet |
| 9 | **Operational recommendations** for the API (system architecture) | document | |
| 10 | **CycloneDX software bill of materials** report | artifact | `CycloneDX` / `dotnet-CycloneDX` (**not installed yet**) |
| 11 | A **security bug identification and publication process** | document | Vulnerability disclosure policy; conventionally a `SECURITY.md` |

Note the split: 1, 2, 4, 5, 8 are code; 6, 9, 11 are written analysis; 3, 10 are generated
artifacts; 7 is build configuration. Roughly half the grade is documents, so budget time for them.

Two dependencies run between livrables: **#6 → #5** (the attack-vector analysis defines the
mitigations the tests must prove) and **#2 → #3** (the Postman collection must be linked to the
OpenAPI schema). Doing #6 late forces rework of #5.

`dependances_projet.md` is the team's own reference doc mapping each dependency to its livrable.
Keep it in sync when dependencies change — it is a graded artifact, not just notes.

## Stack

- .NET 10 (`net10.0`), SDK pinned in `global.json` to `10.0.0` with `rollForward: latestMajor`
  (local SDK is 10.0.401).
- Written and run from **JetBrains Rider** (`.idea/` is committed) on Windows.
- Two projects, no solution file: `APP1S8.csproj` (root) and `Tests/Tests.csproj`.

## Commands

```bash
dotnet build                                        # main project only (no .sln yet)
dotnet test Tests/Tests.csproj                      # run the xUnit suite
dotnet test Tests/Tests.csproj --collect:"XPlat Code Coverage"   # Requis 5 coverage
obfuscar.console obfuscar.xml                       # Requis 8 (config file not written yet)
dotnet cyclonedx APP1S8.csproj -o ./sbom -f json    # Requis 10 (tool not installed yet)
```

## Current state (2026-09-11)

Scaffolding only. `Program.cs` is still `Console.WriteLine("Hello, World!")` and
`Tests/UnitTest1.cs` holds one empty `[Fact]`. No domain code, no API, no real tests.

## Known issues in the scaffolding

These are pre-existing and were flagged to the team; they are theirs to fix.

1. ~~`bin/` and `obj/` are committed with no `.gitignore`.~~ **Done 2026-09-11** — added the
   `dotnet new gitignore` template plus a project-specific block, and ran
   `git rm -r --cached bin obj Tests/obj` (114 deletions staged, files untouched on disk).
2. **Main project is a console app, not a web app.** It uses `Microsoft.NET.Sdk` with
   `<OutputType>Exe</OutputType>`. Swashbuckle and JwtBearer need `Microsoft.NET.Sdk.Web`, and
   `WebApplication.CreateBuilder` won't resolve until the SDK is switched.
3. **Test packages are in the main project.** `xunit`, `Microsoft.NET.Test.Sdk`,
   `xunit.runner.visualstudio`, `coverlet.collector`, and `Moq` are all referenced by
   `APP1S8.csproj`. This causes the `CS7022` build warning (the test SDK injects a competing entry
   point) and would ship test infrastructure inside the obfuscated assembly. They belong in
   `Tests/Tests.csproj` only — `Moq` in particular is currently *only* in the main project.
4. **`Tests` has no `ProjectReference` to `APP1S8`** — the test project cannot see the code under
   test, so no real test can be written yet.
5. **Version drift between the two projects.** `Microsoft.NET.Test.Sdk` 18.10.0 vs 17.14.1,
   `xunit.runner.visualstudio` 4.0.0 vs 3.1.4, `coverlet.collector` 10.0.1 vs 6.0.4. Once the test
   packages live in one place this resolves itself.
6. **No solution file.** `dotnet build` / `dotnet test` at the root only pick up one project. This
   is not just ergonomics: **livrable 5 explicitly requires the tests to live in the same solution
   as the API project**, so the missing `.sln` is a graded gap, not a preference.
7. `Obfuscar` is installed globally but there is no `obfuscar.xml`; `CycloneDX` is not installed at
   all. Both are needed for Requis 8 and 10.

## Conventions

- Course documents and graded deliverables (`dependances_projet.md`, reports) are written in
  **French**. Code, identifiers, and commit messages so far are in English.
- Since this is a *secure* programming course: when reviewing their code, call out injection,
  authn/authz gaps, secret handling, input validation, and error-message leakage explicitly — that
  critique is the point of the course, not a nitpick.
