import {describe, expect, it} from 'vitest';
import {isApiError} from '@/lib/api';
import {
  readAppointment,
  readAppointmentPage,
  readDoctorLookups,
  readPatientLookups
} from './appointment-readers';

const APPOINTMENT = {
  id: 'a1',
  patientId: 'p1',
  patientName: 'Mona Said',
  patientPhone: '0101',
  doctorId: 'd1',
  doctorName: 'Dr. One',
  appointmentDate: '2030-01-02',
  startTime: '10:00:00',
  endTime: '10:30:00',
  reason: null,
  status: 'Scheduled',
  notes: null,
  cancellationReason: null,
  createdBy: 'u1',
  createdAtUtc: '2030-01-01T06:00:00+00:00'
};

function invalid(action: () => unknown) {
  try {
    action();
  } catch (error) {
    expect(isApiError(error) && error.kind === 'invalid-response').toBe(true);
    return;
  }

  throw new Error('expected an invalid-response error');
}

describe('readAppointment', () => {
  it('reads a complete response', () => {
    expect(readAppointment(APPOINTMENT)).toEqual(APPOINTMENT);
  });

  it('accepts nullable fields as null or missing', () => {
    const withoutOptionals = Object.fromEntries(
      Object.entries(APPOINTMENT).filter(([key]) => key !== 'patientPhone' && key !== 'reason')
    );

    expect(readAppointment({...withoutOptionals, endTime: null})).toMatchObject({
      patientPhone: null,
      reason: null,
      endTime: null
    });
  });

  it.each(['id', 'patientName', 'doctorName', 'appointmentDate', 'startTime', 'status', 'createdBy'])(
    'rejects a missing %s',
    (field) => {
      invalid(() => readAppointment({...APPOINTMENT, [field]: undefined}));
    }
  );

  it('rejects an unknown status, wrong types and non-objects', () => {
    invalid(() => readAppointment({...APPOINTMENT, status: 'No-Show'}));
    invalid(() => readAppointment({...APPOINTMENT, status: 2}));
    invalid(() => readAppointment({...APPOINTMENT, startTime: 1000}));
    invalid(() => readAppointment(null));
    invalid(() => readAppointment('x'));
    invalid(() => readAppointment([APPOINTMENT]));
  });
});

describe('readAppointmentPage', () => {
  it('reads the paged envelope', () => {
    const page = readAppointmentPage({items: [APPOINTMENT], page: 2, pageSize: 10, totalCount: 11, totalPages: 2});

    expect(page.items).toHaveLength(1);
    expect(page).toMatchObject({page: 2, pageSize: 10, totalCount: 11, totalPages: 2});
  });

  it('rejects a malformed envelope or item', () => {
    invalid(() => readAppointmentPage({items: 'x', page: 1, pageSize: 10, totalCount: 0, totalPages: 0}));
    invalid(() => readAppointmentPage({items: [], page: '1', pageSize: 10, totalCount: 0, totalPages: 0}));
    invalid(() => readAppointmentPage({items: [{id: 'only'}], page: 1, pageSize: 10, totalCount: 1, totalPages: 1}));
    invalid(() => readAppointmentPage([]));
  });
});

describe('lookup readers', () => {
  it('reads doctor and patient lookups with only the minimal fields', () => {
    expect(readDoctorLookups([{id: 'd1', fullName: 'Dr. One', specialization: null, extra: 'ignored'}])).toEqual([
      {id: 'd1', fullName: 'Dr. One', specialization: null}
    ]);
    expect(readPatientLookups([{id: 'p1', fullName: 'Mona', phone: '0101', address: 'secret'}])).toEqual([
      {id: 'p1', fullName: 'Mona', phone: '0101'}
    ]);
  });

  it('rejects non-arrays and malformed items', () => {
    invalid(() => readDoctorLookups({}));
    invalid(() => readPatientLookups([{id: 'p1'}]));
    invalid(() => readPatientLookups(null));
  });
});
