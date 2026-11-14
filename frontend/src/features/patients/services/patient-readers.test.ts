import {describe, expect, it} from 'vitest';
import {isApiError} from '@/lib/api';
import {
  readLabPage,
  readMedicalProfile,
  readNotePage,
  readPatientLabResult,
  readPatientLookup,
  readPatientLookups,
  readPatientNote,
  readVisit,
  readVisitPage
} from './patient-readers';
import {makePatientLabResult, makePatientNote, makeProfile, makeVisit} from '../test/test-utils';

const PROFILE = makeProfile();
const VISIT = makeVisit();
const NOTE = makePatientNote();
const LAB_RESULT = makePatientLabResult();

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

describe('readPatientNote', () => {
  it('reads a visit-linked note', () => {
    expect(readPatientNote(NOTE)).toEqual(NOTE);
  });

  it('accepts visitId and updatedAtUtc as null for an unlinked note', () => {
    const unlinked = readPatientNote({...NOTE, visitId: null, updatedAtUtc: null});

    expect(unlinked.visitId).toBeNull();
    expect(unlinked.updatedAtUtc).toBeNull();
  });

  it('rejects a note without noteContent', () => {
    invalid(() => readPatientNote({...NOTE, noteContent: undefined}));
  });

  it('rejects a missing doctorName', () => {
    invalid(() => readPatientNote({...NOTE, doctorName: undefined}));
  });

  it('rejects a non-object', () => {
    invalid(() => readPatientNote('a note'));
  });
});

describe('readPatientLabResult', () => {
  it('reads a visit-linked result with a reference range', () => {
    expect(readPatientLabResult(LAB_RESULT)).toEqual(LAB_RESULT);
  });

  it('accepts visitId as null for an unlinked result', () => {
    expect(readPatientLabResult({...LAB_RESULT, visitId: null}).visitId).toBeNull();
  });

  it('rejects a string value', () => {
    invalid(() => readPatientLabResult({...LAB_RESULT, value: '13.5'}));
  });

  it('rejects a missing testName', () => {
    invalid(() => readPatientLabResult({...LAB_RESULT, testName: undefined}));
  });

  it('rejects a missing unit', () => {
    invalid(() => readPatientLabResult({...LAB_RESULT, unit: undefined}));
  });
});

describe('readNotePage', () => {
  it('reads the shared paged envelope', () => {
    expect(
      readNotePage({items: [NOTE], page: 1, pageSize: 10, totalCount: 1, totalPages: 1})
    ).toEqual({items: [NOTE], page: 1, pageSize: 10, totalCount: 1, totalPages: 1});
  });

  it('accepts an empty beyond-last page', () => {
    const emptyPage = {items: [], page: 3, pageSize: 10, totalCount: 21, totalPages: 3};

    expect(readNotePage(emptyPage)).toEqual(emptyPage);
  });

  it('rejects items that are not notes', () => {
    invalid(() => readNotePage({items: [{id: 1}], page: 1, pageSize: 10, totalCount: 1, totalPages: 1}));
  });
});

describe('readLabPage', () => {
  it('reads the shared paged envelope', () => {
    expect(
      readLabPage({items: [LAB_RESULT], page: 2, pageSize: 10, totalCount: 11, totalPages: 2})
    ).toEqual({items: [LAB_RESULT], page: 2, pageSize: 10, totalCount: 11, totalPages: 2});
  });

  it('rejects a missing totalPages', () => {
    invalid(() => readLabPage({items: [], page: 1, pageSize: 10, totalCount: 0}));
  });

  it('rejects items that are not lab results', () => {
    invalid(() => readLabPage({items: [{}], page: 1, pageSize: 10, totalCount: 1, totalPages: 1}));
  });
});
