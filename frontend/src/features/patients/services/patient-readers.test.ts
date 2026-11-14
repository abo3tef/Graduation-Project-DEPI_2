import {describe, expect, it} from 'vitest';
import {isApiError} from '@/lib/api';
import {
  readMedicalProfile,
  readPatientLookup,
  readPatientLookups,
  readVisit,
  readVisitPage
} from './patient-readers';
import {makeProfile, makeVisit} from '../test/test-utils';

const PROFILE = makeProfile();
const VISIT = makeVisit();

function invalid(action: () => unknown) {
  try {
    action();
  } catch (error) {
    expect(isApiError(error) && error.kind === 'invalid-response').toBe(true);
    return;
  }

  throw new Error('expected an invalid-response error');
}

describe('readPatientLookup', () => {
  it('reads a complete lookup', () => {
    const lookup = {id: 'b1', fullName: 'Mona Said', phone: '0101'};

    expect(readPatientLookup(lookup)).toEqual(lookup);
  });

  it('accepts a missing phone as null', () => {
    expect(readPatientLookup({id: 'b1', fullName: 'Mona'}).phone).toBeNull();
  });

  it('rejects a missing name', () => {
    invalid(() => readPatientLookup({id: 'b1'}));
  });

  it('rejects a non-object', () => {
    invalid(() => readPatientLookup('Mona'));
  });
});

describe('readPatientLookups', () => {
  it('reads an array and rejects non-arrays', () => {
    expect(readPatientLookups([{id: 'b1', fullName: 'Mona', phone: null}])).toHaveLength(1);
    invalid(() => readPatientLookups({id: 'b1'}));
  });
});

describe('readMedicalProfile', () => {
  it('reads a complete profile', () => {
    expect(readMedicalProfile(PROFILE)).toEqual(PROFILE);
  });

  it('accepts every optional field as null', () => {
    const minimal = readMedicalProfile({
      patientId: 'b1',
      fullName: 'Mona',
      dateOfBirth: '1990-04-12',
      age: 35,
      gender: 'Female',
      phone: null,
      address: null,
      bloodType: null,
      emergencyContact: null,
      allergies: [],
      chronicConditions: []
    });

    expect(minimal.phone).toBeNull();
    expect(minimal.allergies).toEqual([]);
  });

  it('rejects a string age', () => {
    invalid(() => readMedicalProfile({...PROFILE, age: '35'}));
  });

  it('rejects a missing allergy severity', () => {
    invalid(() =>
      readMedicalProfile({
        ...PROFILE,
        allergies: [{id: 'c1', name: 'Peanuts', description: null, notes: null}]
      })
    );
  });

  it('rejects a non-array chronicConditions', () => {
    invalid(() => readMedicalProfile({...PROFILE, chronicConditions: {}}));
  });
});

describe('readVisit', () => {
  it('reads a complete visit', () => {
    expect(readVisit(VISIT)).toEqual(VISIT);
  });

  it('accepts nullable visit-level fields as null and empty child lists', () => {
    const withoutOptionals = {
      visitId: VISIT.visitId,
      visitDate: VISIT.visitDate,
      doctorId: VISIT.doctorId,
      doctorName: VISIT.doctorName,
      chiefComplaint: null,
      symptoms: null,
      clinicalNotes: null,
      diagnoses: [],
      medications: [],
      vitalSigns: [],
      notes: [],
      labResults: []
    };

    const read = readVisit(withoutOptionals);

    expect(read.chiefComplaint).toBeNull();
    expect(read.symptoms).toBeNull();
    expect(read.clinicalNotes).toBeNull();
    expect(read.diagnoses).toEqual([]);
  });

  it('rejects a visit without visitId', () => {
    invalid(() => readVisit({...VISIT, visitId: undefined}));
  });

  it('rejects a non-array diagnoses list', () => {
    invalid(() => readVisit({...VISIT, diagnoses: 'none'}));
  });

  it('rejects a lab result with a string value', () => {
    invalid(() =>
      readVisit({
        ...VISIT,
        labResults: [{...VISIT.labResults[0], value: '12.5'}]
      })
    );
  });

  it('rejects a vital sign with a string heartRate', () => {
    invalid(() =>
      readVisit({
        ...VISIT,
        vitalSigns: [{...VISIT.vitalSigns[0], heartRate: '72'}]
      })
    );
  });
});

describe('readVisitPage', () => {
  it('reads the shared paged envelope', () => {
    const envelope = {
      items: [VISIT],
      page: 2,
      pageSize: 10,
      totalCount: 11,
      totalPages: 2
    };

    expect(readVisitPage(envelope)).toEqual({
      items: [VISIT],
      page: 2,
      pageSize: 10,
      totalCount: 11,
      totalPages: 2
    });
  });

  it('rejects a missing totalCount', () => {
    invalid(() => readVisitPage({items: [], page: 1, pageSize: 10, totalPages: 0}));
  });

  it('rejects items that are not visits', () => {
    invalid(() => readVisitPage({items: [{visitId: 1}], page: 1, pageSize: 10, totalCount: 1, totalPages: 1}));
  });
});
