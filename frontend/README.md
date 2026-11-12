# Medilog — Frontend

Next.js frontend for **Medilog — Smart Medical Records Platform**.

This package currently contains the architectural foundation only: internationalization,
RTL/LTR support, Cairo font, providers, shared utilities, and feature module skeletons.
No medical business features are implemented yet.

## Stack

| Concern | Technology |
| --- | --- |
| Framework | Next.js 16 (App Router, Turbopack) |
| Language | TypeScript |
| Styling | Tailwind CSS 4 |
| i18n | next-intl 4 (locale-based routing) |
| Server state | TanStack React Query 5 |
| Client state | Zustand 5 |
| Animation | Framer Motion |
| Notifications | React Toastify |
| Icons | React Icons |
| Font | Cairo (`next/font/google`) |

## Commands

```bash
npm install
npm run dev        # http://localhost:3000
npm run build
npm run start
npm run lint
npm run typecheck
```

The default locale is Arabic, so `http://localhost:3000/` redirects to
`http://localhost:3000/ar`. English is available at `/en`.

## Structure

```text
src/
├── app/
│   ├── [locale]/
│   │   ├── layout.tsx       # <html lang/dir>, Cairo font, intl + app providers
│   │   ├── page.tsx         # technical verification page
│   │   └── providers.tsx    # React Query + ToastContainer
│   └── globals.css
├── components/
│   ├── layout/              # application shell components
│   ├── shared/              # cross-feature components (LocaleSwitcher)
│   └── ui/                  # reusable primitives
├── config/                  # static application configuration
├── constants/               # shared constants
├── features/                # feature-based business modules
│   ├── auth/
│   ├── patients/
│   ├── doctors/
│   ├── appointments/
│   ├── medical-records/
│   ├── medical-timeline/
│   └── dashboard/
├── hooks/                   # shared hooks
├── i18n/                    # routing, navigation, request configuration
├── lib/
│   ├── animations/          # shared Framer Motion variants
│   ├── query-client/        # QueryClient + QueryProvider
│   └── utils/               # shared utilities (toast wrapper)
├── messages/                # ar.json, en.json
├── schemas/                 # validation schemas
├── services/api/            # API client layer (not implemented yet)
├── store/                   # global UI stores
└── types/                   # shared types
```

Each feature folder follows the same shape:
`components/`, `hooks/`, `services/`, `store/`, `types/`, and a public `index.ts` barrel.
Business logic must stay inside its feature; only genuinely shared code belongs in
`components/`, `lib/`, `hooks/`, or `types/`.

## Internationalization

- Locales: `ar` (default), `en`.
- Locale prefix is always present (`/ar/...`, `/en/...`).
- `src/proxy.ts` handles locale detection and redirects.
- `src/i18n/request.ts` loads the matching message file.
- `src/app/[locale]/layout.tsx` sets `lang` and `dir` (`rtl` for Arabic, `ltr` for English).
- `NextIntlClientProvider` and `ToastContainer` receive the active direction.

Add a new language by updating `src/i18n/routing.ts` and adding
`src/messages/<locale>.json`.

## Conventions

- Import shared code through the `@/*` alias.
- Keep server-only logic out of client components; add `'use client'` only when needed.
- Feature-specific state belongs in `features/<feature>/store`, not the global store.
- Feature-specific API calls belong in `features/<feature>/services`; the shared HTTP
  client belongs in `services/api`.
- Do not call `react-toastify` directly from feature code; use `@/lib/utils/toast`.
- The shared HTTP client is `src/lib/api` (fetch-based; one `ApiError` type; one place that
  attaches the caller's identity). Features call it from their `services/`.

## Appointments (F1) — running against the API

The secretary appointment screens live in `src/features/appointments` and are served at
`/ar/appointments` and `/en/appointments` (`/appointments/new` to book). They use the API
documented in `backend/docs/api/appointments.md`.

```bash
cp .env.example .env.local     # then check NEXT_PUBLIC_API_BASE_URL
npm run dev                    # http://localhost:3000
```

1. Start the API in Development with a MySQL connection string (`dotnet run` in
   `backend/src/Medilog.WebAPI`) and load the synthetic data in
   `backend/docs/dev/seed-dev-data.sql`.
2. `NEXT_PUBLIC_API_BASE_URL` must be the exact URL the API serves. The backend `http` profile
   serves `http://localhost:5208`; the `https` profile redirects `5208` to `7206`, and a browser
   CORS preflight fails on a redirect.
3. **Temporary development identity (STATE.md Q-07):** real sign-in does not exist yet, so the API
   reads `X-User-Id` / `X-User-Role` headers. They are sent by `src/lib/api` from one provider
   (`src/features/auth/dev-identity`), only when `NEXT_PUBLIC_DEV_IDENTITY=true` in a
   **non-production** build; production builds never send them. Use the "Acting as" button in the
   top bar to enter an id and role (ids of the seed data are listed at the top of the SQL file).
   When authentication is delivered, only the provider and `useActingIdentity` change.

Quality gates: `npm run lint`, `npm run typecheck`, `npm test` (Vitest + Testing Library, jsdom),
`npm run build`.
