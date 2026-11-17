import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {ApiError, isApiError, resetAuthHeadersProviders} from '@/lib/api';
import {actAs, DOCTOR_ID, FakeApi, makePatient, pageOf, problem} from '../test/test-utils';
import {getPatientErrorKey, isRetryableError} from './patient-errors';
import {readPatient, readPatientOptions, readPatientPage} from './patient-readers';
import {
  createPatient,
  deletePatient,
  getPatient,
  listPatients,
  lookupPatients,
  updatePatient
} from './patients.api';

let api: FakeApi;

beforeEach(() => {
  api = new FakeApi();
  api.install();
  actAs('Doctor');
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

const BASE = {
  fullName: 'Sara Ali',
  dateOfBirth: '1990-04-12',
  gender: 'Female',
  phone: '01001234567',
  address: null,
  emergencyContact: null
} as const;

describe('patients api', () => {
  it('lists with the shared paging contract and drops empty filters', async () => {
    api.on('GET', '/api/patients', {body: pageOf([makePatient()])});

    const result = await listPatients({search: '  mona ', gender: 'all', pageNumber: 2, pageSize: 10});

    const call = api.callsTo('GET', '/api/patients')[0];
    expect(call?.query.get('search')).toBe('mona');
    expect(call?.query.get('pageNumber')).toBe('2');
    expect(call?.query.get('pageSize')).toBe('10');
    expect(call?.query.has('gender')).toBe(false);
    expect(result.items).toHaveLength(1);
    expect(result.totalPages).toBe(1);
  });

  it('sends the gender filter when one is chosen and omits an empty search', async () => {
    api.on('GET', '/api/patients', {body: pageOf([])});

    await listPatients({search: '', gender: 'Male', pageNumber: 1, pageSize: 10});

    const call = api.callsTo('GET', '/api/patients')[0];
    expect(call?.query.get('gender')).toBe('Male');
    expect(call?.query.has('search')).toBe(false);
  });

  it('creates a patient for a doctor with the blood type and without a doctorId', async () => {
    api.on('POST', '/api/patients', {status: 201, body: makePatient({bloodType: 'OPositive'})});

    await createPatient({...BASE, bloodType: 'OPositive'});

    expect(api.callsTo('POST', '/api/patients')[0]?.body).toEqual({...BASE, bloodType: 'OPositive'});
  });

  it('never sends a blood type when the input does not carry one (secretary)', async () => {
    api.on('POST', '/api/patients', {status: 201, body: makePatient()});

    await createPatient({...BASE, doctorId: DOCTOR_ID});

    const body = api.callsTo('POST', '/api/patients')[0]?.body as Record<string, unknown>;
    expect(body.doctorId).toBe(DOCTOR_ID);
    expect('bloodType' in body).toBe(false);
  });

  it('updates with PUT and reads one patient by id', async () => {
    const patient = makePatient();
    api.on('PUT', `/api/patients/${patient.id}`, {body: patient});
    api.on('GET', `/api/patients/${patient.id}`, {body: patient});

    await updatePatient(patient.id, {...BASE, bloodType: null});
    const read = await getPatient(patient.id);

    expect(api.callsTo('PUT', `/api/patients/${patient.id}`)[0]?.body).toMatchObject({bloodType: null});
    expect(read.id).toBe(patient.id);
  });

  it('deletes with DELETE and accepts 204', async () => {
    api.on('DELETE', '/api/patients/p1', {status: 204});

    await expect(deletePatient('p1')).resolves.toBeUndefined();
    expect(api.callsTo('DELETE', '/api/patients/p1')).toHaveLength(1);
  });

  it('surfaces the 409 delete conflict as an ApiError with its code', async () => {
    api.on('DELETE', '/api/patients/p1', problem(409, 'patient.has_related_records'));

    const error = await deletePatient('p1').catch((caught: unknown) => caught);

    expect(isApiError(error)).toBe(true);
    expect((error as ApiError).status).toBe(409);
    expect(getPatientErrorKey(error)).toBe('hasRelatedRecords');
  });

  it('looks patients up with the doctor, search text and a bounded limit', async () => {
    api.on('GET', '/api/patients/lookup', {body: [{id: 'p1', fullName: 'Mona', phone: null}]});

    const result = await lookupPatients(' mo ', DOCTOR_ID);

    const call = api.callsTo('GET', '/api/patients/lookup')[0];
    expect(call?.query.get('search')).toBe('mo');
    expect(call?.query.get('doctorId')).toBe(DOCTOR_ID);
    expect(call?.query.get('limit')).toBe('10');
    expect(result).toEqual([{id: 'p1', fullName: 'Mona', phone: null}]);
  });

  it('omits the doctor from the lookup when none is given', async () => {
    api.on('GET', '/api/patients/lookup', {body: []});

    await lookupPatients('mo', undefined);

    expect(api.callsTo('GET', '/api/patients/lookup')[0]?.query.has('doctorId')).toBe(false);
  });
});

describe('patient readers', () => {
  it('reads a secretary response that has no blood type property', () => {
    const withoutBlood: Record<string, unknown> = {...makePatient()};
    delete withoutBlood.bloodType;

    expect('bloodType' in withoutBlood).toBe(false);
    expect(readPatient(withoutBlood).bloodType).toBeNull();
  });

  it('reads a doctor response with a blood type', () => {
    expect(readPatient(makePatient({bloodType: 'ABNegative'})).bloodType).toBe('ABNegative');
  });

  it('rejects unknown enum values and malformed bodies as invalid responses', () => {
    const invalid = (action: () => unknown) => {
      expect(action).toThrow();
      try {
        action();
      } catch (error) {
        expect((error as ApiError).kind).toBe('invalid-response');
      }
    };

    invalid(() => readPatient({...makePatient(), gender: 'Alien'}));
    invalid(() => readPatient({...makePatient(), bloodType: 'Z+'}));
    invalid(() => readPatient({...makePatient(), fullName: 5}));
    invalid(() => readPatient(null));
    invalid(() => readPatientPage({items: 'x'}));
    invalid(() => readPatientOptions({}));
  });

  it('keeps only the minimal fields of a lookup row', () => {
    expect(readPatientOptions([{id: 'p', fullName: 'N', phone: '1', address: 'secret', bloodType: 'A'}])).toEqual([
      {id: 'p', fullName: 'N', phone: '1'}
    ]);
  });
});

describe('patient error keys', () => {
  const apiError = (status: number, code: string | null) => new ApiError({kind: 'conflict', status, code});

  it.each([
    ['patient.has_related_records', 409, 'hasRelatedRecords'],
    ['patient.not_found', 404, 'patientNotFound'],
    ['doctor.not_found', 404, 'doctorNotFound'],
    ['patient.medical_field_forbidden', 403, 'medicalForbidden'],
    ['validation.failed', 400, 'validation'],
    ['identity.invalid', 403, 'identity']
  ])('maps %s to %s', (code, status, key) => {
    expect(getPatientErrorKey(apiError(status, code))).toBe(key);
  });

  it('falls back to the status and treats non-API errors as unknown', () => {
    expect(getPatientErrorKey(apiError(403, null))).toBe('forbidden');
    expect(getPatientErrorKey(apiError(404, 'something.else'))).toBe('notFound');
    expect(getPatientErrorKey(apiError(503, null))).toBe('server');
    expect(getPatientErrorKey(new Error('x'))).toBe('unknown');
  });

  it('retries only network and server failures', () => {
    expect(isRetryableError(apiError(0, null))).toBe(true);
    expect(isRetryableError(apiError(500, null))).toBe(true);
    expect(isRetryableError(apiError(409, null))).toBe(false);
  });
});
