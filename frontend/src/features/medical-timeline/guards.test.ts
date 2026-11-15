import {describe, expect, it} from 'vitest';
import {isApiError} from '@/lib/api';
import {
  expectPatientAllergy,
  expectPatientChronicCondition,
  expectPatientMedicalProfile,
  expectPatientTimelineEvent
} from './guards';

/**
 * Payloads mirror backend/docs/api/PATIENT_MEDICAL_API.md §3.1/§3.3 (frozen by F10-C1)
 * and the nullability of `MedicalRecordResponses.cs`.
 */

const TIMELINE_EVENT = {
  id: '6f0a…',
  type: 'Medication',
  occurredAt: '2026-11-14T10:00:00+00:00',
  title: 'Amoxicillin',
  summary: '500 mg · Every 8 hours',
  relatedVisitId: '9c1b…'
};

function invalid(action: () => unknown): void {
  try {
    action();
  } catch (error) {
    expect(isApiError(error) && error.kind === 'invalid-response').toBe(true);
    return;
  }

  throw new Error('expected an invalid-response error');
}

describe('expectPatientTimelineEvent', () => {
  it('reads the contract payload with every field', () => {
    expect(expectPatientTimelineEvent(TIMELINE_EVENT)).toEqual(TIMELINE_EVENT);
  });

  it('reads all four delivered event types', () => {
    for (const type of ['Visit', 'Diagnosis', 'Medication', 'MedicalNote'] as const) {
      expect(expectPatientTimelineEvent({...TIMELINE_EVENT, type}).type).toBe(type);
    }
  });

  it('accepts nullable summary and relatedVisitId as explicit nulls', () => {
    expect(
      expectPatientTimelineEvent({...TIMELINE_EVENT, summary: null, relatedVisitId: null})
    ).toMatchObject({summary: null, relatedVisitId: null});
  });

  it('rejects an event type outside the delivered contract', () => {
    invalid(() => expectPatientTimelineEvent({...TIMELINE_EVENT, type: 'VitalSigns'}));
    invalid(() => expectPatientTimelineEvent({...TIMELINE_EVENT, type: 'visit'}));
    invalid(() => expectPatientTimelineEvent({...TIMELINE_EVENT, type: 3}));
  });

  it('rejects missing or wrongly-typed fields', () => {
    for (const field of ['id', 'type', 'occurredAt', 'title', 'summary', 'relatedVisitId'] as const) {
      const broken = {...TIMELINE_EVENT};
      delete broken[field];

      invalid(() => expectPatientTimelineEvent(broken));
    }

    invalid(() => expectPatientTimelineEvent({...TIMELINE_EVENT, occurredAt: 17}));
    invalid(() => expectPatientTimelineEvent(null));
    invalid(() => expectPatientTimelineEvent([TIMELINE_EVENT]));
  });
});

describe('expectPatientMedicalProfile', () => {
  const PROFILE = {
    patientId: '0b6f…',
    fullName: 'Test Patient',
    dateOfBirth: '1990-04-12',
    age: 36,
    gender: 'Female',
    phone: null,
    address: null,
    bloodType: 'APositive',
    emergencyContact: 'Test emergency contact',
    allergies: [
      {id: '…', name: 'Penicillin', severity: 'Mild', description: null, notes: null}
    ],
    chronicConditions: [
      {
        id: '…',
        name: 'Diabetes mellitus type 2',
        diagnosisDate: null,
        status: 'Active',
        notes: null
      }
    ]
  };

  it('reads the contract payload with every field', () => {
    expect(expectPatientMedicalProfile(PROFILE)).toEqual(PROFILE);
  });

  it('accepts nullable bloodType, mirroring the nullable backend DTO', () => {
    expect(expectPatientMedicalProfile({...PROFILE, bloodType: null}).bloodType).toBeNull();
  });

  it('rejects non-integer or missing age', () => {
    invalid(() => expectPatientMedicalProfile({...PROFILE, age: 36.5}));
    invalid(() => expectPatientMedicalProfile({...PROFILE, age: '36'}));

    const broken = {...PROFILE};
    delete (broken as Record<string, unknown>).age;

    invalid(() => expectPatientMedicalProfile(broken));
  });

  it('rejects a broken allergy or condition entry', () => {
    invalid(() =>
      expectPatientMedicalProfile({
        ...PROFILE,
        allergies: [{id: '…', name: 'Penicillin', severity: 'Mild'}]
      })
    );
    invalid(() =>
      expectPatientMedicalProfile({
        ...PROFILE,
        chronicConditions: [{id: '…', name: 'Diabetes', diagnosisDate: null}]
      })
    );
  });

  it('rejects missing collections or invalid scalars', () => {
    invalid(() => expectPatientMedicalProfile({...PROFILE, allergies: undefined}));
    invalid(() => expectPatientMedicalProfile({...PROFILE, gender: 2}));
    invalid(() => expectPatientMedicalProfile('profile'));
  });
});

describe('sub-guards', () => {
  it('read nullable severity, diagnosisDate, status and notes', () => {
    expect(
      expectPatientAllergy({id: 'a', name: 'Peanuts', severity: null, description: null, notes: null})
    ).toMatchObject({severity: null});
    expect(
      expectPatientChronicCondition({
        id: 'c',
        name: 'Asthma',
        diagnosisDate: null,
        status: null,
        notes: null
      })
    ).toMatchObject({diagnosisDate: null, status: null});
  });

  it('reject wrongly-typed sub-guard fields', () => {
    invalid(() => expectPatientAllergy({id: 1, name: 'Peanuts'}));
    invalid(() => expectPatientChronicCondition({id: 'c', name: 3}));
  });
});
