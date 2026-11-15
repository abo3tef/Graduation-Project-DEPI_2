# Development seed accounts (F6-C1)

> **Development only.** These accounts exist so the demo can sign in without an SMTP server.
> They are created only when the API runs in the `Development` environment **and**
> `Seed:Enabled` is `true` (see `appsettings.Development.json`). Production and test hosts never
> run the seeder (`DevelopmentSeeder`, decision D-40).

## Accounts

| Role | Email | Password | State |
|------|-------|----------|-------|
| Admin | `admin@medilog.local` | `Medilog@Dev2026` | active, pre-verified |
| Doctor | `doctor@medilog.local` | `Medilog@Dev2026` | active, pre-verified |
| Secretary | `secretary@medilog.local` | `Medilog@Dev2026` | active, pre-verified |

- The password comes from `Seed:Password` in `appsettings.Development.json`. It is a local demo
  credential, not a production secret, and is never logged.
- Accounts are created **once**: the seeder is idempotent and leaves an existing account
  untouched, so a changed password or a deactivation (F11) survives a restart.
- The seeded doctor has no patients or appointments; use `seed-dev-data.sql` for clinic data.
- There are no real people behind these accounts (AGENTS.md §5).

## Required local configuration

Sign-in issues an HS256 token, so `Jwt:SigningKey` must be configured locally. It is **not**
committed. Set it once per machine with user-secrets:

```powershell
cd backend
dotnet user-secrets set "Jwt:SigningKey" "<random value of at least 32 bytes>" --project src/Medilog.WebAPI
```

Any 32+ byte value works for local development; generate one, for example:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

`Jwt:Issuer`, `Jwt:Audience` and `Jwt:AccessTokenMinutes` (default `1440`, i.e. 24 hours) are in
`appsettings.json` and normally need no change. Without a signing key, `POST /api/auth/login`
returns `503 auth.not_configured` instead of a token.

## Trying it

```powershell
cd backend/src/Medilog.WebAPI
dotnet run
```

```http
POST http://localhost:5xxx/api/auth/login
Content-Type: application/json

{ "email": "doctor@medilog.local", "password": "Medilog@Dev2026" }
```

The response contains `accessToken`, `expiresAtUtc` and the account summary (`user.role` is
`Doctor`, `user.doctorId` equals `user.id`). Send it to protected endpoints as
`Authorization: Bearer <accessToken>`. There is no refresh token (Q-13); sign in again after the
token expires.

## Notes

- The temporary `X-User-Id` / `X-User-Role` development headers keep working while
  `Auth:AllowHeaderIdentity` is `true` in `appsettings.Development.json`. F6-C2 removes the
  `DevTokenPanel`; F11-C2 removes the header fallback.
- Do not copy these credentials into a shared or production environment, and never commit a real
  signing key, SMTP password or Firebase service configuration.
