# Patient Medical API (F3-C2, F3-C3a, F3-C3b)

Doctor-facing **read** endpoints for a patient's medical profile, paged medical history,
patient-level notes and lab results, the medical timeline and medical document metadata
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
| 3 | `GET` | `/api/patients/{patientId}/notes` | `200 OK` (F3-C3a) |
| 4 | `GET` | `/api/patients/{patientId}/lab-results` | `200 OK` (F3-C3a) |
| 5 | `GET` | `/api/patients/{patientId}/timeline` | `200 OK` (F3-C3b) |
| 6 | `GET` | `/api/patients/{patientId}/documents` | `200 OK` (F3-C3b) |

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

## 3.3 Patient notes — `GET /api/patients/{patientId}/notes` (F3-C3a)

Every clinical note of the patient — **visit-linked and unlinked** (Class Spec §4.10
`listByPatient()`), newest first (`createdAtUtc` descending, note id as tie-breaker). A note
attached to a visit also appears inside that visit in §3.2; `visitId` is `null` for an
unlinked note.

**Response `200`** (shared envelope; paging per §2 of `CONVENTIONS.md`):

```json
{
  "items": [
    {
      "id": "…",
      "visitId": "…",
      "doctorId": "…",
      "doctorName": "Doctor Under Test",
      "noteContent": "…",
      "createdAtUtc": "2026-11-13T10:00:00+00:00",
      "updatedAtUtc": null
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

## 3.4 Patient lab results — `GET /api/patients/{patientId}/lab-results` (F3-C3a)

Every laboratory result of the patient — **visit-linked and unlinked** (Class Spec §4.9
`getHistory()`), newest test first (`testDate` descending, result id as tie-breaker);
`visitId` is `null` for an unlinked result.

**Response `200`:**

```json
{
  "items": [
    {
      "id": "…",
      "visitId": "…",
      "testName": "Hemoglobin",
      "testDate": "2026-11-13",
      "value": 13.5,
      "unit": "g/dL",
      "referenceRange": null,
      "notes": null
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

## 3.5 Medical timeline — `GET /api/patients/{patientId}/timeline` (F3-C3b)

One page of the patient's medical timeline (Class Spec §4.2 `getTimeline()`; approved decision
Q-12): a single chronological feed over the four persisted event sources — **visits**,
**patient-level notes**, **patient-level lab results** and **medical document metadata**.
Ordering is `occurredAtUtc` descending with two deterministic tie-breakers: `eventType`
ascending (ordinal string order: `document` < `lab_result` < `note` < `visit`), then event `id`
ascending. Paging follows the shared Q-04 contract (§2 of `CONVENTIONS.md`).

Event payloads are **summaries** (D-T1…D-T3): a visit event carries header fields and child
counts, never the full visit; a note event carries the full note content; a lab event carries
name/value/unit; a document event carries metadata only. `visitId` carries the F4 deep-link
target where the event belongs to a visit (document events have none).

**Timestamp convention:** `occurredAtUtc` is the visit date, note creation instant, upload
instant — or, for lab results, **midnight UTC of the test date** (the `testDate` column carries
no time-of-day; the convention is deterministic because the tie-breakers order equal instants).

`eventType` is one of the contract constants `visit`, `note`, `lab_result`, `document`. Exactly
one payload property (`visit`, `note`, `labResult`, `document`) is non-null, matching
`eventType`.

**Response `200`:**

```json
{
  "items": [
    {
      "id": "…",
      "eventType": "note",
      "occurredAtUtc": "2026-11-13T10:00:00+00:00",
      "visitId": "…",
      "visit": null,
      "note": { "doctorName": "Doctor Under Test", "noteContent": "…" },
      "labResult": null,
      "document": null
    },
    {
      "id": "…",
      "eventType": "document",
      "occurredAtUtc": "2026-11-12T09:00:00+00:00",
      "visitId": null,
      "visit": null,
      "note": null,
      "labResult": null,
      "document": { "fileName": "mri-scan.pdf", "fileType": "Pdf", "description": "MRI of the knee", "uploadedByDoctorName": "Doctor Under Test" }
    },
    {
      "id": "…",
      "eventType": "lab_result",
      "occurredAtUtc": "2026-11-11T00:00:00+00:00",
      "visitId": "…",
      "visit": null,
      "note": null,
      "labResult": { "testName": "Hemoglobin", "value": 13.5, "unit": "g/dL" },
      "document": null
    },
    {
      "id": "…",
      "eventType": "visit",
      "occurredAtUtc": "2026-11-10T08:00:00+00:00",
      "visitId": "…",
      "visit": { "doctorName": "Doctor Under Test", "chiefComplaint": "Headache", "diagnosesCount": 1, "medicationsCount": 2, "notesCount": 1, "labResultsCount": 1 },
      "note": null,
      "labResult": null,
      "document": null
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 4,
  "totalPages": 1
}
```

## 3.6 Medical documents — `GET /api/patients/{patientId}/documents` (F3-C3b)

One page of the patient's medical document **metadata** (Class Spec §4.11; approved decision
Q-13), newest upload first (`uploadDateUtc` descending, document id as tie-breaker). A document
without an uploader (Class Spec allows `UploadedBy` to be empty) simply omits
`uploadedByDoctorName` (D-T5).

**Document privacy:** the response — like every document payload in §3.5 — carries **no**
`location`, storage path or file URL. File viewing and downloading are deferred until an
approved secure storage and an authenticated, ownership-checked file-serving flow exist, and
document upload is out of the current F3 scope. There is deliberately **no** file endpoint in
this API.

**Response `200`:**

```json
{
  "items": [
    {
      "id": "…",
      "fileName": "mri-scan.pdf",
      "type": "Pdf",
      "uploadDateUtc": "2026-11-12T09:00:00+00:00",
      "description": "MRI of the knee",
      "uploadedByDoctorName": "Doctor Under Test"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

## 4. Errors

| Status | When | `code` |
|---|---|---|
| `401` | No/invalid credentials | `auth.unauthorized` (shared envelope) |
| `403` | Authenticated non-doctor | `access.forbidden` (framework role check; the Application boundary would yield `medical.access_denied`) |
| `403` | Authenticated doctor with an unusable identity (missing/inactive doctor) | `identity.invalid` |
| `404` | Unknown patient, patient with no owning doctor, or another doctor's patient | `patient.not_found` (identical for all three) |
| `400` | `pageNumber < 1` or `pageSize` outside `1..50` | `validation.failed` with camelCase `errors`/`errorCodes` per field |

## 5. Explicitly out of scope

- Any write operation on medical data (F4, `MEDICAL_RECORDS_API.md`; the acting doctor resolves
  from the same `ICurrentUser`, but routes and use cases are separate).
- **File handling for medical documents** (approved decision Q-13, 2026-11-14): document upload,
  viewing and downloading. The timeline (§3.5) and document metadata listing (§3.6) are delivered
  by F3-C3b; `view()`/`download()` return via a later approved cycle, only after secure storage
  and an authenticated, ownership-checked file-serving flow exist (SRS §11 size limits are not in
  the repository). Until then no file URL, storage path or `location` value leaves the API.
- Any "patient status" view — blocked on the open question Q-03; no `PatientStatus` field exists
  in the domain model.
