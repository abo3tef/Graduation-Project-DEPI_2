# Execution Report — Security / Config Remediation

- **Date:** 2026-11-18
- **Status:** Implemented and verified; **uncommitted** on `main`. No branch, commit, push or PR.
- **Plan:** `docs/reports/SECURITY_CONFIG_REMEDIATION_PLAN.md` — explicitly approved before build.
- **Source:** `docs/reports/BACKEND_GAP_ANALYSIS.md` G-01 … G-04; D-42; D-44.
- **Credential handling:** no discovered credential value is reproduced in this report.

## 1. Delivered changes

### Security and configuration
- `backend/src/Medilog.WebAPI/appsettings.json` — `DefaultConnection` reset to an empty string;
  `Firebase:ProjectId` restored as an empty public placeholder; unused `Auth:AllowHeaderIdentity`
  removed. The unrelated local `Cors` addition was left untouched.
- `backend/src/Medilog.WebAPI/appsettings.Development.json` — `Auth:AllowHeaderIdentity` removed;
  `Seed:Password` is now empty, to be supplied via user-secrets/environment.
- `backend/src/Medilog.WebAPI/Common/CurrentUser.cs` — no header reading, no `IConfiguration`
  dependency; identity is resolved only from validated principal claims (`sub` / `nameidentifier`,
  `role` / `role`).
- `backend/src/Medilog.WebAPI/Program.cs` — stale “Until Q-07 lands” comment replaced.
- `backend/src/Medilog.WebAPI/Controllers/AppointmentsController.cs` — class-level
  `[Authorize(Roles = "Doctor,Secretary")]`, 401 response documented, stale TODO removed.

### Dependency and migration tooling
- `backend/src/Medilog.Infrastructure/Medilog.Infrastructure.csproj` — removed the uncommitted
  `Pomelo.EntityFrameworkCore.MySql 9.0.0` reference; kept `MySql.EntityFrameworkCore 10.0.12`
  (D-44). This also removed the previous NU1608/NU1902 restore warnings.
- `backend/src/Medilog.Infrastructure/Persistence/Migrations/20261115000221_AddPayments.Designer.cs`
  — only `BuildTargetModel` synced from the current snapshot: `Admin`, `AuthToken` and
  `email_verified_at_utc` are now present. `AddPayments.cs`, migration IDs and all other migrations
  are unchanged.

### Tests
- `ApiTestSupport.cs` — test-only `HeaderTestAuthHandler` registered in `ApiFactory` so legacy
  appointment scenarios still send `X-User-*` headers inside the test host; production code never
  reads them. Test config no longer sets `Auth:AllowHeaderIdentity`.
- `HeaderIdentityTests.cs` — anonymous requests now assert `401`; role/format cases assert the
  correct `401`/`403`; the fallback-specific “header identity disabled” test was replaced by a
  real-bearer-host test proving forged identity headers cannot create a principal; added an Admin
  `403` case.
- `LookupAndErrorCodeEndpointTests.cs` — anonymous lookups now assert `401` /
  `ErrorCodes.Unauthorized`.
- `Api/AuthApiTests.cs` — added a real-JWT test proving a token from `/api/auth/login` authenticates
  `/api/appointments`.

### Documentation
- `backend/docs/api/appointments.md`, `CONVENTIONS.md`, `SEED_ACCOUNTS.md`, `AUTH_SETUP.md`,
  `seed-dev-data.sql` updated for bearer auth, user-secret-only credentials and no identity headers.
- `STATE.md` — D-42 updated, D-44 added, change-log row added. No scope/ownership/criteria changed.
- `docs/reports/BACKEND_GAP_ANALYSIS.md` — G-01 … G-04 marked closed.
- This report and the approved plan.

## 2. Commands actually run (observed results)

Run from `backend/` unless stated otherwise.

| Command | Result |
|---|---|
| `dotnet restore Medilog.sln` | Succeeded; all projects restored/up-to-date; no package warnings |
| `dotnet build Medilog.sln` | **Build succeeded — 0 warnings, 0 errors** |
| `dotnet test Medilog.sln` | **Passed — Application 550/550, WebAPI 222/222, total 772/772**, 0 failed |
| `dotnet ef migrations has-pending-model-changes --project src/Medilog.Infrastructure --startup-project src/Medilog.WebAPI` | **“No changes have been made to the model since the last migration.”** |
| `dotnet ef migrations script 20261114194949_AddAuthentication 20261115000221_AddPayments …` | Succeeded; generated SQL creates `payments` only — no `admins`/`auth_tokens` drop or alter |
| `git diff --check` (repo root) | No whitespace errors; one informational LF→CRLF notice for `CurrentUser.cs` (pre-existing pattern) |
| Config inspection | `appsettings*.json` contain only empty credential placeholders; no `AllowHeaderIdentity` key remains |

The migration script was written to a temporary directory outside the repository, not committed.

## 3. Acceptance criteria review

1. **No secrets in tracked config — met.** Empty placeholders remain; docs point to user-secrets/env.
2. **No header-identity fallback in production — met.** `CurrentUser` has no header or configuration
   path; a real bearer host returns `401` for forged headers.
3. **Appointments protected — met.** Anonymous → `401`, Admin → `403`, Doctor/Secretary → allowed;
   service-level scope and overlap rules unchanged and still tested.
4. **One MySQL provider — met.** Build/restore warnings cleared; `UseMySQL()` still compiles.
5. **Designer repaired — met.** Snapshot parity verified programmatically; EF reports no pending
   changes and the generated migration SQL contains only the payments changes.
6. **Existing behavior preserved — met.** Full suite green; two new tests net added (one removed
   fallback-only test, three added tests).
7. **Evidence recorded — met** in this report.

## 4. Deviations and issues encountered

- During the first designer edit, the automated replacement dropped the auto-generated file header;
  the compiler caught it (`CS1022`). The header was restored verbatim from HEAD and the build was
  re-run successfully. No other migration file was affected.
- `appsettings.json` still contains one unrelated uncommitted change: an extra `Cors` origin
  (`http://localhost:5555`). It was left untouched as out of scope.
- The dev seed password is no longer committed; developers must set `Seed:Password` in user-secrets
  before enabling seeding. `SEED_ACCOUNTS.md` documents this behavior.
- `git diff --check` still emits the same informational line-ending notice for `CurrentUser.cs`
  seen earlier in the session; it is not a whitespace error.

## 5. Remaining risks / not done

- **Credential rotation is not confirmed.** The owner/Tech Lead must rotate the previously exposed
  remote database credential and confirm before any commit or sharing. Cleaning the config does not
  undo exposure.
- Live Firebase token validation, SMTP delivery and a real MySQL connection remain unverified (no
  reachable instance/project was supplied).
- All previously reported non-security gaps remain open: F5 revenue stub (G-05), VitalSigns timeline
  events / Q-20 (G-06), pagination/active-state consistency items, and the missing
  `API_Endpoints_Summary.md` (G-07), which is still deliberately deferred until after this
  remediation.
- Stripe remains out of scope and unapproved.
- F11-C1 and this remediation are uncommitted on `main` pending the team's review/PR process.

## 6. Proposed STATE update

Already applied in `STATE.md`: D-42 closure, D-44 provider decision, and a change-log entry. No
Cycle status, ownership or acceptance criterion was changed.
