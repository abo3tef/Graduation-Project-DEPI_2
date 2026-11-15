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
| F6-C1 Auth backend | Shalaby | — | In review | Q-11, Q-12, Q-13, Q-14, Q-15 — answered (D-32…D-43); started 2026-11-14 (⚠ oversized, see §7) |
| F6-C2 Auth frontend | Shalaby | F6-C1 | Approved | Q-13 |
| F4v2-C1 Diagnosis delete backend | Shalaby | Phase 1 | Approved | — |
| F4v2-C2 Diagnosis delete frontend | Shalaby | F4v2-C1 | Approved | — |
| F7-C1 Payments backend | Habiba | F6-C1 (A) | Approved | Q-21 |
| F7-C2 Payments frontend + dashboard | Habiba | F7-C1 | Approved | Q-21 |
| F8-C1 Patients backend | Menna | F6-C1 (A) | Approved | Q-17, Q-18 |
| F8-C2 Patients frontend + picker | Menna | F8-C1 | Approved | — |
| F9-C1 Vital signs backend | Anton | F6-C1 (A) | Approved | Q-19 |
| F9-C2 Vital signs frontend | Anton | F9-C1 | Approved | — |
| F10-C1 Timeline backend | Merihan | F6-C1 (A), F9-C1 (vitals events) | Approved | Q-20 |
| F10-C2 Timeline frontend | Merihan | F10-C1 | Approved | — |
| F11-C1 Admin backend | Abdullah | F6-C1 | Approved | Q-16, Q-22 |
| F11-C2 Admin frontend + E2E integration | Abdullah | F11-C1, all C2 | Approved | — |

## 6. Active Tasks

| Member | Active Cycle | Branch | Status | Last update |
|--------|--------------|--------|--------|-------------|
| Shalaby | F6-C1 (Auth backend) | `feature/shalaby-auth` | In review | 2026-11-15 |
| Habiba | — | `feature/habiba-payments` | — | — |
| Menna | — | `feature/menna-patients` | — | — |
| Anton | — | `feature/anton-vitals` | — | — |
| Merihan | — | `feature/merihan-medical-profile` | — | — |
| Abdullah | — | `feature/abdullah-admin-integration` | — | — |

## 7. Risks

| Risk | Impact | Mitigation |
|------|--------|-----------|
| **F6-C1 is oversized** for one Cycle (login, register, email verification, reset, Google, SMTP, seed, tests). The 2-Cycle limit was set by the team; this exceeds the ElwakeelManage sizing rule | Delay blocks every other member's role testing | Checkpoint A (T1–T3 + seed) merged first; the rest of C1 follows in small commits; Tech Lead may move F4-v2 to another member |
| Many Cycles touch the same shared files (Program.cs, DbContext, migrations, messages `ar/en`, API client) | Merge conflicts, broken migrations | Short-lived branches, rebase often on the integration branch, **one migration at a time** merged in the order of §4; the owner of the file is notified (AGENTS.md §9) |
| Phase 1 decisions (payment model, doctor–patient access, patient status) exist only in code | Agents guess wrong rules | Record them in §8 first (F7-C1-T1 does this for payments) |
| Firebase / SMTP need external accounts and secrets | Live Google sign-in and real email unverified until the accounts exist | F6-C1 is not blocked: the tests use a fake Firebase validator and the `.eml` dev sink; create the Firebase project and SMTP account before the demo; secrets in env/user-secrets only |
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
| 2026-11-14 | **Q-11:** email verification activates the account; unverified or inactive accounts cannot log in; seed users are pre-verified; Admin may deactivate (F11) | F6-C1 plan — Tech Lead confirms at PR |
| 2026-11-14 | **Q-12:** Google signs in / links an existing verified account with the same email only, never auto-creates; the Firebase ID token is validated by the framework JWT handler against `securetoken.google.com/{projectId}` (no FirebaseAdmin package); the role is never taken from the client | F6-C1 plan — Tech Lead confirms at PR |
| 2026-11-14 | **Q-13:** access token only, **no refresh token**; 24 h demo lifetime from configuration (`Jwt:AccessTokenMinutes`, default `1440`); frontend token storage is decided in F6-C2 | F6-C1 plan — Tech Lead confirms at PR |
| 2026-11-14 | **Q-14:** **no ASP.NET Core Identity** — custom credentials already exist on `doctors`/`secretaries` (`PasswordHash`, `AccountStatus`, unique email); an `admins` table is added; claims are `sub` = user id, `role` = `Doctor`/`Secretary`/`Admin`, `doctorId` for doctors (equal to `sub`), HS256 | F6-C1 T1 inspection — Tech Lead confirms at PR |
| 2026-11-14 | **Q-15:** BCL `System.Net.Mail` SMTP behind `IEmailService` (secrets in user-secrets/env, dev file sink when unconfigured); verification/reset tokens are 32-byte CSPRNG values stored as SHA-256 hashes, single-use, previous invalidated; verification 24 h, reset 60 min; link style | F6-C1 plan — Tech Lead confirms at PR |

### F6-C1 plan decisions (2026-11-14, recorded before `build`)
| ID | Decision | Approved value |
|----|----------|----------------|
| **D-32** | Identity storage (Q-14) | No ASP.NET Core Identity and no user table refactor: `doctors`/`secretaries` keep their credentials. A new `admins` table is added, plus `email_verified_at_utc` on all three account types. `Admin` becomes a legitimate `UserRole`/`Roles` value |
| **D-33** | Auth token storage | One `auth_tokens` table (id, user id, user type, purpose, `token_hash`, `expires_at_utc`, `consumed_at_utc`, `created_at_utc`), unique index on `token_hash`; issuing a new token consumes the previous unconsumed one for the same user + purpose |
| **D-34** | Password hashing | BCL PBKDF2-HMAC-SHA256 (`Rfc2898DeriveBytes.Pbkdf2`), 600 000 iterations, 16-byte salt, 32-byte key, stored as `v1$iterations$salt$hash`, verified with `CryptographicOperations.FixedTimeEquals` — **no new package** |
| **D-35** | Claims contract | `sub` (user id), `role` (`Doctor`/`Secretary`/`Admin`), `doctorId` (doctors only), `iss`, `aud`, `iat`, `nbf`, `exp`, `jti`; HS256 with a ≥32-byte key from user-secrets/env. `sub == doctorId` for doctors, so Phase 1 authorization is unchanged |
| **D-36** | Lifetimes | Access token `Jwt:AccessTokenMinutes` (default 1440 = 24 h), verification token 24 h, reset token 60 min — all from configuration, values approved for the demo |
| **D-37** | Auth error shape | New stable codes on the existing `ProblemDetails` envelope: `auth.invalid_credentials`, `auth.email_unverified`, `auth.account_inactive`, `auth.email_already_registered`, `auth.token_invalid`, `auth.invalid_google_token`, `auth.google_not_configured`, `auth.not_configured` (401/403/409/503) |
| **D-38** | Register response | `201` with the created account summary and **no `Location`** (the frozen contract has no account read route) — documented deviation from `CONVENTIONS.md` §4 |
| **D-39** | Email delivery | `IEmailService` + BCL SMTP; when SMTP is disabled/unconfigured in Development the message is written as `.eml` under `backend/logs/dev-mail/` (gitignored); tokens and passwords are never logged |
| **D-40** | Seed users | Development-only, idempotent seeder (`Seed:Enabled`), pre-verified `admin@medilog.local` / `doctor@medilog.local` / `secretary@medilog.local`; passwords documented in `backend/docs/dev/SEED_ACCOUNTS.md`; never seeded in production |
| **D-41** | Packages | No new runtime package: JWT issuing uses `Microsoft.IdentityModel.*` 8.19.2 already resolved by the approved `JwtBearer` package; SMTP, PBKDF2 and secure random are BCL |
| **D-42** | Header identity | `Auth:AllowHeaderIdentity` stays `true` in Development until F6-C2 removes `DevTokenPanel`; removal of the fallback belongs to F11-C2 (E2E) |
| **D-43** | Email normalization | Emails are trimmed and lower-cased before compare/store; one account per email across doctor/secretary/admin (application check + per-table unique index) |

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
| Q-16 | How are secretaries assigned to doctors after self-registration (FR-42)? Not listed in F11 | Admin assigns in F11 (`SecretaryDoctor`) | F11, secretary scope |
| Q-17 | Patient `DELETE` rule: D-23 only covers correcting entries | Reject with 409 when the patient has appointments, visits, or payments; only empty patients can be hard-deleted | F8 |
| Q-18 | Fields a secretary may see/edit on a patient (FR-49: name, DOB, gender, phone, address, emergency contact) | Blood type and any medical field excluded for secretary | F8 |
| Q-19 | Vital-sign fields and valid ranges (Class Spec: BP, HR, temperature, respiratory rate, weight, height, SpO2) | All Class Spec fields, optional, plausible-range validation approved by the team; edit by owning doctor only; no delete | F9 |
| Q-20 | Timeline event types and order (SRS lists visits, diagnoses, medications, labs, documents, notes; request mentions visits, medications, vitals) | Visit, Diagnosis, Medication, VitalSigns, MedicalNote; newest first with stable tie-breaker; labs/documents excluded | F10 |
| Q-22 | Admin role editing: allowed transitions, protection for self/last Admin, effect on existing data | Admin cannot change own role or deactivate self; last active Admin protected; role change limited to Doctor↔Secretary only if no conflicting data | F11 |

## 9. Blockers

| Date | Blocker | Blocking | Owner |
|------|---------|----------|-------|
| 2026-11-14 | Firebase project + SMTP account/secrets not confirmed | F6 live demo only (no longer F6-C1: the automated checks use a fake Firebase validator and the `.eml` sink) | Shalaby / Tech Lead |
| 2026-11-14 | `docs/PROJECT_OVERVIEW.md` missing | Session Start Kit | Tech Lead |
| 2026-11-14 | Phase 1 decisions not written down (§8) | F7, F10, F11 | Tech Lead |
| 2026-11-15 | No reachable MySQL instance while building F6-C1 | Applies/verifies the auth migration against a real database | Shalaby / Tech Lead |

## 10. Execution Reports

| Cycle | Contributor | Date | Result | Report |
|-------|-------------|------|--------|--------|
| F6-C1 — Auth backend | Shalaby | 2026-11-15 | In review | `docs/reports/F6-C1.md` |

## 11. Next Steps

1. All 14 Phase 2 Cycle files are `Approved` (2026-11-14) here and in each file. The Tech Lead still records answers/defaults for Q-16 … Q-22 in §8 as each Cycle's `plan` starts; a blocking question without an answer or explicit default stops that Cycle before `build`. Q-11 … Q-15 are answered in §8.
2. **Tech Lead review + merge of `feature/shalaby-auth`**, checkpoint A (`b3e4bd3`, `a941ba1`) first so the other features can test real roles; the rest of F6-C1 follows in the same PR. Auth contract for F6-C2: `backend/docs/api/AUTH_API.md`.
3. Shalaby then starts **F6-C2** (frontend screens, central token handling, `DevTokenPanel` removal). Everyone else continues C1 `plan` in parallel.
4. Follow §4 milestones; update §5 and §6 after each `done`.
5. Final week-end task list (F11-C2): full build/test, E2E role matrix, seed data, demo script.

## 12. Changelog

| Date | Change | By |
|------|--------|----|
| 2026-11-08 | Initial STATE.md (Phase 1 features, questions) | AI draft |
| 2026-11-14 | Phase 1 marked merged; Phase 2 (F6–F11) added with owners, branches, 14 Draft Cycles, schedule, risks, decisions (Q-05, Q-06, Q-07, Q-10 partial, D-23) and open questions Q-11…Q-22 | AI draft — pending Tech Lead review |
| 2026-11-14 | Basic data filled (Tech Lead: Abdullah, integration branch: `main`, DB: MySQL); all 14 Phase 2 Cycles set to `Approved` here and in their Cycle files; the "Draft cycles" blocker closed | Tech Lead |
| 2026-11-14 | Q-11 … Q-15 recorded as approved F6 defaults; F6-C1 plan decisions D-32 … D-43 added to §8 (identity storage, auth-token table, PBKDF2, claims, lifetimes, error codes, seed, packages) | Shalaby (F6 owner) — Tech Lead confirms at PR |
| 2026-11-15 | F6-C1 implemented on `feature/shalaby-auth` (checkpoint A → slice E) and moved to `In review`; execution report `docs/reports/F6-C1.md` added; Firebase/SMTP blocker narrowed to the live demo; MySQL blocker added | Shalaby (F6 owner) — Tech Lead review pending |
