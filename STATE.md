# STATE.md — Medilog Project State

> **Live status of the current delivery.** Read after `AGENTS.md` at the start of every AI session.
> Records *status, decisions, blockers, next steps*. It does **not** define rules (`AGENTS.md` does) and must **not** change approved scope, ownership, or acceptance criteria.
> **Updated by:** the contributor at the end of each Cycle, through a PR reviewed by the Tech Lead. The AI only proposes the update (`ElwakeelBuild` → `done`).

- **Last updated:** 2026-11-14
- **Final delivery:** 2026-11-15 (night)
- **Tech Lead:** Abdullah
- **Integration branch:** `main`
- **Database:** MySQL
- **Current phase:** Phase 2 — MVP Integration (F6–F11)
- **Phase 1 (F1–F5):** merged and integrated (per Tech Lead). Execution reports not yet indexed.

## 1. Session Start Kit

Every AI session starts with, in this order:
1. `AGENTS.md`
2. `docs/PROJECT_OVERVIEW.md` — ⚠ still missing
3. `STATE.md`
4. `.ai/skills/ElwakeelManage.md` (planning) **or** `.ai/skills/ElwakeelBuild.md` (implementing)
5. The Cycle file `docs/tasks/<Feature>/<CycleId>.md` and only the code it touches

Commands: `plan → build → review → done`. A Cycle can be built only when its status is `Approved` (the Tech Lead changes `Draft` → `Approved` in the Cycle file **and** in §5 below).

## 2. Phase 1 Baseline (merged)

| ID | Feature | Status |
|----|---------|--------|
| F1 | Appointment & Booking Management | Merged |
| F2 | Payment Management (screens) | Merged — backend completion in F7 |
| F3 | Patient Medical Status | Merged — timeline completion in F10 |
| F4 | Medical Updates & Prescriptions | Merged — delete in F4-v2, vitals in F9 |
| F5 | Doctor Clinic Dashboard | Merged — real payment data in F7 |

## 3. Phase 2 — Owners and Branches

| ID | Feature | Owner | Branch | Cycles |
|----|---------|-------|--------|--------|
| F6 | Full Authentication & Identity System | Shalaby | `feature/shalaby-auth` | F6-C1, F6-C2 |
| F4-v2 | Diagnosis Delete | Shalaby | `feature/shalaby-auth` | F4v2-C1, F4v2-C2 |
| F7 | Payments Backend & Integration | Habiba | `feature/habiba-payments` | F7-C1, F7-C2 |
| F8 | Patient Management & Search | Menna | `feature/menna-patients` | F8-C1, F8-C2 |
| F9 | Vital Signs & Visit Enhancements | Anton | `feature/anton-vitals` | F9-C1, F9-C2 |
| F10 | Patient Medical Profile & Timeline Integration | Merihan | `feature/merihan-medical-profile` | F10-C1, F10-C2 |
| F11 | Admin Dashboard & Full E2E Integration | Abdullah | `feature/abdullah-admin-integration` | F11-C1, F11-C2 |

Cycle files: `docs/tasks/<Feature>/<CycleId>.md`. Rule: **C1 = Backend + DB + tests; C2 = Frontend + integration.**

## 4. Proposed Schedule (needs Tech Lead confirmation)

| Milestone | Target | Content |
|-----------|--------|---------|
| M1 — Checkpoint A | Today (Sat 11-14) | F6-C1 tasks T1–T3 + seed users merged: login + JWT claims + `Admin/Doctor/Secretary` seed. Unblocks role-protected testing for everyone |
| M2 — All C1 merged | Sun 11-15, midday | F6-C1 rest, F4v2-C1, F7-C1, F8-C1, F9-C1, F10-C1, F11-C1 |
| M3 — All C2 merged, feature freeze | Sun 11-15, ~18:00 | All frontend + integration cycles; unfinished work is cut at Cycle boundary |
| M4 — E2E + seed + demo | Sun 11-15, before night | F11-C2 integration pass, full build/lint/test, demo accounts, delivery |

## 5. Feature / Cycle Board

Status: `Draft` · `Approved` · `In progress` · `In review` · `Done` · `Blocked`

| Cycle | Owner | Depends on | Status | Blocking open items |
|-------|-------|-----------|--------|---------------------|
| F6-C1 Auth backend | Shalaby | — | Approved | Q-11, Q-12, Q-13, Q-14, Q-15 (⚠ oversized, see §7) |
| F6-C2 Auth frontend | Shalaby | F6-C1 | Approved | Q-13 |
| F4v2-C1 Diagnosis delete backend | Shalaby | Phase 1 | Approved | — |
| F4v2-C2 Diagnosis delete frontend | Shalaby | F4v2-C1 | Approved | — |
| F7-C1 Payments backend | Habiba | F6-C1 (A) | Approved | Q-21 |
| F7-C2 Payments frontend + dashboard | Habiba | F7-C1 | Approved | Q-21 |
| F8-C1 Patients backend | Menna | F6-C1 (A) | Approved | Q-17, Q-18 |
| F8-C2 Patients frontend + picker | Menna | F8-C1 | Approved | — |
| F9-C1 Vital signs backend | Anton | F6-C1 (A) | Approved | Q-19 |
| F9-C2 Vital signs frontend | Anton | F9-C1 | Approved | — |
| F10-C1 Timeline backend | Merihan | F6-C1 (A), F9-C1 (vitals events) | In review | Q-20 (proposed default in §8; VitalSigns events deferred to F10-C1-T4) |
| F10-C2 Timeline frontend | Merihan | F10-C1 | Approved | — |
| F11-C1 Admin backend | Abdullah | F6-C1 | Approved | Q-16, Q-22 |
| F11-C2 Admin frontend + E2E integration | Abdullah | F11-C1, all C2 | Approved | — |

## 6. Active Tasks

| Member | Active Cycle | Branch | Status | Last update |
|--------|--------------|--------|--------|-------------|
| Shalaby | — | `feature/shalaby-auth` | — | — |
| Habiba | — | `feature/habiba-payments` | — | — |
| Menna | — | `feature/menna-patients` | — | — |
| Anton | — | `feature/anton-vitals` | — | — |
| Merihan | F10-C1 | `feature/merihan-medical-profile` | In review | 2026-11-15 |
| Abdullah | — | `feature/abdullah-admin-integration` | — | — |

## 7. Risks

| Risk | Impact | Mitigation |
|------|--------|-----------|
| **F6-C1 is oversized** for one Cycle (login, register, email verification, reset, Google, SMTP, seed, tests). The 2-Cycle limit was set by the team; this exceeds the ElwakeelManage sizing rule | Delay blocks every other member's role testing | Checkpoint A (T1–T3 + seed) merged first; the rest of C1 follows in small commits; Tech Lead may move F4-v2 to another member |
| Many Cycles touch the same shared files (Program.cs, DbContext, migrations, messages `ar/en`, API client) | Merge conflicts, broken migrations | Short-lived branches, rebase often on the integration branch, **one migration at a time** merged in the order of §4; the owner of the file is notified (AGENTS.md §9) |
| Phase 1 decisions (payment model, doctor–patient access, patient status) exist only in code | Agents guess wrong rules | Record them in §8 first (F7-C1-T1 does this for payments) |
| Firebase / SMTP need external accounts and secrets | F6 blocked on environment | Create the Firebase project and SMTP/dev mail account before F6-C1 starts; secrets in env/user-secrets only |
| New packages (JWT bearer, SMTP client, Firebase JS SDK) | Unapproved dependencies | Each one approved in the `plan` of F6 and logged in §8 |

## 8. Decisions Log

### Approved
| Date | Decision | Source |
|------|----------|--------|
| 2026-11-08 | Phase 1 scope F1–F5 and technical requirements (filtering, pagination, FluentValidation, AutoMapper, entity configurations, workflow) | Instructor / AGENTS.md |
| 2026-11-14 | **Q-05:** .NET 10, MVC Controllers, plain Application services, no MediatR, no extra repository above `IApplicationDbContext`, FluentValidation, AutoMapper, EF Core Code First | Team |
| 2026-11-14 | **Q-06:** Next.js 16, React 19, TypeScript, next-intl, TanStack Query, native fetch, no Axios | Team |
| 2026-11-14 | **Q-07:** full authentication system in scope (login, register with role, email verification via SMTP, forgot/reset password, Google via Firebase ID token, seed users) | Team |
| 2026-11-14 | **Q-10:** Code First approved; Phase 2 branch names per §3; DB engine = **MySQL** and integration branch = **`main`** (recorded in the header) | Team |
| 2026-11-14 | **D-23:** hard delete for entry correction by the owning doctor (diagnosis delete, F4-v2) | Team |
| 2026-11-14 | Phase 2 scope F6–F11, owners, 2-Cycle rule (C1 backend, C2 frontend + integration), delivery 2026-11-15 night | Team |

### Phase 1 decisions implemented in code but not written down (Tech Lead must record the actual rule — agents must not guess)
| ID | Topic | Record by |
|----|-------|-----------|
| Q-01 / Q-21 | Payment model (what a payment attaches to, partial payments, methods, statuses, allowed status changes, revenue attribution) | F7-C1-T1, confirmed by Tech Lead |
| Q-02 | Doctor–patient access relationship | Tech Lead |
| Q-03 | Meaning of "patient status" (F3) and "patients' statuses today" (F5) | Tech Lead |
| Q-08 | Medical entities in scope for F3/F4 | Tech Lead |
| Q-09 | Whether cancelled appointments free their slot; secretary-to-doctor scoping | Tech Lead |

### Open questions for Phase 2 (a Cycle with a blocking question cannot be `Approved` without an answer or an explicit Tech Lead default)
| ID | Question | Proposed default (needs approval) | Blocks |
|----|----------|-----------------------------------|--------|
| Q-11 | After register: is the account usable after email verification only, or also needs Admin activation? Is login allowed before verification? | Email verification activates the account; Admin can deactivate; unverified cannot log in; seed users are pre-verified | F6, F11 |
| Q-12 | Google Sign-In: may it create new accounts (with which role) or only sign in an existing verified account with the same email? Backend verification method (framework JWT bearer against Firebase issuer/audience vs FirebaseAdmin package) | Sign in / link only by verified email, no auto-create; verify with framework JWT bearer (no extra package) | F6 |
| Q-13 | Access-token lifetime, frontend token storage, refresh token yes/no | Access token only, no refresh; lifetime from configuration; storage chosen and approved in the F6-C2 plan | F6-C2 |
| Q-14 | Identity storage (ASP.NET Core Identity vs custom credentials on `Doctor`/`Secretary`/Admin) and the exact JWT claims contract (must match what Phase 1 authorization and the dev token already use, plus `DoctorId`; secretary claim name) | Decided in F6-C1 `plan` after inspecting the repo | F6, F7–F11 |
| Q-15 | SMTP provider/library, dev mail delivery, code vs link, expiry values for verification and reset tokens, resend limits | Link/code single-use, expiry in configuration, values approved in F6-C1 plan | F6 |
| Q-16 | How are secretaries assigned to doctors after self-registration (FR-42)? Not listed in F11 | Admin assigns in F11 (`SecretaryDoctor`) | F11, secretary scope |
| Q-17 | Patient `DELETE` rule: D-23 only covers correcting entries | Reject with 409 when the patient has appointments, visits, or payments; only empty patients can be hard-deleted | F8 |
| Q-18 | Fields a secretary may see/edit on a patient (FR-49: name, DOB, gender, phone, address, emergency contact) | Blood type and any medical field excluded for secretary | F8 |
| Q-19 | Vital-sign fields and valid ranges (Class Spec: BP, HR, temperature, respiratory rate, weight, height, SpO2) | All Class Spec fields, optional, plausible-range validation approved by the team; edit by owning doctor only; no delete | F9 |
| Q-20 | Timeline event types and order (SRS lists visits, diagnoses, medications, labs, documents, notes; request mentions visits, medications, vitals) | Visit, Diagnosis, Medication, VitalSigns, MedicalNote; newest first with stable tie-breaker; labs/documents excluded | F10 |
| Q-22 | Admin role editing: allowed transitions, protection for self/last Admin, effect on existing data | Admin cannot change own role or deactivate self; last active Admin protected; role change limited to Doctor↔Secretary only if no conflicting data | F11 |

## 9. Blockers

| Date | Blocker | Blocking | Owner |
|------|---------|----------|-------|
| 2026-11-14 | Firebase project + SMTP account/secrets not confirmed | F6 | Shalaby / Tech Lead |
| 2026-11-14 | `docs/PROJECT_OVERVIEW.md` missing | Session Start Kit | Tech Lead |
| 2026-11-14 | Phase 1 decisions not written down (§8) | F7, F10, F11 | Tech Lead |

## 10. Execution Reports

| Cycle | Contributor | Date | Result | Report |
|-------|-------------|------|--------|--------|
| F10-C1 | Merihan | 2026-11-15 | Build 0/0; Application 243/243, WebAPI 118/118; no model changes; MySQL not verified | `docs/reports/F10-C1.md` |

## 11. Next Steps

1. All 14 Phase 2 Cycle files are `Approved` (2026-11-14) here and in each file. The Tech Lead still records answers/defaults for Q-11 … Q-22 in §8 as each Cycle's `plan` starts; a blocking question without an answer or explicit default stops that Cycle before `build`.
2. Shalaby starts **F6-C1 checkpoint A** (T1–T3 + seed); everyone else starts C1 `plan` in parallel and builds once checkpoint A is merged.
3. Follow §4 milestones; update §5 and §6 after each `done`.
4. Final week-end task list (F11-C2): full build/test, E2E role matrix, seed data, demo script.

## 12. Changelog

| Date | Change | By |
|------|--------|----|
| 2026-11-08 | Initial STATE.md (Phase 1 features, questions) | AI draft |
| 2026-11-14 | Phase 1 marked merged; Phase 2 (F6–F11) added with owners, branches, 14 Draft Cycles, schedule, risks, decisions (Q-05, Q-06, Q-07, Q-10 partial, D-23) and open questions Q-11…Q-22 | AI draft — pending Tech Lead review |
| 2026-11-14 | Basic data filled (Tech Lead: Abdullah, integration branch: `main`, DB: MySQL); all 14 Phase 2 Cycles set to `Approved` here and in their Cycle files; the “Draft cycles” blocker closed | Tech Lead |
| 2026-11-15 | F10-C1 timeline backend implemented (Visit/Diagnosis/Medication/MedicalNote per Q-20; type + instant-window filters, deterministic ordering, data-source pagination; API + 27 tests; contract doc §3.3) — `In review`, uncommitted, VitalSigns events deferred to F10-C1-T4 until F9-C1 merges | AI draft — pending Merihan/Tech Lead review |
| 2026-11-15 | F10-C1 review fixes applied (uncommitted): pagination-overflow guard (`long` skip, two-pass count→bounded fetch, clamped window; `pageNumber=int.MaxValue` → 200/empty/metadata), ordering contract corrected to the true ordinal type order (`Diagnosis` < `MedicalNote` < `Medication` < `Visit`) in the API doc/report/service remarks, GUID tie-break aligned to canonical string order (`Id.ToString()`, matching the SQL `char(36)` column) — 4 regression tests added | AI draft — pending Merihan/Tech Lead review |
