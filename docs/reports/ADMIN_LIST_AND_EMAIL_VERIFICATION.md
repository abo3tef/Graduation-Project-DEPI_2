# Execution Report — Admin list 500 + full email-verification flow

- **Date:** 2026-11-19
- **Scope:** two bounded fixes on the current working tree — (1) `GET /api/admin/users` returning
  `500`, including the Next.js 16 admin-route warning; (2) complete the registration → verify →
  sign-in flow with a visible Development verification link and resend actions. No scope expansion,
  no commit, no push, no PR.
- **Base:** existing uncommitted workspace. All pre-existing uncommitted work was preserved; the
  files listed below are only the ones edited during this session.

## 1. Root cause and fix — `GET /api/admin/users`

The runtime log showed:

```text
The LINQ expression ... .OrderBy(e0 => new AdminUserRecord(...).Email)
could not be translated.
```

`AdminService.AllUsers()` projected the three-table `UNION` straight into the positional
`AdminUserRecord`. EF Core could not map `OrderBy`/`Where` member access back through that record
constructor after the set operation. InMemory tests hid the fault; only the relational MariaDB
provider reproduced it.

Fix: project to a new composable `AdminUserRow` member-initializer after the `UNION`, apply all
filters, deterministic ordering (`Email`, then `Id` tie-breaker) and paging on that query, and map
to `AdminUserRecord` only in memory after the page is materialized. `FindUserAsync` uses the same
row → DTO mapping. A relational regression test compiles the exact `BuildListQuery` shape with the
MySQL provider via `ToQueryString()` (no live connection) for five filter combinations.

The Next.js 16 blocking-prerender warning on `/admin` is addressed with
`export const instant = false;` in the route file; a production build and a dev request to
`/en/admin` now report no `instant`/`blocking-prerender` warnings.

## 2. Email-verification flow

- `IAuthLinkBuilder.BuildEmailVerificationLink(token, email?)` now appends `&email=<url-encoded>`
  to the emailed link. The address is a resend-form convenience only; the token alone authorizes
  verification. Token-only clients stay compatible.
- `POST /api/auth/verify-email` accepts optional `email`; the validator checks its shape but never
  uses it for authorization. `VerifyEmailRequest` documents this explicitly.
- In Development with SMTP disabled, `EmailService` now logs the full message (including the
  one-time link) to the API console in addition to the `.eml` sink. With SMTP enabled, message
  bodies and tokens are still never logged.
- Frontend `/verify-email` reads both `token` and `email`, forwards them to the API, and prefills
  the resend form. `LoginForm` offers "Send a new link" after `403 auth.email_unverified`;
  `RegisterForm` offers the same action on the "check your inbox" screen. All new text is
  translated in `messages/en.json` and `messages/ar.json`.
- Existing behavior retained: registration returns `201` without a token, unverified sign-in is
  `403 auth.email_unverified`, seeded demo accounts are pre-verified, tokens are CSPRNG, hashed,
  time-limited and single-use, and resend always answers `204`.

## 3. Files changed in this session

### Backend
- `src/Medilog.Application/Features/Admin/AdminService.cs` — composable query + post-paging mapping.
- `src/Medilog.Application/Features/Admin/AdminUserRow.cs` **(new)** — translatable union row.
- `src/Medilog.Application/Medilog.Application.csproj` — `InternalsVisibleTo` for translation tests.
- `src/Medilog.Application/Common/Abstractions/IAuthLinkBuilder.cs` — optional email parameter.
- `src/Medilog.Application/Features/Auth/AuthTokenService.cs` — pass email to link builder.
- `src/Medilog.Application/Features/Auth/Contracts/AuthContracts.cs` — `VerifyEmailRequest.Email?`.
- `src/Medilog.Application/Features/Auth/Validation/VerifyEmailRequestValidator.cs` — optional-email shape rule.
- `src/Medilog.Infrastructure/Email/AuthLinkBuilder.cs` — `&email=` query parameter.
- `src/Medilog.Infrastructure/Email/EmailService.cs` — Development console body/link logging.
- `tests/Medilog.Application.Tests/Services/AdminQueryTranslationTests.cs` **(new)** — MySQL SQL regression.
- `tests/Medilog.Application.Tests/Email/AuthLinkBuilderTests.cs` **(new)** — link shape and encoding.
- `tests/Medilog.Application.Tests/Auth/AuthTestSupport.cs` — parse only the `token` query parameter.
- `tests/Medilog.Application.Tests/Validation/AuthRequestValidatorTests.cs` — optional-email validator cases.
- `tests/Medilog.WebAPI.Tests/Api/AuthRegistrationApiTests.cs` — assert emailed link carries `email=`.
- `tests/Medilog.WebAPI.Tests/Support/RecordingEmailService.cs` — parse only the `token` query parameter.
- `docs/api/AUTH_API.md` — contract and Development-ink explanation.

### Frontend
- `src/app/[locale]/admin/page.tsx` — `export const instant = false;`.
- `src/app/[locale]/(auth)/verify-email/page.tsx` — read `email` query parameter.
- `src/features/auth/types/auth.types.ts` / `api/auth.api.ts` — optional email in verify request.
- `src/features/auth/components/VerifyEmailScreen.tsx` — prefill + forward email.
- `src/features/auth/components/LoginForm.tsx` / `RegisterForm.tsx` — resend actions.
- `src/features/auth/api/auth.api.test.ts`, `VerifyEmailScreen.test.tsx`, `LoginForm.test.tsx`,
  `RegisterForm.test.tsx` — regression coverage for the new contract and UX.
- `src/messages/en.json` / `src/messages/ar.json` — `Auth.login.verifyNeeded`.

### Shared docs
- `docs/POSTMAN_ENDPOINTS.md` — optional `email` field on `POST /api/auth/verify-email`.

No EF migration was added: `20261114194949_AddAuthentication.cs` already contains
`EmailVerifiedAtUtc` and `auth_tokens`. No secret, connection string, JWT key or SMTP password was
printed or committed.

## 4. Verification actually run

| Command | Result |
|---|---|
| `dotnet build Medilog.sln` | **0 warnings, 0 errors** |
| `dotnet test Medilog.sln --no-build` | **574 Application + 225 WebAPI passed**, 0 failed |
| `npm run lint` | passed |
| `npm run typecheck` | passed |
| `npm test` | **48 files, 748 tests passed** |
| `npm run build` | passed, 57 static pages, no `instant`/prerender warning |

Live E2E (API on `http://localhost:5208`, Development, synthetic account):

```text
ADMIN_LOGIN=200
ADMIN_USERS=200 TOTAL=4
REGISTER=201 TOKEN_IN_BODY=False
PRE_VERIFY_LOGIN=403 CODE=auth.email_unverified
VERIFY=204 LINK_HAS_EMAIL=True
POST_VERIFY_LOGIN=200
ADMIN_USERS_RECHECK=200
```

Seed accounts checked through the same admin endpoint: `admin@medilog.local`,
`doctor@medilog.local`, `secretary@medilog.local` are all `Active` and `emailVerified=true`.
A fresh dev request to `/en/admin` returned `200` with `NEXT_INSTANT_WARNINGS=0`.

## 5. Deviations, risks, next step

- The `email` parameter is deliberately non-authoritative. It cannot be used to verify a different
  account because the token is bound to the account at issuance.
- Development console logging exposes the one-time link by design; the host already refuses the
  development email sink outside Development, and the SMTP-enabled path never logs bodies.
- The InMemory provider cannot catch relational translation faults; the new `ToQueryString()` test
  is the guard. Keep it when refactoring `AdminService`.
- No commit, push or PR was made. `STATE.md` was left to the contributor/Tech Lead for review.

### Proposed STATE update (not applied)

- F11-C1: `GET /api/admin/users` is no longer blocked by a relational translation fault; live
  endpoint returns `200` with the shared paged envelope. Regression test added.
- F11-C2: admin route no longer emits the Next.js 16 `instant`/blocking-prerender warning.
- F6-C2: verification link carries an optional `email` for resend prefill; Development console
  shows the verification link; login/register resend actions and ar/en strings are complete.
- Gates: backend 0 warnings/0 errors, 799 tests passed; frontend lint/typecheck/748 tests/build all
  passed; register → verify → sign-in E2E passed on the live API.
- Blocker: none. Next step is owner review of this diff and the `STATE.md` update, then the agreed
  PR flow.
