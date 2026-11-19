# Medilog project overview

Medilog is a bilingual (Arabic/English) medical-records platform for clinic doctors,
secretaries, and administrators. It supports appointments, patients, medical visits,
diagnoses, prescriptions, payments, vital signs, timelines, and account administration.

## Current delivery context

Phase 1 (F1–F5) is the merged baseline. Phase 2 (F6–F11) is the approved MVP integration
scope. Individual cycles remain in review, in progress, or approved as recorded in
[`../STATE.md`](../STATE.md); this file does not promote a cycle to Done.

## Architecture

- Backend: .NET 10 ASP.NET Core MVC API using Clean Architecture (`Domain`, `Application`,
  `Infrastructure`, `WebAPI`) and MySQL through EF Core.
- Frontend: Next.js 16, React 19, TypeScript, next-intl, TanStack Query, native `fetch`, and
  Tailwind CSS 4. Source lives under `frontend/src/`.
- Security: protected API requests use validated JWT bearer tokens. The backend derives identity,
  role, and ownership from claims; browser route guards are UX only.

## Essential references

| Need | Source |
|---|---|
| Approved scope, security, workflow | [`../AGENTS.md`](../AGENTS.md) |
| Live cycle status, decisions, blockers | [`../STATE.md`](../STATE.md) |
| HTTP conventions and endpoints | [`../backend/docs/api/API_CATALOG.md`](../backend/docs/api/API_CATALOG.md) |
| Local credentials and authentication setup | [`../backend/docs/dev/AUTH_SETUP.md`](../backend/docs/dev/AUTH_SETUP.md) |
| Configuration templates | [`CONFIGURATION.md`](CONFIGURATION.md) |

## Local quality gates

```powershell
cd backend
dotnet restore
dotnet build Medilog.sln
dotnet test Medilog.sln

cd ../frontend
npm run lint
npm run typecheck
npm test
npm run build
```

These automated checks do not replace a live MySQL migration check, real SMTP/Firebase setup,
or the role and RTL acceptance checks that remain in `STATE.md`.
