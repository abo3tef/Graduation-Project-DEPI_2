# Security / Config Remediation — PLAN (awaiting approval)

- **Date:** 2026-11-18
- **Status:** **Plan only — no implementation has started.** Must be explicitly approved before `build`.
- **Type:** Bounded cross-cutting remediation (no new business feature).
- **Source:** `docs/reports/BACKEND_GAP_ANALYSIS.md` findings G-01 … G-04, plus D-42 in `STATE.md`.
- **Branch:** current `main` working tree. All changes stay **uncommitted**; no branch, commit, push, or PR.
- **Credential rule:** no credential value is reproduced in this plan or in any report. Only file/line
  locations are recorded.

---

## 1. Goal

Close the four highest-priority security/config gaps identified in the approved gap analysis, without
reopening or reimplementing any completed Cycle:

1. Remove all secrets from tracked configuration (remote DB credential, dev seed password).
2. Remove the spoofable header-identity fallback from production code and configuration.
3. Protect all appointment HTTP endpoints with real JWT role authorization.
4. Restore a single, EF-10-compatible MySQL provider and repair the stale migration designer.

Explicitly **out of scope** for this remediation (each needs its own approved plan):

- F5 revenue `SUM(Paid)` aggregation (G-05).
- F10 VitalSigns timeline events / Q-20 (G-06).
- `API_Endpoints_Summary.md` and the payments contract doc (G-07) — still deferred until after this
  remediation, as agreed.
- Stripe / webhooks (G-16 — no approved decision).
- Medical-history paging overflow, active-doctor rechecks, Q-02/Q-08/Q-09 recording, branch/PR work.

---

## 2. Affected files and exact changes

### 2.1 Secrets and configuration

| File | Change |
|---|---|
| `backend/src/Medilog.WebAPI/appsettings.json` | Set `ConnectionStrings:DefaultConnection` back to `""` (the HEAD value). Restore the `Firebase` section to `"ProjectId": ""` (public placeholder, no secret). Leave the unrelated uncommitted `Cors` addition untouched. |
| `backend/src/Medilog.WebAPI/appsettings.Development.json` | Remove the `Auth:AllowHeaderIdentity` setting entirely. Replace the literal `Seed:Password` with `""` so demo credentials come from user-secrets/env. Keep `Seed:Enabled` as-is. |
| `backend/docs/dev/SEED_ACCOUNTS.md`, `backend/docs/dev/AUTH_SETUP.md` | Document `Seed:Password`, `ConnectionStrings:DefaultConnection` / `MEDILOG_CONNECTION_STRING`, and `Jwt:SigningKey` as user-secret/environment values. Remove statements that the header fallback is available. |

Rationale: Infrastructure already prefers `MEDILOG_CONNECTION_STRING` when the config value is blank
(`DependencyInjection.cs:34-49`); `SeedOptions.FromConfiguration` already tolerates a missing
password (`SeedOptions.cs:28-36`) and `DevelopmentSeeder` fails fast instead of creating unusable
accounts. No production code change is needed for the credential move itself.

**Credential rotation is an external action for the owner/Tech Lead.** Code cleanup does not undo
exposure; confirmation of rotation is required before any eventual commit.

### 2.2 Remove the header-identity fallback

| File | Change |
|---|---|
| `backend/src/Medilog.WebAPI/Common/CurrentUser.cs` | Delete `UserIdHeader`, `UserRoleHeader`, `EnabledSettingName`, the `IConfiguration` constructor parameter, `_allowHeaderIdentity`, and `ReadHeader`. `UserId`/`Role` resolve **only** from the authenticated principal. Keep both claim spellings (`ClaimTypes.NameIdentifier` + `sub`, `ClaimTypes.Role` + `role`) so `MapInboundClaims = false` and test hosts still work. Update XML docs. |
| `backend/src/Medilog.WebAPI/Program.cs` | Replace the stale `// Until Q-07 lands …` comment (lines 21-23) with an accurate statement that identity comes only from the validated bearer principal. No pipeline change. |

`CurrentUser` already receives its identity from `TestAuthHandler` claims in the Phase 2 test host
(`WebApiFactory.cs:78-90`), so those tests are unaffected.

### 2.3 Appointment HTTP authorization

| File | Change |
|---|---|
| `backend/src/Medilog.WebAPI/Controllers/AppointmentsController.cs` | Add `using Microsoft.AspNetCore.Authorization;`, class-level `[Authorize(Roles = "Doctor,Secretary")]`, remove the `TODO (Q-07)` comment, and add `[ProducesResponseType(StatusCodes.Status401Unauthorized)]`. Keep service-side scope checks as defense in depth. |
| `backend/docs/api/appointments.md` | Replace the `X-User-Id` / `X-User-Role` auth section with Bearer JWT + Doctor/Secretary roles; correct status-code statements. |
| `backend/docs/api/CONVENTIONS.md` | Update line 35 from “is removed in F11-C2” to record that the fallback has been removed. |

### 2.4 Test-host migration for the removed fallback

The Phase 1 appointment tests use `ApiFactory` + `X-User-*` headers, which today work **only** through
the production fallback being removed (`ApiTestSupport.cs:37-43, 93-116`). They must be moved to a
test-only authentication scheme so they still exercise the real pipeline without restoring the
production vulnerability.

| File | Change |
|---|---|
| `backend/tests/Medilog.WebAPI.Tests/ApiTestSupport.cs` | Register a small **test-only** `AuthenticationHandler` in `ApiFactory` that reads the existing `X-User-Id` / `X-User-Role` headers and emits `sub`/`role` claims (same shape as `TestAuthHandler`). Remove `Auth:AllowHeaderIdentity` from test config. Keep all existing `Caller` helpers and test bodies unchanged where possible. |
| `backend/tests/Medilog.WebAPI.Tests/HeaderIdentityTests.cs` | Rewrite tests that assert fallback-specific behavior: anonymous appointment requests now expect **401** from `[Authorize]`; wrong role expects **403**; active/inactive-user and wrong-role scope tests remain. Delete the now-meaningless “invalid header format” / “header identity disabled” cases and replace them with HTTP authorization cases. |
| `backend/tests/Medilog.WebAPI.Tests/AppointmentsEndpointTests.cs`, `ErrorEnvelopeTests.cs`, `LookupAndErrorCodeEndpointTests.cs` | Only where an anonymous request previously expected 403, change the expectation to 401. No business-assertion changes. |
| New test(s) in `AppointmentsEndpointTests.cs` or a dedicated file | Assert: anonymous → `401`; Admin role → `403`; a forged `X-User-Id` / `X-User-Role` header **without** the test auth principal → `401` (proves production no longer trusts headers); Doctor/Secretary → allowed. |

No `WebApiFactory`-based F6/F7/F8/F9/F10/F11 tests need changing: they already use `X-Test-*`
headers handled by `TestAuthHandler`.

### 2.5 Single MySQL provider and migration designer

| File | Change |
|---|---|
| `backend/src/Medilog.Infrastructure/Medilog.Infrastructure.csproj` | **Recommended:** remove the uncommitted `Pomelo.EntityFrameworkCore.MySql 9.0.0` and keep `MySql.EntityFrameworkCore 10.0.12`, because `DependencyInjection.cs:51` calls its `UseMySQL()` extension and EF Core is 10.0.14. Confirm whether `NU1608`/`NU1902` warnings disappear; if `NU1902` persists, report it rather than silently adding a package. |
| `backend/src/Medilog.Infrastructure/Persistence/Migrations/20261115000221_AddPayments.Designer.cs` | Replace only the `BuildTargetModel` body with the integrated final model from `MedilogDbContextModelSnapshot.cs` (adds `Admin`, `AuthToken`, `email_verified_at_utc`). Do **not** change `AddPayments.cs` Up/Down, migration IDs, or any other migration. No `migrations remove` / `add` / database reset. |

The designer repair is a tooling-model fix, not a schema change. It needs review because it touches
shared migration history; it is included here because the user selected this remediation, and it will
be reported explicitly in the execution report.

### 2.6 Documentation / state after build

- `docs/reports/BACKEND_GAP_ANALYSIS.md` — mark G-01 … G-04 resolved with evidence (no credential values).
- `STATE.md` — record D-42 completion (header fallback removed) and the provider/designer/config
  outcome. No scope, ownership, or acceptance-criteria changes.
- `docs/reports/SECURITY_CONFIG_REMEDIATION.md` — execution report: files changed, commands run,
  actual results, deviations, remaining risks, rotation confirmation status.

---

## 3. Acceptance criteria

1. No real-looking DB credential, SMTP password, JWT key, or demo password exists in any tracked or
   working-tree `appsettings*.json`. Empty placeholders remain; secrets are read from env/user-secrets.
2. `CurrentUser` contains no header-reading code and no `Auth:AllowHeaderIdentity` setting; a request
   with forged `X-User-*` headers and no valid bearer token cannot obtain an identity.
3. Every `/api/appointments` route returns `401` for anonymous callers, `403` for Admin, and works only
   for authenticated Doctor/Secretary principals within their existing service-level scope.
4. Only one MySQL EF provider package is referenced; `UseMySQL()` still compiles; no new NuGet warning
   is introduced.
5. `AddPayments.Designer.cs` target model equals the current snapshot (contains Admin/AuthToken and
   verification columns); EF reports no pending model changes; migration script generation succeeds.
6. Existing F6-F11 behavior and tests remain green except for deliberate status-code expectations
   (403 → 401) and removal of fallback-only tests.
7. All required commands in §5 actually pass, and the report records the observed output.

---

## 4. Test plan

| Level | Cases |
|---|---|
| HTTP (test auth) | All appointment routes: anonymous 401; Admin 403; Doctor/Secretary success; forged production headers without test principal 401. |
| HTTP (real JWT) | At least one appointment route accepts a token issued by `/api/auth/login`; no token → 401. Reuse `WebApiFactory(useTestAuthentication: false)`. |
| Regression | Existing overlap/boundary/scope tests unchanged; error-envelope tests still verify `application/problem+json` for 401/403/404. |
| EF tooling | `dotnet ef migrations has-pending-model-changes`; `dotnet ef migrations script 20261114194949_AddAuthentication 20261115000221_AddPayments -o <temp file>` and confirm no auth-table drop/recreate appears. |
| Config | Inspect (not print values) that only empty placeholders remain in appsettings files. |

---

## 5. Commands to run after approval

From `backend/`:

```bash
dotnet restore Medilog.sln
dotnet build Medilog.sln
dotnet test Medilog.sln
dotnet ef migrations has-pending-model-changes --project src/Medilog.Infrastructure --startup-project src/Medilog.WebAPI
dotnet tool restore   # only if dotnet-ef is not already restored
```

From repository root:

```bash
git diff --check
```

All results will be reported exactly as observed, including any remaining pre-existing warnings.
No test result will be claimed unless it was executed.

---

## 6. Risks and required decisions

| Risk / decision | Handling |
|---|---|
| **Credential rotation** cannot be performed from code. | Owner/Tech Lead must rotate the exposed remote DB credential and confirm before any commit. |
| **MySQL provider choice** is a shared dependency decision. | Recommended Oracle provider (see §2.5). Approval of this plan implies approval of that choice; say so now if Pomelo is required instead. |
| **Designer edit touches shared migration history.** | Bounded to one auto-generated file; reviewed diff; no migration deletion or regeneration. |
| **Removing the dev fallback changes many Phase 1 test expectations.** | Test-only auth handler keeps business coverage; only auth-status assertions change. |
| **Live Firebase/SMTP/MySQL smoke tests remain unverified.** | Out of scope here; still reported as a residual risk. |

---

## 7. Approval gate

Per `AGENTS.md §8.2/§8.3`, implementation may start only after explicit approval of this plan.
Please approve, request changes to a specific section, or choose an alternative provider if needed.
