# F1-C3b — Booking Lookups, Machine-Readable Error Codes, Dev Seed

- **Feature:** F1 — Appointment & Booking Management
- **Cycle:** F1-C3b (backend mini-cycle that unblocks the frontend, F1-C4)
- **Owner:** Menna
- **Branch:** `feature/menna-appointments`
- **Depends on:** F1-C1, F1-C2, F1-C3 (`Done`)
- **Status:** `Done` (2026-11-12) — approved by the Tech Lead via Menna together with F1-C4 (Q-C4-01, Q-C4-02, error-code part of Q-C4-06)
- **Workflow phase:** `plan → build → review → done` (compact; see the results below)

## 1. Goal
1. Give the booking screens the data they need without exposing anything medical: doctor and patient **lookups**.
2. Return **stable machine-readable codes** on error responses so the UI can show Arabic/English messages instead of English server text.
3. Provide **synthetic development data** so the screens can be exercised locally against MySQL.

## 2. Scope delivered
| Item | Detail |
|---|---|
| `GET /api/appointments/lookups/doctors` | A doctor gets themself; a secretary gets the assigned **active** doctors; ordered by name; `{id, fullName, specialization}` |
| `GET /api/appointments/lookups/patients?doctorId&search&limit` | Only that doctor's patients (404 if the doctor is out of scope); name/phone contains (literal match, accent/case-insensitive); `limit` 1–20 (default 10), applied in the database; `{id, fullName, phone}` only |
| Error codes | `code` on every application problem response (`ErrorCodes`, `DomainErrorCodes`), `errorCodes` per field for validation failures; `DomainException` and all typed Application exceptions now carry a code |
| Seed | `backend/docs/dev/seed-dev-data.sql` (4 doctors incl. one unassigned and one inactive, 1 secretary + assignments, 6 patients, 5 appointments) with cleanup section |
| Docs | `backend/docs/api/appointments.md` updated (lookups, code table, seed pointer) |

No migration, no schema change, no new package. Secretary and doctor scope rules are the ones already enforced by `AppointmentService`.

## 3. Acceptance criteria and results
| AC | Result | Evidence |
|---|---|---|
| Lookups scoped by role/assignment; patients scoped to the chosen doctor | Met | `LookupAndErrorCodeTests` (Application), `LookupAndErrorCodeEndpointTests` (HTTP) |
| Responses contain only `id`, name and phone (doctors: specialization) — no medical data | Met | reflection tests on the DTOs; HTTP tests assert exact property names and absence of medical/address text; generated SQL selects only `id`, `full_name`, `phone` |
| Validation of `doctorId`, `limit` (1–20), search length (≤ 100) | Met | tests (400 + field `errors`/`errorCodes`) |
| Codes on errors; stable and unique | Met | tests for each code family, uniqueness/format test, HTTP checks of `code` and `errorCodes` |
| Query translates in the real EF store; limit/order in the database | Met (translation + SQL inspection); **execution against MySQL not run** | scratch probe against an unreachable server stops at the connection step; SQL inspected |
| Seed matches the schema | Met structurally (column names and value counts checked against the migration); **not executed** (no MySQL here) | script check |
| `dotnet build` 0 warnings / 0 errors; `dotnet test` passes | Met | Application.Tests 115, WebAPI.Tests 57, all passing |

## 4. Review notes
- Search text uses `LOCATE`, so `%` and `_` typed by a user are literal (checked on the generated SQL).
- The doctor lookup lists **active** doctors only: an inactive doctor with history is not offered in the list filter (documented).
- Framework-generated 400s (malformed JSON etc.) have no `code`; the contract tells clients to treat them as `request.invalid`.
- Real-MySQL execution of the lookup queries, the doctor lock and the seed script remains an open verification item for Menna's machine.
