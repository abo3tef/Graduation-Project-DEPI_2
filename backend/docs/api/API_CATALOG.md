# Medilog API catalog

This index reflects the route attributes in `Medilog.WebAPI` as of 2026-11-18. All protected
routes require `Authorization: Bearer <accessToken>`; identity headers are not supported. Shared
pagination, error, and JSON rules are in [`CONVENTIONS.md`](CONVENTIONS.md).

| Area | Routes | Roles | Contract |
|---|---|---|---|
| Authentication | `POST /api/auth/login`, `/register`, `/verify-email`, `/resend-verification`, `/forgot-password`, `/reset-password`, `/google` | Anonymous | [`AUTH_API.md`](AUTH_API.md) |
| Appointments | `POST, GET /api/appointments`; `GET /{id}`; `PUT /{id}/schedule`, `/status`; `POST /{id}/cancel`; `GET /lookups/doctors`, `/lookups/patients` | Doctor, Secretary | [`appointments.md`](appointments.md) |
| Patients | `POST, GET /api/patients`; `GET /lookup`, `/{id}`; `PUT, DELETE /{id}` | Doctor, Secretary | [`PATIENTS_API.md`](PATIENTS_API.md) |
| Payments | `POST, GET /api/payments`; `GET /{id}`, `/payable-appointments`; `PUT /{id}`, `/{id}/status` | Doctor, Secretary | Payment contract is recorded in `STATE.md` (F7-C1) |
| Medical visits | `POST /api/medical-visits`; `GET, PUT /{visitId}`; diagnoses and medications `POST, PUT, DELETE` below a visit | Doctor | [`MEDICAL_RECORDS_API.md`](MEDICAL_RECORDS_API.md) |
| Vital signs | `POST /api/medical-visits/{visitId}/vital-signs`; `GET` paged list; `PUT /{vitalSignsId}` | Doctor | [`MEDICAL_RECORDS_API.md`](MEDICAL_RECORDS_API.md) |
| Medical profile | `GET /api/patients/{patientId}/medical-profile`, `/medical-visits`, `/timeline` | Doctor | [`PATIENT_MEDICAL_API.md`](PATIENT_MEDICAL_API.md) |
| Doctor dashboard | `GET /api/dashboard/doctor` | Doctor | Dashboard feature contract/report |
| Administration | `GET /api/admin/users`; `PUT /users/{id}/status`, `/role`; `GET, PUT /secretaries/{secretaryId}/doctors` | Admin | [`ADMIN_API.md`](ADMIN_API.md) |

OpenAPI is available in Development at `/openapi/v1.json`. This catalog is an index, not a
replacement for feature contracts; request and response fields belong in the linked documents.
