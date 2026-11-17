# Patient Medical API (F3-C2)

Doctor-facing **read** endpoints for a patient's medical profile and paged medical history
(Feature F3 — Patient Medical Status). Implemented by `MedicalRecordsController` as a thin
adapter over `IMedicalRecordService`; all authorization, validation and data-scope rules live
in the Application layer (D-11, F3-C1 task file §0). Shared conventions are defined in
`CONVENTIONS.md`; the visit-centric write side is documented in `MEDICAL_RECORDS_API.md`.

## 1. Authentication and data scope

| Aspect | Value |
|---|---|
| Required role | `Doctor` (`[Authorize(Roles = "Doctor")]` on the controller) |
| Authenticated non-doctor (e.g. `Secretary`) | `403 Forbidden` — framework role check, no data access |
| Unauthenticated request | `401 Unauthorized` |
| Data scope | Only patients whose `Patient.DoctorId` equals the acting doctor (D-17 / BRULE-03) |
| Out-of-scope or unknown patient | `404 Not Found` (`patient.not_found`, identical for both) — the API does not reveal which patients exist (D-05) |

The deeper Application boundary (`403 medical.access_denied` before any store call for a
non-doctor principal) is defense-in-depth against authorization misconfiguration and is proven
by the F3-C1 unit tests.

## 2. Endpoints

| # | Method | Route | Success |
|---|--------|-------|---------|
| 1 | `GET` | `/api/patients/{patientId}/medical-profile` | `200 OK` |
| 2 | `GET` | `/api/patients/{patientId}/medical-visits` | `200 OK` |
| 3 | `GET` | `/api/patients/{patientId}/timeline` | `200 OK` (F10-C1) |

## 3.1 Medical profile — `GET /api/patients/{patientId}/medical-profile`

Returns every Class Spec §4.2 Patient attribute, including `bloodType` and `emergencyContact`,
plus the derived age and the patient's allergies and chronic conditions.

**Response `200`:**

```json
{
  "patientId": "0b6f…",
  "fullName": "Test Patient",
  "dateOfBirth": "1990-04-12",
  "age": 36,
  "gender": "Female",
  "phone": null,
  "address": null,
  "bloodType": "APositive",
  "emergencyContact": "Test emergency contact",
  "allergies": [
    { "id": "…", "name": "Penicillin", "severity": "Mild", "description": null, "notes": null }
  ],
  "chronicConditions": [
    { "id": "…", "name": "Diabetes mellitus type 2", "diagnosisDate": null, "status": "Active", "notes": null }
  ]
}
```

## 3.2 Medical history — `GET /api/patients/{patientId}/medical-visits`

One page of the patient's previous visits, newest first (`VisitDate` descending, visit id as
the stable tie-breaker), each with its linked diagnoses, medications, vital signs, visit notes
and laboratory results. Paging follows the shared Q-04 contract (`CONVENTIONS.md` §2):

| Parameter | In | Rules |
|---|---|---|
| `pageNumber` | query | `>= 1`; default `1` |
| `pageSize` | query | `1..50`; default `10` |

**Response `200`** (shared `PagedResult` envelope; a `pageNumber` beyond the last page returns
an empty `items` array with correct metadata):

```json
{
  "items": [
    {
      "visitId": "…",
      "visitDate": "2026-11-13T10:00:00+00:00",
      "doctorId": "…",
      "doctorName": "Doctor Under Test",
      "chiefComplaint": "Headache",
      "symptoms": "Since yesterday",
      "clinicalNotes": "Rest and hydration",
      "diagnoses": [ { "id": "…", "name": "Migraine", "description": "…", "diagnosisDate": "2026-11-13" } ],
      "medications": [ { "id": "…", "name": "Amoxicillin", "dosage": "500 mg", "frequency": "Every 8 hours", "duration": "7 days", "instructions": "Take after food" } ],
      "vitalSigns": [ { "id": "…", "bloodPressure": "120/80", "heartRate": 72, "temperature": null, "respiratoryRate": null, "weight": null, "height": null, "spO2": null } ],
      "notes": [ { "id": "…", "doctorId": "…", "doctorName": "…", "content": "…", "createdAtUtc": "…", "updatedAtUtc": null } ],
      "labResults": [ { "id": "…", "testName": "Hemoglobin", "testDate": "2026-11-13", "value": 13.5, "unit": "g/dL", "referenceRange": null, "notes": null } ]
    }
  ],
  "page": 1,
  "pageSize": 2,
  "totalCount": 3,
  "totalPages": 2
}
```

## 3.3 Patient timeline — `GET /api/patients/{patientId}/timeline` (F10-C1)

One page of the patient's medical timeline (decision Q-20): visits, diagnoses, medications and
clinical notes merged into a single chronological feed, newest first, each event carrying only
the fields needed for display. Vital-sign events join this contract once F9-C1 merges
(F10-C1-T4); laboratory results and documents are excluded by Q-20.

**Query parameters:**

| Parameter | In | Rules |
|---|---|---|
| `eventType` | query | Optional; one of `Visit`, `Diagnosis`, `Medication`, `MedicalNote` — case-insensitive; anything else is `400` |
| `from`, `to` | query | Optional ISO-8601 instants; `from <= to` is enforced (`400` otherwise). The window applies to visits, medications and notes as instants. **Diagnoses have no time-of-day**, so the window applies to them in UTC calendar days: a diagnosis on day D belongs to the window when `date(from) <= D <= date(to)` |
| `pageNumber` | query | `>= 1`; default `1` |
| `pageSize` | query | `1..50`; default `10` |

**Ordering contract:** `occurredAt` descending, then event `type` ascending in true ordinal
order (`Diagnosis` < `MedicalNote` < `Medication` < `Visit`), then event `id` ascending in
canonical GUID string order (the same order the `char(36)` id columns give the SQL sources) as
the stable tie-breaker.

**`occurredAt` conventions per source:**

| Event type | `occurredAt` |
|---|---|
| `Visit` | `VisitDate` (normalized UTC instant) |
| `Diagnosis` | midnight UTC of `DiagnosisDate` (the column is a calendar date without a time) |
| `Medication` | the `VisitDate` of the visit that produced the prescription item (a medication has no date of its own) |
| `MedicalNote` | `createdAtUtc` (audit-stamped creation instant) |

Paging follows the shared Q-04 envelope; each source is counted and truncated at the data
source (SQL), never the whole timeline in memory. A `pageNumber` beyond the last page returns
an empty `items` array with correct metadata.

**Response `200`:**

```json
{
  "items": [
    {
      "id": "…",
      "type": "MedicalNote",
      "occurredAt": "2026-11-15T09:30:00+00:00",
      "title": "Clinical Note",
      "summary": "Follow up in one week.",
      "relatedVisitId": null
    },
    {
      "id": "…",
      "type": "Medication",
      "occurredAt": "2026-11-14T10:00:00+00:00",
      "title": "Amoxicillin",
      "summary": "500 mg · Every 8 hours",
      "relatedVisitId": "…"
    },
    {
      "id": "…",
      "type": "Visit",
      "occurredAt": "2026-11-14T10:00:00+00:00",
      "title": "Medical Visit",
      "summary": "Persistent headache",
      "relatedVisitId": "…"
    },
    {
      "id": "…",
      "type": "Diagnosis",
      "occurredAt": "2026-11-14T00:00:00+00:00",
      "title": "Tension headache",
      "summary": "Pressure-like pain",
      "relatedVisitId": "…"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 4,
  "totalPages": 1
}
```

**Display projection:**

| Event type | `title` | `summary` |
|---|---|---|
| `Visit` | fixed `"Medical Visit"` | `chiefComplaint` |
| `Diagnosis` | `diagnosisName` | `description` |
| `Medication` | `medicationName` | `"dosage · frequency"` (empty parts skipped; `null` when both are empty) |
| `MedicalNote` | fixed `"Clinical Note"` | `noteContent` |

`relatedVisitId` points at the visit that produced the event (the visit itself, the parent
visit for diagnoses/medications, or the optional note link; `null` for an unlinked note).

## 4. Errors

| Status | When | `code` |
|---|---|---|
| `401` | No/invalid credentials | `auth.unauthorized` (shared envelope) |
| `403` | Authenticated non-doctor | `access.forbidden` (framework role check; the Application boundary would yield `medical.access_denied`) |
| `403` | Authenticated doctor with an unusable identity (missing/inactive doctor) | `identity.invalid` |
| `404` | Unknown patient, patient with no owning doctor, or another doctor's patient | `patient.not_found` (identical for all three) |
| `400` | `pageNumber < 1` or `pageSize` outside `1..50` | `validation.failed` with camelCase `errors`/`errorCodes` per field |
| `400` | Timeline: unknown `eventType`, or `from > to` | `validation.failed` on `eventType` / `from` |

## 5. Explicitly out of scope

- Any write operation on medical data (F4, `MEDICAL_RECORDS_API.md`; the acting doctor resolves
  from the same `ICurrentUser`, but routes and use cases are separate).
- Medical documents (view/download) and patient-level (unlinked) laboratory results — deferred
  by the approved F3-C1 scope decision (task file D-11); they return via a later approved cycle,
  not via these endpoints. (Patient-level notes and the patient timeline are delivered by
  F10-C1 above.)
- Timeline vital-sign events — deferred to F10-C1-T4 until F9-C1 merges (Q-20).
- Any "patient status" view — blocked on the open question Q-03; no `PatientStatus` field exists
  in the domain model.
