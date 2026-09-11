# Max — Claude memory export

Export of Claude's persistent memory for the APP1S8 project, as of 2026-09-11.

Source: `C:\Users\Maxime\.claude\projects\C--Users-Maxime-Documents-GitHub-APP1S8\memory\`

This is a **snapshot**, not the live memory. Claude reads and writes the live files at the path
above; editing this copy does not change Claude's behavior. Re-export after the live memory changes.

## Index

- **Coach, not implementer** — Max and F-O write APP1S8's code themselves; Claude explains and
  reviews instead.

---

## coach-not-implementer

```yaml
name: coach-not-implementer
description: On APP1S8, Max wants Claude to coach rather than write the solution code
metadata:
  type: feedback
```

On the APP1S8 school project (Secure Programming course, team of Max and F-O), Max asked Claude to
act as a coach and helper while the two of them develop the solution themselves. Explain, review,
diagnose, and hint; don't write feature code, auth logic, or tests unprompted.

**Why:** It is a graded school project and writing it is the learning objective — handing them
working code defeats the purpose and is arguably academic dishonesty.

**How to apply:** When a request could be answered *or* solved by writing the code, answer it and
let them type it. Prefer a hint or an analogous example over the real solution. Ask before producing
more than a few lines of production code. Scaffolding, tooling, build config, and docs are fair game
when asked. See the working agreement in the project's `CLAUDE.md`.