# Authentication API (F6-C1)

> Backend contract for feature F6 — Full Authentication & Identity System (Cycle F6-C1).
> Frontend work (F6-C2) consumes exactly this contract. Any change must be agreed with the F6 owner
> first (AGENTS.md §4.4).

All routes below live under `/api/auth` and are **anonymous**: the caller has no token yet. Every
other endpoint keeps using `Authorization: Bearer <accessToken>` exactly as in Phase 1. The shared
error envelope (`application/problem+json`, `code`, `traceId`, `errors`) is described in
[`CONVENTIONS.md`](CONVENTIONS.md); JSON uses camelCase and dates are ISO-8601 UTC.

## 1. Endpoints

| Method | Route | Request body | Success | Failure |
|---|---|---|---|---|
| `POST` | `/api/auth/login` | `email`, `password` | `200` + token | `400` validation, `401` credentials, `403` account state, `503` not configured |
| `POST` | `/api/auth/register` | `fullName`, `email`, `password`, `phone?`, `role`, `specialization?` | `201` + account summary (no `Location`, see §5) | `400` validation, `409` email exists, `503` email |
| `POST` | `/api/auth/verify-email` | `token`, `email?` | `204` | `400` validation, `401` invalid token |
| `POST` | `/api/auth/resend-verification` | `email` | `204` (always) | `400` validation, `503` email |
| `POST` | `/api/auth/forgot-password` | `email` | `204` (always) | `400` validation, `503` email |
| `POST` | `/api/auth/reset-password` | `token`, `newPassword` | `204` | `400` validation, `401` invalid token |
| `POST` | `/api/auth/google` | `idToken` | `200` + token | `400` validation, `401` invalid token, `403` account state, `503` not configured |

`role` accepts only `Doctor` or `Secretary` (closed allow-list; `Admin` is rejected with `400`).
`phone` is optional. `specialization` is mandatory for Doctor registration, returned in auth/account
responses for doctors, and rejected for Secretary registration. Curated options are available from
`GET /api/specializations`.

## 2. Sign-in response

Both `/login` and `/google` return the same shape:

```json
{
  "accessToken": "<compact JWT, HS256>",
  "expiresAtUtc": "2026-11-16T12:00:00+00:00",
  "user": {
    "id": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11",
    "fullName": "Demo Doctor",
    "email": "doctor@medilog.local",
    "role": "Doctor",
    "doctorId": "0f8f6f0a-6d4c-4a54-9a5e-2f2b0c9d1a11"
  }
}
```

- `doctorId` is present only for doctors and is equal to `id` (decision D-35), so the Phase 1
  doctor-scoped queries work unchanged.
- There is **no refresh token** (Q-13): the access token lasts 24 hours by default and the client
  signs in again after that. The token is returned once and never logged.

### Claims contract

| Claim | Value |
|---|---|
| `sub` | account id (`doctors.id`, `secretaries.id` or `admins.id`) |
| `role` | `Doctor`, `Secretary` or `Admin` |
| `doctorId` | doctors only — equals `sub` |
| `iss`, `aud`, `iat`, `nbf`, `exp`, `jti` | standard claims, HS256 with the configured key |

The server derives the acting user, role and ownership from these claims only. A client-supplied
id or role is never trusted (AGENTS.md §5).

## 3. Registration and email verification

```http
POST /api/auth/register
{
  "fullName": "New Doctor",
  "email": "new.doctor@example.test",
  "password": "Medilog@2026",
  "phone": "01000000000",
  "role": "Doctor",
  "specialization": "Cardiology"
}
```

`201` body: the account summary (`id`, `fullName`, `email`, `role`, `doctorId`) — **no token**.
The account is created `Inactive` with no verified email (Q-11); the user must open the emailed
link first. Registration never creates an `Admin`.

Password policy (register and reset): 8–128 characters with at least one upper-case letter, one
lower-case letter, one digit and one symbol.

The email contains a link to the frontend (`Auth:AppBaseUrl`), e.g.
`https://<frontend>/verify-email?token=<one-time token>&email=<url-encoded address>`. The token is
single-use and expires after 24 hours. `POST /api/auth/verify-email` accepts `token` and an optional
`email`; the address is copied from the link only to prefill the resend form, and the token alone
authorizes verification. Token-only clients remain compatible. Verification answers `204` and
activates the account; `resend-verification` invalidates the previous link and always answers `204`,
whether or not the address has an account.

In Development with SMTP disabled, the verification link and full message body are also written to
the API console so the register → verify → sign-in flow can be exercised without SMTP. With SMTP
enabled, message bodies and tokens are never logged.

## 4. Password recovery

`POST /api/auth/forgot-password` emails a reset link only when the address has an account and
always answers `204`, so it cannot be used to enumerate accounts. The reset token is single-use and
expires after 60 minutes.

`POST /api/auth/reset-password` replaces the password hash and consumes the token. A reset does
**not** verify the address: a registered account that never confirmed its email still cannot sign
in until it completes verification.

## 5. Success codes and deviations

- `register` returns `201` with the account summary and **no `Location` header**. This is decision
  D-38: the frozen contract has no "read account" route, so there is nothing to link to. It is the
  single documented exception to [`CONVENTIONS.md`](CONVENTIONS.md) §4.
- `verify-email`, `resend-verification`, `forgot-password` and `reset-password` answer `204` with an
  empty body.

## 6. Error codes

| `code` | Status | Meaning |
|---|---|---|
| `validation.failed` | 400 | Request shape is invalid; see `errors` |
| `auth.invalid_credentials` | 401 | Unknown email **or** wrong password (identical answer) |
| `auth.email_unverified` | 403 | Correct password, but the email address is not confirmed yet |
| `auth.account_inactive` | 403 | Account deactivated by an administrator (F11) |
| `auth.email_already_registered` | 409 | Registration reuses an existing account email |
| `auth.token_invalid` | 401 | Verification/reset token unknown, used, expired or wrong purpose |
| `auth.invalid_google_token` | 401 | Firebase token invalid, or no existing account for its email |
| `auth.google_not_configured` | 503 | `Firebase:ProjectId` is not configured |
| `auth.not_configured` | 503 | JWT signing key missing, or email cannot be sent |

## 7. Security rules enforced here

- Passwords are stored only as PBKDF2-HMAC-SHA256 hashes (600 000 iterations, per-password salt);
  they are never returned, logged or emailed.
- One-time tokens are generated with a CSPRNG, stored only as SHA-256 hashes, single-use, expiring,
  and a new token invalidates the previous one.
- `forgot-password` and `resend-verification` return the same answer for known and unknown emails;
  login reports the same error for an unknown email and a wrong password.
- Email is only ever sent to the address stored on the account, never to a caller-supplied one.
- Google Sign-In validates the Firebase ID token on the server (signature, issuer, audience,
  expiry) and only signs in an existing, email-verified, active account with the same address. It
  never creates an account and never takes a role from the client (Q-12).
- Logs contain no password, token, email body or medical data.

## 8. Related documents

- Local setup (JWT key, SMTP or the development mail sink, Firebase project id):
  [`../dev/AUTH_SETUP.md`](../dev/AUTH_SETUP.md)
- Development seed accounts: [`../dev/SEED_ACCOUNTS.md`](../dev/SEED_ACCOUNTS.md)
- Implementation report: `docs/reports/F6-C1.md`
