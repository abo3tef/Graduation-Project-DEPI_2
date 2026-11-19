# Authentication local setup (F6-C1)

> Everything here is **local development configuration**. Never commit a real signing key, SMTP
> password, Firebase service account or production URL (AGENTS.md §5).

The API starts without any of these values, but the affected endpoint answers `503` instead of
pretending to work. The one hard rule: **outside `Development`/`Testing` the host refuses to start
while `Email:Enabled` is `false`**, so a production deployment can never silently write mail to disk.

## 0. Database connection (never commit credentials)

`ConnectionStrings:DefaultConnection` in `appsettings.json` is intentionally empty. Supply a local
value only through user-secrets or the `MEDILOG_CONNECTION_STRING` environment variable:

```powershell
cd backend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local connection string>" --project src/Medilog.WebAPI
```

Infrastructure reads the configuration value first and falls back to `MEDILOG_CONNECTION_STRING`.
Never place a working remote credential in a tracked appsettings file, and rotate any credential
that was previously exposed before sharing the repository.

Startup applies the checked-in migrations through `DatabaseInitializer` before seeding. Set
`Database:ApplyMigrationsOnStartup=false` in user-secrets to manage the schema yourself with
`dotnet ef database update`. A local **MariaDB** server is supported automatically by
`MariaDbCompatibilityInterceptor` (it adapts `GET_LOCK` and the collation only when the server
reports MariaDB); MySQL 8 is unaffected.

## 1. JWT signing key (required for sign-in)

Sign-in issues an HS256 token, so `Jwt:SigningKey` must be at least 32 bytes. Store it in
user-secrets, never in a file:

```powershell
cd backend
dotnet user-secrets set "Jwt:SigningKey" "<value of at least 32 bytes>" --project src/Medilog.WebAPI
```

Generate a value:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

`Jwt:Issuer` (`medilog`), `Jwt:Audience` (`medilog-client`) and `Jwt:AccessTokenMinutes` (`1440`) are
in `appsettings.json` and normally need no change.

If the key is missing, `POST /api/auth/login` returns `503 auth.not_configured` instead of a token.

## 2. Transactional email

`Email` section (`appsettings.json`):

| Key | Meaning |
|---|---|
| `Enabled` | `false` = write messages to the development sink; `true` = send over SMTP |
| `Host`, `Port`, `UseStartTls` | SMTP server |
| `UserName`, `Password` | SMTP credentials — **user-secrets / environment variables only** |
| `FromAddress`, `FromName` | sender identity |
| `DevMailDirectory` | sink directory relative to the API content root (default `logs/dev-mail`) |

### Without SMTP (the demo default)

`appsettings.json` ships `Email:Enabled = false`, so registration, resend and forgot-password write
a `.eml` file under `backend/logs/dev-mail/` (gitignored) instead of sending mail. Open the newest
file, copy the link and continue in the frontend or by calling the API:

```powershell
Get-ChildItem backend\logs\dev-mail\*.eml | Sort-Object LastWriteTime | Select-Object -Last 1 |
  Get-Content
```

The log records only the file name; the recipient, subject and token are never logged.

### With SMTP

```powershell
cd backend
dotnet user-secrets set "Email:Enabled" "true" --project src/Medilog.WebAPI
dotnet user-secrets set "Email:Host" "smtp.example.test" --project src/Medilog.WebAPI
dotnet user-secrets set "Email:Port" "587" --project src/Medilog.WebAPI
dotnet user-secrets set "Email:UserName" "<smtp user>" --project src/Medilog.WebAPI
dotnet user-secrets set "Email:Password" "<smtp password>" --project src/Medilog.WebAPI
dotnet user-secrets set "Email:FromAddress" "no-reply@example.test" --project src/Medilog.WebAPI
```

If `Enabled` is `true` but `Host`/`Port`/`FromAddress` are missing, sending fails with
`503 auth.not_configured` — the message is never silently written to disk instead.

### Token lifetimes and links

| Key | Default | Meaning |
|---|---|---|
| `Auth:AppBaseUrl` | `http://localhost:3000` | frontend base URL used in emailed links |
| `Auth:EmailVerificationTokenMinutes` | `1440` | verification link lifetime (24 h) |
| `Auth:PasswordResetTokenMinutes` | `60` | reset link lifetime |

The links point at `<AppBaseUrl>/verify-email?token=...` and `<AppBaseUrl>/reset-password?token=...`;
those two routes are built in F6-C2.

## 3. Google Sign-In (Firebase)

1. Create a Firebase project and enable the Google provider.
2. Copy the **project id** (not a secret) into configuration:

```powershell
cd backend
dotnet user-secrets set "Firebase:ProjectId" "<firebase project id>" --project src/Medilog.WebAPI
```

That is all the backend needs. The token's issuer is derived as
`https://securetoken.google.com/<projectId>`, the audience is the project id, and Google's public
signing certificates are downloaded on first use and cached. No Firebase Admin SDK and no service
account JSON are used or committed (decision D-41).

To try it manually, obtain a Firebase ID token from the frontend (F6-C2 Google button) or a scratch
page that signs in with the same project, then:

```http
POST /api/auth/google
{ "idToken": "<firebase id token>" }
```

Google Sign-In signs in **an existing, email-verified, active account whose email matches the
token**, and never creates one (Q-12). Register with that email first and confirm it.

Without `Firebase:ProjectId` the endpoint returns `503 auth.google_not_configured`. A malformed,
forged, expired or wrong-audience token returns `401 auth.invalid_google_token`.

## 4. Development accounts

Development-only seed users (admin/doctor/secretary, pre-verified) and their password live in
[`SEED_ACCOUNTS.md`](SEED_ACCOUNTS.md). They are created only when `Seed:Enabled` is `true` **and**
the environment is `Development`.

## 5. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Host throws "No MySQL connection string is configured" | Set `ConnectionStrings:DefaultConnection` in user-secrets or `MEDILOG_CONNECTION_STRING` |
| `InvalidCastException: DBNull to Int64` in `AcquireDatabaseLockAsync` | MariaDB server: the compatibility interceptor rewrites `GET_LOCK` and normalizes the scalar; confirm it is registered in `AddInfrastructure` |
| `Unknown collation: 'utf8mb4_0900_ai_ci'` | MySQL 8-only collation on MariaDB: the same interceptor substitutes `utf8mb4_unicode_ci` |
| "Database initialization failed" at startup | Server unreachable or credentials wrong; check the logged inner exception and the connection string in user-secrets |
| `503 auth.not_configured` on login | `Jwt:SigningKey` missing or shorter than 32 bytes |
| `503 auth.google_not_configured` | `Firebase:ProjectId` missing, or Google certificates unreachable |
| `403 auth.email_unverified` after registering | Open the `.eml` link (or call `/verify-email`) before signing in |
| `403 auth.account_inactive` | Account deactivated; an admin reactivates it (F11) |
| No `.eml` file appears | `Email:Enabled` is `true`; check the SMTP settings instead |
| Host fails to start with "Email is disabled" | You are outside `Development`/`Testing`: configure SMTP or run in Development |

## 6. Tests

```powershell
cd backend
dotnet build Medilog.sln
dotnet test Medilog.sln
```

The suites use the EF Core InMemory provider, a recording email service, a fake Firebase validator
and a non-secret test signing key. They need no MySQL database, SMTP account or Firebase project.
