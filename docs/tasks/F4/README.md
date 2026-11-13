# F4 — Medical Updates & Prescriptions (Member 4)

Owner: **Mohamed Shalaby** — Member 4.
Scope source: `AGENTS.md` §2 Feature 4; `STATE.md` §2 and §6.

> This file is a **proposal** for the Cycle order only. It does not approve any Cycle and does
> not change scope. Each Cycle gets its own task file and its own approval gate
> (`plan` → `build` → `review` → `done`).

## Cycle order

| Cycle | Title | Layers | Depends on | Status |
|---|---|---|---|---|
| **F4-C1** | Medical domain model & EF Core configurations (`MedicalVisit`, `Diagnosis`, `Medication`) | Domain, Infrastructure | — | **Done** — merged with `main` (2026-11-10) |
| **F4-C2** | Create/update a medical visit with diagnoses (use case + DTOs + validators + endpoints + doctor scoping) | Application, WebAPI | F4-C1, Q-02 (doctor–patient access), Q-07 (auth), P0 contracts | **In review** — C2a Done; C2b built, verified (build 0/0, 50 tests) and pushed as `64693ea` |
| **F4-C3** | Add/update/delete prescribed medication lines on a visit (use case + endpoints + tests) | Application, WebAPI | F4-C1, F4-C2 | **In review** — implemented 2026-11-11; build 0/0, 82 tests, no model drift; D-23 needs Tech Lead ratification |
| **F4-C4** | Doctor medical entry/edit screens (visit, diagnosis, medication forms) | Frontend (`features/medical-records`) | F4-C2, F4-C3, Q-06 (frontend libraries) | **In progress** — approved 2026-11-11 (D-27…D-31, Q-11 deferred); C4a built and verified, C4b/C4c pending |

## Why this order

1. **C1 is schema-only** so it can land before authentication, controllers, and shared list
   contracts exist. It creates no HTTP surface, so it cannot leak medical data and cannot
   conflict with F1/F3 work.
2. **C2 needs the doctor-scoping decision (Q-02) and authentication (Q-07).** Writing medical
   data without a server-side acting-doctor identity would violate `AGENTS.md` §5.
3. **C3 is separated from C2** because medication write rules (nested collection add/remove)
   have different failure modes than visit creation and deserve their own reviewable diff.
4. **C4 comes last** because the frontend must reuse the agreed API contract, and the
   frontend API layer (`frontend/src/services/api`) is still empty (Q-06).

## Merge note (2026-11-10)

`F4-C1` was merged with `main`, which already contained the shared persistence foundation
(`Patient`/`Doctor` aggregates, MySQL provider, snake_case schema, `20261109211333_InitialCreate`,
and central audit rules in `MedilogDbContext.ApplyAuditRules()`). The F4-C1 configuration files
now follow that foundation:

- `PatientId` / `DoctorId` are real foreign keys with navigation properties (D-02 Option C
  superseded).
- Audit timestamps are populated centrally — **the audit interceptor is no longer an F4-C2
  dependency**.
- The child/visit patient-consistency invariant is enforced by the composite FK
  `(visit_id, patient_id)` → `(id, patient_id)`, so F4-C2 only needs request-level validation.

## Next action

- **C1** is closed and merged.
- **C2a** is Done; **C2b** is built, verified and pushed as `64693ea` (build 0/0, 50 tests, no
  model drift). Tech Lead ratification of the first API convention remains a merge-time gate.
- **C3 is built and verified** (`docs/tasks/F4/F4-C3.md`) with decisions D-23 (hard delete), D-24
  (`Medications` in the visit response) and D-25 (extend `IMedicalVisitService`). It is
  `In review`; the Tech Lead must ratify D-23 before merge.
- **C4** (frontend medical entry/edit screens) is **approved and in progress**: D-27 native-`fetch`
  client, D-28 development token bridge, D-29 TanStack Query + React-state forms, D-30 three
  routes, D-31 id bridge + `Header` link. Q-11 (frontend test framework) is **deferred**, and
  `frontend/src` is the agreed source of truth. C4a (API client, token bridge, workspace) is built
  and verified; C4b (visit create/detail/edit) and C4c (diagnoses/medications) remain.
- Frontend reality check: the app root is **`frontend/src`**, not `Front_End/` as `AGENTS.md` §6.1
  still states. C4 follows the real layout and records the documentation conflict.
