# Configuration and secret templates

Use the tracked templates only as schemas. Do not put credentials, JWTs, patient data, or
production URLs in tracked files.

## Frontend

Copy [`../frontend/.env.example`](../frontend/.env.example) to `frontend/.env.local`.
Only `NEXT_PUBLIC_*` values are exposed to the browser, so they may contain the API URL,
time zone, and Firebase's public web configuration—but never a secret or access token.

## Backend

[`../backend/.env.example`](../backend/.env.example) lists the supported environment-variable
names. It is a reference file: .NET does not load `.env` files by itself. Set those values in the
shell, deployment platform, or user secrets. For local development, user secrets are preferred:

```powershell
cd backend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local MySQL connection>" --project src/Medilog.WebAPI
dotnet user-secrets set "Jwt:SigningKey" "<random value of at least 32 bytes>" --project src/Medilog.WebAPI
dotnet user-secrets set "Seed:Password" "<local demo password>" --project src/Medilog.WebAPI
```

The tracked `appsettings.json` and `appsettings.Development.json` intentionally have empty
secret slots. The full authentication, SMTP, Firebase, and seed-account guidance is in
[`../backend/docs/dev/AUTH_SETUP.md`](../backend/docs/dev/AUTH_SETUP.md).

## Required safeguards

- Use `Jwt__SigningKey`, never a frontend variable, for the signing key.
- Use `ConnectionStrings__DefaultConnection` or `MEDILOG_CONNECTION_STRING` for the database.
- Enable SMTP only with `Email__Host`, `Email__UserName`, and `Email__Password` configured.
- Keep `Seed__Enabled` restricted to development and set a non-empty `Seed__Password`.
- Rotate any credential that was previously exposed or entered into an untrusted location.
