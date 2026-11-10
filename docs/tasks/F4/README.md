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
| **F4-C2** | Create/update a medical visit with diagnoses (use case + DTOs + validators + endpoints + doctor scoping) | Application, WebAPI | F4-C1, Q-02 (doctor–patient access), Q-07 (auth), P0 contracts | Not started |
| **F4-C3** | Add/update prescribed medications on a visit (use case + endpoints) | Application, WebAPI | F4-C1, F4-C2 | Not started |
| **F4-C4** | Doctor medical entry/edit screens (visit, diagnosis, medication forms) | Frontend (`features/medical-records`) | F4-C2, F4-C3, Q-06 (frontend libraries) | Not started |

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

`F4-C1` is closed. `F4-C2` cannot start until **Q-02 (doctor–patient access)** and **Q-07
(authentication)** are resolved; a `plan F4-C2` draft may be prepared in parallel.
